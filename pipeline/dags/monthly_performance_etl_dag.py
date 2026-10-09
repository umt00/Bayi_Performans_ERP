import os
import uuid
import pandas as pd
import pendulum
from datetime import datetime, timezone, date
from airflow import DAG
from airflow.operators.python import PythonOperator
from airflow.providers.postgres.hooks.postgres import PostgresHook

PENALTY_WEIGHTS = {
    "MARJ_ESIK_ALTI": 40,
    "HEDEF_ALTINDA": 30,
    "YUKSEK_ISKONTO": 20,
    "STOK_YETERSIZ": 20,
    "YUKSEK_IADE": 20
}

def extract_transform_and_load(**context):
    """
    Airflow PostgresHook kullanarak Supabase'deki ham tablolardan (satis, hedefler, urunler, vw_dinamik_stok_esigi)
    aylık bayi metriklerini hesaplar:
    - İade hesabında `IsIade = TRUE` ve negatif miktarları dikkate alır.
    - Bayinin bölgesine/deposuna göre fiili kritik stok sayısını (vw_dinamik_stok_esigi) hesaplar.
    - `bayi_donem_performans` tablosuna aylık karne satırı olarak (INSERT/UPDATE) ekler.
    """
    execution_date = context.get('logical_date')
    if execution_date:
        calc_date = execution_date.in_tz("Europe/Istanbul").date()
    else:
        calc_date = pendulum.now("Europe/Istanbul").date()
        
    donem_str = calc_date.strftime("%Y-%m-01")
    
    hook = PostgresHook(postgres_conn_id='supabase')
    conn = hook.get_conn()
    
    # 1. EXTRACT: Satışlar (IsIade destekli), hedefler, ürünler ve depodaki kritik stok sayıları
    query = f"""
    WITH bayi_depo_esleme AS (
        SELECT 
            b."BayiKodu" AS bayi_kodu,
            CASE 
                WHEN b."Sehir" ILIKE '%istanbul%' OR b."Sehir" ILIKE '%kocaeli%' OR b."Sehir" ILIKE '%tekirdağ%' OR b."Sehir" ILIKE '%bursa%' OR b."Bolge" ILIKE '%marmara%' THEN 'DP01'
                WHEN b."Sehir" ILIKE '%ankara%' OR b."Sehir" ILIKE '%eskişehir%' OR b."Sehir" ILIKE '%konya%' OR b."Sehir" ILIKE '%kayseri%' OR b."Bolge" ILIKE '%iç anadolu%' OR b."Bolge" ILIKE '%ic anadolu%' OR b."Bolge" ILIKE '%doğu anadolu%' OR b."Bolge" ILIKE '%dogu anadolu%' THEN 'DP02'
                WHEN b."Sehir" ILIKE '%izmir%' OR b."Sehir" ILIKE '%manisa%' OR b."Sehir" ILIKE '%aydın%' OR b."Sehir" ILIKE '%denizli%' OR b."Bolge" ILIKE '%ege%' THEN 'DP03'
                WHEN b."Sehir" ILIKE '%adana%' OR b."Sehir" ILIKE '%antalya%' OR b."Sehir" ILIKE '%mersin%' OR b."Sehir" ILIKE '%hatay%' OR b."Bolge" ILIKE '%akdeniz%' THEN 'DP04'
                WHEN b."Sehir" ILIKE '%samsun%' OR b."Sehir" ILIKE '%trabzon%' OR b."Sehir" ILIKE '%ordu%' OR b."Sehir" ILIKE '%rize%' OR b."Bolge" ILIKE '%karadeniz%' THEN 'DP05'
                WHEN b."Sehir" ILIKE '%gaziantep%' OR b."Sehir" ILIKE '%şanlıurfa%' OR b."Sehir" ILIKE '%sanliurfa%' OR b."Sehir" ILIKE '%diyarbakır%' OR b."Sehir" ILIKE '%mardin%' OR b."Bolge" ILIKE '%güneydoğu%' OR b."Bolge" ILIKE '%guneydogu%' THEN 'DP06'
                ELSE 'DP01'
            END AS depo_kodu
        FROM bayiler b
    ),
    depo_kritik_stok AS (
        SELECT 
            "DepoKodu" AS depo_kodu,
            COUNT(*) AS kritik_urun_sayisi
        FROM vw_dinamik_stok_esigi
        WHERE "StokKritikMi" = TRUE
        GROUP BY "DepoKodu"
    ),
    aylik_satis AS (
        SELECT 
            s."BayiKodu" AS bayi_kodu,
            -- Net Ciro: İade olmayan satışların toplam tutarı
            SUM(CASE 
                WHEN (s."IsIade" IS NOT TRUE) AND s."Miktar" > 0 
                THEN s."Miktar" * s."BirimFiyat" * (1 - COALESCE(s."IskontoOrani", 0)) 
                ELSE 0 
            END) AS ciro,
            -- Ortalama Kâr Marjı (%)
            ROUND((AVG(CASE 
                WHEN (s."IsIade" IS NOT TRUE) AND s."Miktar" > 0 
                THEN (((s."BirimFiyat" * (1 - COALESCE(s."IskontoOrani", 0))) - u."BirimMaliyet") / NULLIF((s."BirimFiyat" * (1 - COALESCE(s."IskontoOrani", 0))), 0)) * 100 
                ELSE NULL 
            END))::numeric, 2) AS ortalama_kar_marji,
            -- Ortalama İskonto Oranı (%)
            ROUND((AVG(CASE 
                WHEN (s."IsIade" IS NOT TRUE) AND s."Miktar" > 0 
                THEN COALESCE(s."IskontoOrani", 0) * 100 
                ELSE NULL 
            END))::numeric, 2) AS ortalama_iskonto_orani,
            -- İade Oranı (%): IsIade = TRUE veya Miktar < 0 olanların normal satışlara oranı
            ROUND((COALESCE(
                SUM(CASE WHEN s."IsIade" = TRUE OR s."Miktar" < 0 THEN ABS(s."Miktar") ELSE 0 END) 
                / NULLIF(SUM(CASE WHEN (s."IsIade" IS NOT TRUE) AND s."Miktar" > 0 THEN s."Miktar" ELSE 0 END), 0) * 100, 0
            ))::numeric, 2) AS ortalama_iade_orani
        FROM satis s
        JOIN urunler u ON s."UrunKodu" = u."UrunKodu"
        WHERE EXTRACT(MONTH FROM s."Tarih"::date) = EXTRACT(MONTH FROM '{donem_str}'::date)
          AND EXTRACT(YEAR FROM s."Tarih"::date) = EXTRACT(YEAR FROM '{donem_str}'::date)
        GROUP BY s."BayiKodu"
    ),
    aylik_hedef AS (
        SELECT 
            h."BayiKodu" AS bayi_kodu,
            COALESCE(MAX(h."Hedef"), 0) AS hedef
        FROM hedefler h
        WHERE h."Yil" = EXTRACT(YEAR FROM '{donem_str}'::date)
          AND h."Ay" = EXTRACT(MONTH FROM '{donem_str}'::date)
        GROUP BY h."BayiKodu"
    )
    SELECT 
        b."BayiKodu" AS bayi_kodu,
        COALESCE(s.ciro, 0) AS ciro,
        COALESCE(h.hedef, 0) AS hedef,
        CASE 
            WHEN COALESCE(h.hedef, 0) > 0 THEN ROUND(((COALESCE(s.ciro, 0) / h.hedef) * 100)::numeric, 2)
            ELSE 100.0
        END AS hedef_tutma_orani,
        COALESCE(s.ortalama_kar_marji, 0) AS ortalama_kar_marji,
        COALESCE(s.ortalama_iskonto_orani, 0) AS ortalama_iskonto_orani,
        COALESCE(s.ortalama_iade_orani, 0) AS ortalama_iade_orani,
        COALESCE(dks.kritik_urun_sayisi, 0) AS kritik_stok_kalan_urun_sayisi
    FROM bayiler b
    LEFT JOIN aylik_satis s ON b."BayiKodu" = s.bayi_kodu
    LEFT JOIN aylik_hedef h ON b."BayiKodu" = h.bayi_kodu
    LEFT JOIN bayi_depo_esleme bde ON b."BayiKodu" = bde.bayi_kodu
    LEFT JOIN depo_kritik_stok dks ON bde.depo_kodu = dks.depo_kodu
    """
    
    df = hook.get_pandas_df(query)
    if df.empty:
        print(f"{donem_str} donemi icin islenecek gercek veri bulunamadi.")
        return
        
    df["donem"] = donem_str
    
    # 2. TRANSFORM: Deterministik Kural ve Risk Motoru
    def apply_business_rules(row):
        marj = float(row.get("ortalama_kar_marji") or 0)
        iskonto = float(row.get("ortalama_iskonto_orani") or 0)
        hedef_tutma = float(row.get("hedef_tutma_orani") or 0)
        iade = float(row.get("ortalama_iade_orani") or 0)
        kritik_stok = int(row.get("kritik_stok_kalan_urun_sayisi") or 0)
        
        gerekceler = []
        if marj < 15.0:
            gerekceler.append("MARJ_ESIK_ALTI")
        if iskonto > 15.0:
            gerekceler.append("YUKSEK_ISKONTO")
        if hedef_tutma < 80.0:
            gerekceler.append("HEDEF_ALTINDA")
        if iade > 10.0:
            gerekceler.append("YUKSEK_IADE")
        if kritik_stok > 0:
            gerekceler.append("STOK_YETERSIZ")
            
        row["gerekce_kodlari"] = gerekceler
        
        # Risk skoru hesaplama
        risk_skoru = sum(PENALTY_WEIGHTS.get(g, 0) for g in gerekceler)
        row["risk_skoru"] = risk_skoru
        
        # Risk seviyesi
        if risk_skoru <= 30:
            row["risk_seviyesi"] = "DUSUK"
        elif risk_skoru <= 60:
            row["risk_seviyesi"] = "ORTA"
        else:
            row["risk_seviyesi"] = "YUKSEK"
            
        return row

    df = df.apply(apply_business_rules, axis=1)
    
    # 3. LOAD: `bayi_donem_performans` tablosuna yazma (Aylık karne PK: bayi_kodu, donem)
    from psycopg2.extras import execute_values
    cursor = conn.cursor()
    insert_query = """
        INSERT INTO public.bayi_donem_performans (
            bayi_kodu, donem, ciro, hedef, hedef_tutma_orani,
            ortalama_kar_marji, ortalama_iskonto_orani, ortalama_iade_orani,
            kritik_stok_kalan_urun_sayisi, risk_skoru, risk_seviyesi, gerekce_kodlari
        ) VALUES %s
        ON CONFLICT (bayi_kodu, donem) 
        DO UPDATE SET
            ciro = EXCLUDED.ciro,
            hedef = EXCLUDED.hedef,
            hedef_tutma_orani = EXCLUDED.hedef_tutma_orani,
            ortalama_kar_marji = EXCLUDED.ortalama_kar_marji,
            ortalama_iskonto_orani = EXCLUDED.ortalama_iskonto_orani,
            ortalama_iade_orani = EXCLUDED.ortalama_iade_orani,
            kritik_stok_kalan_urun_sayisi = EXCLUDED.kritik_stok_kalan_urun_sayisi,
            risk_skoru = EXCLUDED.risk_skoru,
            risk_seviyesi = EXCLUDED.risk_seviyesi,
            gerekce_kodlari = EXCLUDED.gerekce_kodlari;
    """
    
    insert_data = []
    for _, row in df.iterrows():
        insert_data.append((
            row["bayi_kodu"],
            row["donem"],
            float(row["ciro"]) if pd.notnull(row["ciro"]) else 0,
            float(row["hedef"]) if pd.notnull(row["hedef"]) else 0,
            float(row["hedef_tutma_orani"]) if pd.notnull(row["hedef_tutma_orani"]) else 0,
            float(row["ortalama_kar_marji"]) if pd.notnull(row["ortalama_kar_marji"]) else 0,
            float(row["ortalama_iskonto_orani"]) if pd.notnull(row["ortalama_iskonto_orani"]) else 0,
            float(row["ortalama_iade_orani"]) if pd.notnull(row["ortalama_iade_orani"]) else 0,
            int(row["kritik_stok_kalan_urun_sayisi"]) if pd.notnull(row["kritik_stok_kalan_urun_sayisi"]) else 0,
            int(row["risk_skoru"]) if pd.notnull(row["risk_skoru"]) else 0,
            row["risk_seviyesi"],
            row["gerekce_kodlari"]
        ))
        
    execute_values(cursor, insert_query, insert_data, page_size=2000)
    conn.commit()
    
    cursor.close()
    conn.close()

    print(f"{len(df)} bayinin {donem_str} donemi karne verisi basariyla yuklendi.")


# ---------------- AIRFLOW DAG DEFINITION (AYLIK PERİYOT) ----------------

default_args = {
    'owner': 'data_engineer',
    'depends_on_past': False,
    'start_date': pendulum.datetime(2026, 1, 1, tz="Europe/Istanbul"),
    'retries': 1,
}

with DAG(
    dag_id='monthly_performance_etl_dag',
    default_args=default_args,
    schedule_interval='0 8 1 * *',  # Her ayın 1'inde saat 08:00 (Aylık periyot)
    catchup=False,
    description='Aylık bayi performans karnesini (IsIade ve kritik depo stok destekli) hesaplar ve yükler',
    tags=['etl', 'monthly', 'performance']
) as dag:

    task_extract_load = PythonOperator(
        task_id='extract_transform_load_task',
        python_callable=extract_transform_and_load,
        provide_context=True
    )
