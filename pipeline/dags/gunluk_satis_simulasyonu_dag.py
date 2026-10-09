from datetime import datetime, timedelta
import pandas as pd
import numpy as np
import uuid
import os
import random
from airflow import DAG
from airflow.operators.python import PythonOperator
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

def sahte_satis_uret():
    # 1. Supabase'den gecmis verileri cekip dagilimi ogren (Urun popülaritesi, Bayi agirligi)
    hook = PostgresHook(postgres_conn_id='supabase')
    engine_pg = hook.get_sqlalchemy_engine()
    
    print("Gecmis veriler analiz ediliyor...")
    try:
        # Tüm geçmişi çekip analiz etmek yerine istatistiksel ağırlıkları çıkarıyoruz
        df_hist = pd.read_sql("SELECT \"BayiKodu\", \"UrunKodu\", \"DepoKodu\", \"SatisKanali\" FROM satis", engine_pg)
        
        if df_hist.empty:
            raise ValueError("Tarihsel satis verisi bulunamadi!")
            
        bayi_weights = df_hist['BayiKodu'].value_counts(normalize=True).to_dict()
        urun_weights = df_hist['UrunKodu'].value_counts(normalize=True).to_dict()
        depo_weights = df_hist['DepoKodu'].value_counts(normalize=True).to_dict()
        kanal_weights = df_hist['SatisKanali'].value_counts(normalize=True).to_dict()
    except Exception as e:
        print(f"Uyari: Gecmis veriler okunamadi. Varsayilan listeler kullanilacak. Hata: {e}")
        bayi_weights = {'B001': 0.3, 'B002': 0.3, 'B003': 0.4}
        urun_weights = {'U001': 0.2, 'U002': 0.2, 'U003': 0.6}
        depo_weights = {'D01': 0.5, 'D02': 0.5}
        kanal_weights = {'Mağaza': 0.4, 'Online': 0.4, 'B2B': 0.2}

    # 2. Günlük hedef hacmi 3-4 bin arası belirle (Yeni Kurallar)
    gunluk_hedef = random.randint(3000, 4000)
    print(f"Bugunku simule edilecek satis hacmi: {gunluk_hedef} adet")

    # 3. Verileri Ağırlıklara Göre Üret
    bayiler = list(bayi_weights.keys())
    bayi_probs = list(bayi_weights.values())
    urunler = list(urun_weights.keys())
    urun_probs = list(urun_weights.values())
    depolar = list(depo_weights.keys())
    depo_probs = list(depo_weights.values())
    kanallar = list(kanal_weights.keys())
    kanal_probs = list(kanal_weights.values())

    today = datetime.now()
    
    # Tarih formatlarini cesitlendir (Kirlilik 1)
    aylar_tr = {1: "Ocak", 2: "Şubat", 3: "Mart", 4: "Nisan", 5: "Mayıs", 6: "Haziran", 7: "Temmuz", 8: "Ağustos", 9: "Eylül", 10: "Ekim", 11: "Kasım", 12: "Aralık"}
    date_formats = [
        f" {today.day} {aylar_tr[today.month]} {today.year} ",
        f"{today.day}/{today.month}/{today.year}",
        f"{today.day}.{today.month}.{today.year}",
        f"{today.year}-{today.month:02d}-{today.day:02d}", # Clean format
        f"{today.day}-{aylar_tr[today.month][:3]}-{today.year}"
    ]

    # Son SatisID'yi al
    try:
        max_id_df = pd.read_sql("SELECT MAX(CAST(NULLIF(regexp_replace(\"SatisID\", '\\D', '', 'g'), '') AS BIGINT)) FROM satis", engine_pg)
        last_id = max_id_df.iloc[0, 0] if not pd.isna(max_id_df.iloc[0, 0]) else 100000
    except Exception as e:
        print(f"Uyari: SatisID alinamadi, 100000 den baslanacak. Hata: {e}")
        last_id = 100000

    mock_data = {
        'SatisID': [str(int(last_id) + i) for i in range(1, gunluk_hedef + 1)],
        'Tarih': random.choices(date_formats, weights=[0.2, 0.2, 0.2, 0.3, 0.1], k=gunluk_hedef),
        'BayiKodu': random.choices(bayiler, weights=bayi_probs, k=gunluk_hedef),
        'UrunKodu': random.choices(urunler, weights=urun_probs, k=gunluk_hedef),
        'DepoKodu': random.choices(depolar, weights=depo_probs, k=gunluk_hedef),
        'Miktar': [max(1, int(random.gauss(50, 30))) for _ in range(gunluk_hedef)], # Doğal miktar dağılımı
        'BirimFiyat': [round(random.uniform(100, 50000), 2) for _ in range(gunluk_hedef)],
        'IskontoOrani': [round(random.uniform(0, 0.3), 2) for _ in range(gunluk_hedef)],
        'SatisKanali': random.choices(kanallar, weights=kanal_probs, k=gunluk_hedef)
    }

    df_mock = pd.DataFrame(mock_data)

    # 4. Kirlilik / Anomali Enjeksiyonu (%45 Oranında)
    num_anomalies = int(gunluk_hedef * 0.45)
    anomaly_indices = random.sample(range(gunluk_hedef), num_anomalies)
    
    for idx in anomaly_indices:
        kirlilik_tipi = random.choice(['miktar_bosluk', 'miktar_harf', 'fiyat_tl', 'kanal_yazim', 'iskonto_yuzde'])
        
        if kirlilik_tipi == 'miktar_bosluk':
            # "1 250" gibi binlik ayracına boşluk koyma hatası
            val = str(df_mock.at[idx, 'Miktar'])
            if len(val) > 2: df_mock.at[idx, 'Miktar'] = val[:-2] + " " + val[-2:]
            
        elif kirlilik_tipi == 'miktar_harf':
            # "15 adet" veya " 50"
            df_mock.at[idx, 'Miktar'] = f" {df_mock.at[idx, 'Miktar']} adet "
            
        elif kirlilik_tipi == 'fiyat_tl':
            # Fiyat sonuna TL koyma veya virgülle ondalık ayırma
            val = str(df_mock.at[idx, 'BirimFiyat']).replace('.', ',')
            df_mock.at[idx, 'BirimFiyat'] = f"{val} TL"
            
        elif kirlilik_tipi == 'kanal_yazim':
            # "online" yerine "OnLine " vs.
            val = str(df_mock.at[idx, 'SatisKanali'])
            df_mock.at[idx, 'SatisKanali'] = val.upper() if random.random() > 0.5 else val.lower() + " "
            
        elif kirlilik_tipi == 'iskonto_yuzde':
            # 0.15 yerine "%15" yazılması
            val = df_mock.at[idx, 'IskontoOrani']
            df_mock.at[idx, 'IskontoOrani'] = f"%{int(val*100)}"
            
    # Azure SQL deki filtreleme icin gercek zaman damgasi (Backend logic)
    df_mock['Sisteme_Giris_Zamani'] = pd.to_datetime('today')

    # 5. Azure SQL'e Yaz (Append)
    azure_url = os.environ.get('AZURE_SQL_URL') or "mssql+pymssql://CloudSAbffd0b7b@demodemosqldatabase:PenTa123.penta@demodemosqldatabase.database.windows.net/yeni_satislar?charset=utf8"
    if not azure_url:
        print("HATA: AZURE_SQL_URL bulunamadi!")
        return
        
    print(f"Azure SQL'e baglaniliyor ve {len(df_mock)} satir veri gonderiliyor...")
    engine_azure = create_engine(azure_url)
    
    try:
        # Pymssql driver'inin karma tipleri (sayi + metin) baglarken cokmemesi icin veriyi string'e cevir
        for col in df_mock.columns:
            if col != 'Sisteme_Giris_Zamani':
                df_mock[col] = df_mock[col].astype(str)
                
        # raw_streaming_satis tablosuna ekle
        # Veriler string'e cevrildigi icin artik method='multi' kullanabiliriz (hizli yazma)
        # SQL Server parametre limitini asmamak icin chunksize=100 ayarlandi.
        df_mock.to_sql('raw_streaming_satis', engine_azure, if_exists='append', index=False, chunksize=100, method='multi')
        print("Azure SQL aktarimi basariyla tamamlandi!")
    except Exception as e:
        print(f"Azure SQL yazma hatasi: {e}")
        raise e

with DAG(
    dag_id='gunluk_satis_simulasyonu_dag',
    default_args=default_args,
    description='Azure SQL e kirli satis verisi uretir (Catchup Acik)',
    schedule_interval='0 10 * * *',
    start_date=datetime(2026, 8, 15),
    catchup=True,
    tags=['Simulasyon', 'SahteVeri', 'AzureSQL']
) as dag:

    sahte_veri_uretici_gorevi = PythonOperator(
        task_id='sahte_satis_uret',
        python_callable=sahte_satis_uret
    )
