import random
import requests
import pendulum
from airflow import DAG
from airflow.operators.python import PythonOperator
import uuid

# Her çalıştığında üretilecek teklif sayısı
MIN_OFFERS = 1
MAX_OFFERS = 3

# Kullanılacak mock veriler
BAYI_KODLARI = [f"B{str(i).zfill(3)}" for i in range(1, 21)]  # B001 to B020
URUN_KODLARI = ["U001", "U002", "U003", "U004", "U005", "U006", "U007", "U008"]
DEPO_KODLARI = ["D01", "D02", "D03"]

FASTAPI_URL = "http://host.docker.internal:8000/teklif-olustur"

def generate_and_send_offers(**context):
    offer_count = random.randint(MIN_OFFERS, MAX_OFFERS)
    print(f"{offer_count} adet teklif oluşturulacak ve doğrudan FastAPI'ye gönderilecek...")

    successful = 0
    failed = 0

    for i in range(offer_count):
        bayi = random.choice(BAYI_KODLARI)
        teklif_id = f"SIM-TEK-{uuid.uuid4().hex[:6].upper()}"
        
        kalem_sayisi = random.randint(1, 3)
        kalemler = []
        
        for sira in range(1, kalem_sayisi + 1):
            miktar = random.randint(1, 50)
            birim_fiyat = round(random.uniform(100.0, 5000.0), 2)
            iskonto_orani = round(random.uniform(0.0, 0.20), 2)
            
            kalemler.append({
                "sira_no": sira,
                "urun_kodu": random.choice(URUN_KODLARI),
                "urun_adi": f"Urun {sira}",
                "depo_kodu": random.choice(DEPO_KODLARI),
                "depo_adi": "Simulasyon Depo",
                "miktar": miktar,
                "birim_fiyat": birim_fiyat,
                "iskonto_orani": iskonto_orani
            })

        payload = {
            "teklif_id": teklif_id,
            "bayi_kodu": bayi,
            "bayi_adi": f"{bayi} Sirketi",
            "bayi_sehir": "Istanbul",
            "bayi_bolge": "Marmara",
            "aciklama": "Airflow DAG tarafindan API'ye atilan otomatik teklif",
            "kalemler": kalemler
        }
        
        try:
            response = requests.post(FASTAPI_URL, json=payload, timeout=10)
            if response.status_code == 202:
                successful += 1
            else:
                failed += 1
                print(f"API Hatası ({response.status_code}): {response.text}")
        except Exception as e:
            failed += 1
            print(f"Baglanti hatasi (FastAPI acik olmayabilir): {e}")

    print(f"Tamamlandi. Basarili: {successful}, Basarisiz: {failed}")


with DAG(
    dag_id='teklif_generator_dag',
    schedule_interval='0 */3 * * *', # Her 3 saatte bir çalışır (Günde 8 kere)
    start_date=pendulum.datetime(2023, 1, 1, tz="Europe/Istanbul"),
    catchup=False,
    tags=['simulasyon', 'teklif', 'api'],
) as dag:

    task_generate_offers = PythonOperator(
        task_id='generate_daily_offers',
        python_callable=generate_and_send_offers,
    )
