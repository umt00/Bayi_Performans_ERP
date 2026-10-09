import os
import sys
import json
from dotenv import load_dotenv
from openai import OpenAI
import traceback

# Add project root to path to import app.ai.commenter_client if run from elsewhere
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..')))

from app.ai.commenter_client import (
    build_prompt,
    build_alert_prompt,
    validate_guardrails,
    evaluate_offer
)

# Airflow PostgresHook kullanımı (Eğer airflow yüklüyse ve connection çalışıyorsa)
has_airflow = True
try:
    from airflow.providers.postgres.hooks.postgres import PostgresHook
except ImportError:
    has_airflow = False
    print("Warning: airflow modülü bulunamadı. DB bağlantısı için psycopg2 kullanılacak.")
    try:
        import psycopg2
        import psycopg2.extras
    except ImportError:
        print("Error: psycopg2 kütüphanesi bulunamadı. Lütfen 'pip install psycopg2-binary' çalıştırın.")
        sys.exit(1)


def get_db_connection():
    """
    Airflow varsa PostgresHook kullanır, yoksa yerel .env dosyasındaki
    SUPABASE_DB_URL bilgisiyle psycopg2 kullanarak bağlanır.
    """
    if has_airflow:
        hook = PostgresHook(postgres_conn_id='supabase')
        return hook.get_conn()
    else:
        db_url = os.getenv("SUPABASE_DB_URL")
        if not db_url:
            print("Warning: SUPABASE_DB_URL ortam değişkeni bulunamadı. Yerel Supabase varsayılanına (127.0.0.1:54322) bağlanılıyor...")
            db_url = "postgresql://postgres:postgres@127.0.0.1:54322/postgres"
            
        # Use psycopg2.extras.DictCursor to return dict-like rows like Airflow does
        return psycopg2.connect(db_url, cursor_factory=psycopg2.extras.DictCursor)


def fetch_unprocessed_records(conn):
    """
    ai_comment alanı henüz doldurulmamış (NULL) olan bayi performans kayıtlarını getirir.
    """
    cursor = conn.cursor()
    query = """
        SELECT 
            bayi_kodu, donem, snapshot_tarihi,
            gerceklesen_ciro, hedef_ciro, hedefe_ulasma_pct,
            ortalama_marj_pct, marj_esigi_pct, ortalama_iskonto_pct,
            iade_orani_pct, kritik_stok_urun_sayisi, risk_skoru,
            risk_seviyesi, gerekce_kodlari, hesaplanan_karar
        FROM bayi_donem_performans
        WHERE ai_comment IS NULL
    """
    cursor.execute(query)
    
    # Sütun isimlerini al
    columns = [desc[0] for desc in cursor.description]
    
    # dict listesi olarak dönüştür
    records = []
    for row in cursor.fetchall():
        records.append(dict(zip(columns, row)))
        
    cursor.close()
    return records


def update_record_ai_outputs(conn, bayi_kodu, donem, snapshot_tarihi, ai_comment, alert_message_draft, is_fallback):
    """
    Supabase'de (PostgreSQL) ilgili kaydın AI çıktılarını günceller.
    """
    cursor = conn.cursor()
    query = """
        UPDATE bayi_donem_performans
        SET 
            ai_comment = %s,
            alert_message_draft = %s,
            is_fallback = %s
        WHERE 
            bayi_kodu = %s AND 
            donem = %s AND 
            snapshot_tarihi = %s
    """
    cursor.execute(query, (ai_comment, alert_message_draft, is_fallback, bayi_kodu, donem, snapshot_tarihi))
    conn.commit()
    cursor.close()


def prepare_llm_data(record):
    """
    Veritabanından gelen düz satırı LLM prompt build metodunun beklediği iç içe formata çevirir.
    """
    return {
        "bayi_kodu": record["bayi_kodu"],
        "metrikler": {
            "gerceklesen_ciro": record["gerceklesen_ciro"],
            "hedef_ciro": record["hedef_ciro"],
            "hedefe_ulasma_pct": record["hedefe_ulasma_pct"],
            "ortalama_marj_pct": record["ortalama_marj_pct"],
            "ortalama_iskonto_pct": record["ortalama_iskonto_pct"],
            "iade_orani_pct": record["iade_orani_pct"],
            "kritik_stok_urun_sayisi": record["kritik_stok_urun_sayisi"]
        },
        "esikler": {
            "marj_esigi_pct": record["marj_esigi_pct"]
        },
        "sistem_karari": {
            "risk_skoru": record["risk_skoru"],
            "gerekce_kodlari": record["gerekce_kodlari"] if record["gerekce_kodlari"] else [],
            "risk_seviyesi": record["risk_seviyesi"],
            "hesaplanan_karar": record["hesaplanan_karar"]
        }
    }


