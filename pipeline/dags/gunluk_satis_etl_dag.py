from datetime import datetime, timedelta
import pandas as pd
import sys
import os
from airflow import DAG
from airflow.operators.python import PythonOperator
from airflow.operators.trigger_dagrun import TriggerDagRunOperator
from airflow.providers.postgres.hooks.postgres import PostgresHook
from sqlalchemy import create_engine

default_args = {
    'owner': 'penta',
    'depends_on_past': False,
    'email_on_failure': False,
    'email_on_retry': False,
    'retries': 1,
    'retry_delay': timedelta(minutes=5),
}

def veri_temizleme_ve_aktarma():
    # Docker/yerel esneklik icin etl dizinini AIRFLOW_HOME üzerinden bul
    airflow_home = os.environ.get('AIRFLOW_HOME', '/opt/airflow')
    sys.path.append(os.path.join(airflow_home, 'etl'))
    try:
        from clean import tarih_temizle, metin_temizle, miktar_temizle, yuzde_temizle, fiyat_temizle
    except ImportError as e:
        print("HATA: clean.py bulunamadi.")
        raise e

    hook = PostgresHook(postgres_conn_id='supabase')
    engine_pg = hook.get_sqlalchemy_engine()
    
    # 1. Kirli Veriyi Azure SQL'den Oku (Extract)
    azure_url = os.environ.get('AZURE_SQL_URL') or "mssql+pymssql://CloudSAbffd0b7b@demodemosqldatabase:PenTa123.penta@demodemosqldatabase.database.windows.net/yeni_satislar?charset=utf8"
    if not azure_url:
        print("HATA: AZURE_SQL_URL bulunamadi!")
        return
        
    engine_azure = create_engine(azure_url)
    
    try:
        # Sadece bugun girilen verileri al
        query = """
        SELECT * FROM raw_streaming_satis 
        WHERE CAST(Sisteme_Giris_Zamani AS DATE) = CAST(GETDATE() AS DATE)
        """
        df = pd.read_sql(query, engine_azure)
    except Exception as e:
        print(f"Azure SQL okuma hatasi (Tablo henuz olusmamis olabilir): {e}")
        raise e
        
    if df.empty:
        print("Azure SQL'de islenecek yeni (bugune ait) veri bulunamadi.")
        return
        
    ilk_satir_sayisi = len(df)
    print(f"{ilk_satir_sayisi} adet kirli veri Azure SQL'den okundu. Temizlige baslaniyor...")
        
    # 2. Özel Kurallarla Temizle (Transform)
    df = tarih_temizle(df, 'Tarih')
    df = metin_temizle(df, 'BayiKodu')
    df = metin_temizle(df, 'UrunKodu')
    df = miktar_temizle(df, 'Miktar')
    df["IsIade"] = df["Miktar"] < 0
    df = yuzde_temizle(df, 'IskontoOrani')
    df = fiyat_temizle(df, 'BirimFiyat')
    df = metin_temizle(df, 'SatisKanali')
    
    # Backend filtering column'i at (Supabase'de lazim degil)
    if 'Sisteme_Giris_Zamani' in df.columns:
        df.drop(columns=['Sisteme_Giris_Zamani'], inplace=True)
        
    # Supabase'de ON CONFLICT hatasi (unique constraint) almamak icin
    # Ayni gun, ayni bayi tarafindan ayni urune yapilan satislari tekillestir
    df = df.groupby(['Tarih', 'BayiKodu', 'UrunKodu'], as_index=False).agg({
        'SatisID': 'first',
        'DepoKodu': 'first',
        'Miktar': 'sum',
        'BirimFiyat': 'mean',
        'IskontoOrani': 'mean',
        'SatisKanali': 'first',
        'IsIade': 'max'
    })
    
    temp_table_name = 'yeni_satis_temp'
    df.to_sql(temp_table_name, engine_pg, if_exists='replace', index=False)
    temizlenen_satir_sayisi = len(df)
    print("Veriler temizlendi ve gecici tabloya alindi.")
    
    # 3. Supabase Ana Tabloya Aktar (Load - UPSERT)
    conn = hook.get_conn()
    conn.autocommit = False 
    cursor = conn.cursor()
    
    try:
        upsert_query = f"""
        INSERT INTO satis ("SatisID", "Tarih", "BayiKodu", "UrunKodu", "DepoKodu", "Miktar", "BirimFiyat", "IskontoOrani", "SatisKanali", "IsIade")
        SELECT "SatisID", CAST("Tarih" AS DATE), "BayiKodu", "UrunKodu", "DepoKodu", "Miktar", "BirimFiyat", "IskontoOrani", "SatisKanali", "IsIade"
        FROM {temp_table_name}
        ON CONFLICT ("Tarih", "BayiKodu", "UrunKodu") 
        DO UPDATE SET 
            "Miktar" = EXCLUDED."Miktar",
            "BirimFiyat" = EXCLUDED."BirimFiyat",
            "IskontoOrani" = EXCLUDED."IskontoOrani",
            "SatisKanali" = EXCLUDED."SatisKanali",
            "IsIade" = EXCLUDED."IsIade",
            "SatisID" = EXCLUDED."SatisID",
            "DepoKodu" = EXCLUDED."DepoKodu";
        """
        cursor.execute(upsert_query)
        print(f"UPSERT islemi basarili: {len(df)} satir ana tabloya eklendi/guncellendi.")
        
        # cursor.execute(f"DROP TABLE {temp_table_name};")  # Kullanıcı kontrolü için tablo tutulacak
        conn.commit()
        print("TRANSACTION COMMIT EDILDI (Gecici tablo korundu).")
        
        # Raporu Text Dosyasina Yaz
        log_dir = os.path.join(airflow_home, 'logs')
        os.makedirs(log_dir, exist_ok=True)
        report_path = os.path.join(log_dir, 'etl_temizlik_raporu.txt')
        rapor_metni = f"[{datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] ETL Raporu: Azure'dan {ilk_satir_sayisi} satir okundu. Temizleme sonrasi Supabase'e (UPSERT) {temizlenen_satir_sayisi} satir basariyla aktarildi.\n"
        with open(report_path, "a", encoding="utf-8") as f:
            f.write(rapor_metni)
        print("ETL Raporu log dosyasina kaydedildi.")
        
    except Exception as e:
        conn.rollback()
        print(f"KRITIK HATA! Islem geri alindi (ROLLBACK). Hata detayi: {e}")
        raise e
    finally:
        cursor.close()
        conn.close()

with DAG(
    dag_id='gunluk_satis_etl_dag',
    default_args=default_args,
    description='Azure SQL den kirli veriyi cekip temizleyerek Supabase e aktarir',
    schedule_interval='30 10 * * *',
    start_date=datetime(2026, 8, 15),
    catchup=True,
    tags=['ETL', 'AzureSQL', 'ArtimliYukleme']
) as dag:

    etl_gorevi = PythonOperator(
        task_id='veri_temizleme_ve_aktarma',
        python_callable=veri_temizleme_ve_aktarma
    )

    # Supabase ana tabloya yazım bittikten sonra Elasticsearch aktarımını tetikle
    trigger_elastic_sync = TriggerDagRunOperator(
        task_id='trigger_elastic_sync',
        trigger_dag_id='sync_elastic_dag',
        wait_for_completion=False # Tetikleyip bırakır, ES aktarımını beklemez (Decoupled yapı)
    )

    etl_gorevi >> trigger_elastic_sync
