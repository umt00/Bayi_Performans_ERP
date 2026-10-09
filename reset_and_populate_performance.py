import os
import psycopg2
from psycopg2.extras import execute_values
import pandas as pd
from dotenv import load_dotenv

load_dotenv()

DB_URL = os.getenv("SUPABASE_DB_URL")

def clean_tables():
    print("=== 1. TABLOLAR TEMİZLENİYOR ===")
    conn = psycopg2.connect(DB_URL)
    cur = conn.cursor()
    
    tables_to_clean = [
        "teklif_ai_yorumlari",
        "teklif_degerlendirme",
        "teklif_kalemleri",
        "teklifler",
        "bayi_donem_performans"
    ]
    
    for t in tables_to_clean:
        cur.execute(f"TRUNCATE TABLE public.{t} CASCADE;")
        print(f"  [TRUNCATED] {t}")
        
    conn.commit()
    cur.close()
    conn.close()
    print("Tüm operasyonel ve analitik tablolar başarıyla temizlendi.\n")

def check_sales_dates_and_run_etl():
    print("=== 2. TÜM DÖNEMLER VE TÜM BAYİLER İÇİN ETL ÇALIŞTIRMA ===")
    conn = psycopg2.connect(DB_URL)
    cur = conn.cursor()
    
    cur.execute('SELECT MIN("Tarih"::date), MAX("Tarih"::date), COUNT(*) FROM satis;')
    min_date, max_date, total_sales = cur.fetchone()
    print(f"Satış Tablosu: Min Tarih = {min_date}, Max Tarih = {max_date}, Toplam Satır = {total_sales}")
    
    cur.execute('SELECT COUNT(*) FROM bayiler;')
    total_bayi = cur.fetchone()[0]
    print(f"Toplam Bayi Sayısı: {total_bayi}")

    # Tüm dönemleri ve tüm bayileri içeren kapsamlı SQL sorgusu
    query = """
    WITH distinct_donemler AS (
        SELECT DISTINCT TO_CHAR("Tarih"::date, 'YYYY-MM-01')::date AS donem
        FROM satis
    ),
    bayi_depo_esleme AS (
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
    bayi_donem_grid AS (
        SELECT b."BayiKodu" AS bayi_kodu, d.donem
        FROM bayiler b
        CROSS JOIN distinct_donemler d
    ),
    donemsel_satis AS (
        SELECT 
            s."BayiKodu" AS bayi_kodu,
            TO_CHAR(s."Tarih"::date, 'YYYY-MM-01')::date AS donem,
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
            -- İade Oranı (%)
            ROUND((COALESCE(
                SUM(CASE WHEN s."IsIade" = TRUE OR s."Miktar" < 0 THEN ABS(s."Miktar") ELSE 0 END) 
                / NULLIF(SUM(CASE WHEN (s."IsIade" IS NOT TRUE) AND s."Miktar" > 0 THEN s."Miktar" ELSE 0 END), 0) * 100, 0
            ))::numeric, 2) AS ortalama_iade_orani
        FROM satis s
        JOIN urunler u ON s."UrunKodu" = u."UrunKodu"
        GROUP BY s."BayiKodu", TO_CHAR(s."Tarih"::date, 'YYYY-MM-01')::date
    ),
    donemsel_hedef AS (
        SELECT 
            h."BayiKodu" AS bayi_kodu,
            TO_DATE(CONCAT(h."Yil"::text, '-', LPAD(h."Ay"::text, 2, '0'), '-01'), 'YYYY-MM-DD') AS donem,
            COALESCE(MAX(h."Hedef"), 0) AS hedef
        FROM hedefler h
        GROUP BY h."BayiKodu", h."Yil", h."Ay"
    )
    SELECT 
        grid.bayi_kodu,
        grid.donem,
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
    FROM bayi_donem_grid grid
    LEFT JOIN donemsel_satis s ON grid.bayi_kodu = s.bayi_kodu AND grid.donem = s.donem
    LEFT JOIN donemsel_hedef h ON grid.bayi_kodu = h.bayi_kodu AND grid.donem = h.donem
    LEFT JOIN bayi_depo_esleme bde ON grid.bayi_kodu = bde.bayi_kodu
    LEFT JOIN depo_kritik_stok dks ON bde.depo_kodu = dks.depo_kodu
    ORDER BY grid.donem DESC, grid.bayi_kodu ASC;
    """
    
    print("Metrikler veritabanından hesaplanıyor...")
    df = pd.read_sql(query, conn)
    print(f"Toplam hesaplanan bayi x dönem satır sayısı: {len(df)} (Dönem sayısı: {df['donem'].nunique()}, Bayi sayısı: {df['bayi_kodu'].nunique()})")
    
    PENALTY_WEIGHTS = {
        "MARJ_ESIK_ALTI": 40,
        "HEDEF_ALTINDA": 30,
        "YUKSEK_ISKONTO": 20,
        "STOK_YETERSIZ": 20,
        "YUKSEK_IADE": 20
    }
    
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
        risk_skoru = sum(PENALTY_WEIGHTS.get(g, 0) for g in gerekceler)
        row["risk_skoru"] = risk_skoru
        
        if risk_skoru <= 30:
            row["risk_seviyesi"] = "DUSUK"
        elif risk_skoru <= 60:
            row["risk_seviyesi"] = "ORTA"
        else:
            row["risk_seviyesi"] = "YUKSEK"
            
        return row

    print("İş kuralları ve risk motoru işletiliyor...")
    df = df.apply(apply_business_rules, axis=1)

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
        donem_val = row["donem"].strftime("%Y-%m-01") if hasattr(row["donem"], "strftime") else str(row["donem"])
        insert_data.append((
            row["bayi_kodu"],
            donem_val,
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
        
    print(f"{len(insert_data)} adet kayıt veritabanına toplu aktarılıyor...")
    execute_values(cur, insert_query, insert_data, page_size=2000)
    conn.commit()
    
    cur.execute("SELECT count(*), count(distinct donem), count(distinct bayi_kodu) FROM public.bayi_donem_performans;")
    cnt, donem_cnt, bayi_cnt = cur.fetchone()
    print(f"ETL Başarıyla Tamamlandı! bayi_donem_performans tablosunda toplam {cnt} satır (Dönem Sayısı: {donem_cnt}, Bayi Sayısı: {bayi_cnt}) oluşturuldu.")
    
    cur.close()
    conn.close()

if __name__ == "__main__":
    clean_tables()
    check_sales_dates_and_run_etl()