def process_single_record(record, api_key, base_url, model_name, conn):
    """
    Tek bir bayi kaydı için LLM süreçlerini işletir. Hata olursa fallback moduna geçer.
    """
    bayi_kodu = record["bayi_kodu"]
    donem = record["donem"]
    snapshot_tarihi = record["snapshot_tarihi"]
    
    print(f"\n{'='*40}")
    print(f"Executing LLM task for: Bayi {bayi_kodu} (Dönem: {donem})")
    
    try:
        data = prepare_llm_data(record)
        
        prompt = build_prompt(data)
        alert_prompt = build_alert_prompt(data)

        ai_comment = ""
        alert_message = ""
        is_fallback = False
        
        if api_key and api_key != "your_groq_api_key_here":
            try:
                client = OpenAI(api_key=api_key, base_url=base_url)
                
                # Call 1: Performance Comment
                resp1 = client.chat.completions.create(
                    model=model_name,
                    messages=[{"role": "user", "content": prompt}],
                    temperature=0.1,
                    max_tokens=300
                )
                ai_comment = resp1.choices[0].message.content
                
                # Call 2: Alert Message
                resp2 = client.chat.completions.create(
                    model=model_name,
                    messages=[{"role": "user", "content": alert_prompt}],
                    temperature=0.1,
                    max_tokens=200
                )
                alert_message = resp2.choices[0].message.content
                
            except Exception as e:
                print(f"LLM call failed for {bayi_kodu}: {e}")
                is_fallback = True
        else:
            print("Skipping LLM call due to missing API key. Using Fallback.")
            is_fallback = True

        # Guardrails
        if not is_fallback:
            if not validate_guardrails(ai_comment) or not validate_guardrails(alert_message):
                print("Guardrail violation detected! Using Fallback.")
                is_fallback = True

        # DTO and Fallback resolution
        evaluation = evaluate_offer(bayi_kodu, ai_comment, alert_message, is_fallback)
        
        # Save to DB
        update_record_ai_outputs(
            conn=conn,
            bayi_kodu=bayi_kodu,
            donem=donem,
            snapshot_tarihi=snapshot_tarihi,
            ai_comment=evaluation["ai_comment"],
            alert_message_draft=evaluation["alert_message_draft"],
            is_fallback=evaluation["is_fallback"]
        )
        print(f"Successfully processed and updated {bayi_kodu}.")
        
    except Exception as ex:
        print(f"Critical error processing record {bayi_kodu}: {ex}")
        traceback.print_exc()
        # Fallback ile en azından kaydı güvenli şekilde güncelleyip döngünün devam etmesini sağlayalım
        try:
            evaluation = evaluate_offer(bayi_kodu, "", "", True)
            update_record_ai_outputs(
                conn, bayi_kodu, donem, snapshot_tarihi,
                evaluation["ai_comment"], evaluation["alert_message_draft"], evaluation["is_fallback"]
            )
            print(f"Fallback values applied for {bayi_kodu} due to exception.")
        except Exception as inner_ex:
            print(f"Failed to even apply fallback for {bayi_kodu}: {inner_ex}")


def run_evaluation_flow():
    print("Step 1: Setup API Key")
    env_path = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".env"))
    load_dotenv(env_path)
    api_key = os.getenv("GROQ_API_KEY")
    base_url = os.getenv("GROQ_BASE_URL")
    model_name = os.getenv("GROQ_MODEL")
    
    if not api_key or api_key == "your_groq_api_key_here":
        print("Warning: GROQ_API_KEY is not set. LLM call will fail or use fallback.")

    print("Step 2: Connect to Supabase")
    try:
        conn = get_db_connection()
    except Exception as e:
        print(f"Veritabanı bağlantı hatası: {e}")
        return

    print("Step 3: Fetch Unprocessed Records (ai_comment IS NULL)")
    try:
        records = fetch_unprocessed_records(conn)
        print(f"Found {len(records)} unprocessed records.")
    except Exception as e:
        print(f"Veri çekilirken hata oluştu: {e}")
        conn.close()
        return

    print("Step 4: Process Records Idempotently")
    for record in records:
        # Her bir satır kendi hata yönetimine sahip
        process_single_record(record, api_key, base_url, model_name, conn)
        
    print("\nFlow completed successfully.")
    conn.close()


if __name__ == "__main__":
    run_evaluation_flow()
