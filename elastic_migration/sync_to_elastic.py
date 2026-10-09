import os
import time
import psycopg2
import json
from psycopg2.extras import RealDictCursor
from elasticsearch import Elasticsearch, helpers
from dotenv import load_dotenv
from pathlib import Path

# --- YAPILANDIRMA (Configuration) ---
env_path = Path(__file__).resolve().parent.parent / ".env"
load_dotenv(dotenv_path=env_path)

DB_URL = os.getenv("SUPABASE_POSTGRES_URL") or os.getenv("SUPABASE_DB_URL")
TABLE_NAME = os.getenv("SUPABASE_TABLE", "satis")

ES_HOST = os.getenv("ES_HOST", "http://localhost:9200")
INDEX_NAME = "satislar_index"

CHUNK_SIZE = 1000 # Her defasında belleğe alınıp ES'e gönderilecek paket büyüklüğü

def get_es_client() -> Elasticsearch:
    return Elasticsearch([ES_HOST])

def fetch_data_from_postgres():
    """
    Supabase'e REST API yerine doğrudan PostgreSQL üzerinden bağlanarak 
    Server-Side Cursor (es_sync_cursor) ile verileri yığınlar halinde çeker. 
    Memory limitlerine takılmadan 55.000+ satırı ışık hızında işler.
    """
    if not DB_URL or "[BURAYA_SIFRE_GELECEK]" in DB_URL:
        raise ValueError(".env dosyasında geçerli bir SUPABASE_POSTGRES_URL bulunamadı!")

    conn = psycopg2.connect(DB_URL)
    # cursor_factory=RealDictCursor sayesinde gelen veriler JSON (Dictionary) formatında olur
    cur = conn.cursor(name="es_sync_cursor", cursor_factory=RealDictCursor)
    cur.itersize = CHUNK_SIZE
    query = """
    SELECT 
        s."SatisID",
        s."Tarih",
        s."BayiKodu",
        b."BayiAdi",
        b."Sehir" AS "BayiSehir",
        b."Bolge",
        b."Segment",
        s."UrunKodu",
        u."UrunAdi",
        u."Kategori",
        u."Marka",
        s."DepoKodu",
        d."DepoAdi",
        s."Miktar",
        s."BirimFiyat",
        s."IskontoOrani",
        s."SatisKanali",
        s."IsIade",
        (s."Miktar" * s."BirimFiyat" * (1 - COALESCE(s."IskontoOrani", 0))) AS "Ciro",
        ((s."BirimFiyat" * (1 - COALESCE(s."IskontoOrani", 0)) - u."BirimMaliyet") * s."Miktar") AS "BrutKar"
    FROM satis s
    LEFT JOIN bayiler b ON s."BayiKodu" = b."BayiKodu"
    LEFT JOIN urunler u ON s."UrunKodu" = u."UrunKodu"
    LEFT JOIN depolar d ON s."DepoKodu" = d."DepoKodu";
    """
    cur.execute(query)
    while True:
        data = cur.fetchmany(CHUNK_SIZE)
        if not data:
            break
        yield data
        
    cur.close()
    conn.close()

def generate_es_actions(data_chunk):
    """
    Gelen JSON formatındaki satırları ES Upsert (Update or Insert) mantığına çevirir.
    """
    for row in data_chunk:
        yield {
            "_index": INDEX_NAME,
            "_id": row.get("SatisID"),     # Kaydın ES üzerindeki eşsiz kimliği (Gerçek tabloya göre güncellendi)
            "_op_type": "update",
            "doc_as_upsert": True,
            "doc": dict(row)
        }

def create_index_if_not_exists(es: Elasticsearch):
    """
    Elasticsearch'te index yoksa, index_mapping.json dosyasını okuyarak
    custom mapping (Türkçe analyzer vb.) ayarlarıyla indexi kurar.
    Index zaten varsa silmez, mevcut indexi korur (UPSERT).
    """
    if es.indices.exists(index=INDEX_NAME):
        print(f"[{INDEX_NAME}] indexi zaten mevcut. Silinmeden devam ediliyor (UPSERT modu)...")
        return

    print(f"[{INDEX_NAME}] indexi bulunamadı. JSON tabanlı Mapping ayarlarıyla oluşturuluyor...")
    mapping_path = Path(__file__).resolve().parent / "index_mapping.json"
    with open(mapping_path, "r", encoding="utf-8") as f:
        mapping_data = json.load(f)
    
    # Elasticsearch 8.x uyumlu index oluşturma komutu
    es.indices.create(
        index=INDEX_NAME, 
        settings=mapping_data.get("settings"), 
        mappings=mapping_data.get("mappings")
    )
    print(f"[OK] [{INDEX_NAME}] basariyla olusturuldu ve mapping uygulandi.")

def run_etl_pipeline():
    print("ETL Pipeline (PostgreSQL -> Elasticsearch) Başlatılıyor...")
    start_time = time.time()
    
    es = get_es_client()
    if not es.ping():
        raise ConnectionError("Elasticsearch'e bağlanılamadı. Docker container'ını kontrol edin.")
        
    # Veri aktarmadan önce indexi mapping ayarlarıyla kur
    create_index_if_not_exists(es)
    
    total_docs_synced = 0
    
    for chunk in fetch_data_from_postgres():
        actions = generate_es_actions(chunk)
        
        success, failed = helpers.bulk(
            client=es, 
            actions=actions, 
            chunk_size=CHUNK_SIZE, 
            stats_only=True,
            raise_on_error=False
        )
        total_docs_synced += success
        print(f"[{total_docs_synced} / ~55000] adet kayıt Elasticsearch'e Upsert edildi...")
        
    end_time = time.time()
    print(f"ETL Süreci Tamamlandı! Toplam {total_docs_synced} kayıt aktarıldı. (Süre: {end_time - start_time:.2f} saniye)")

if __name__ == "__main__":
    run_etl_pipeline()
