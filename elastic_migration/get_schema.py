import psycopg2
import os
from dotenv import load_dotenv
from pathlib import Path

env_path = Path(__file__).resolve().parent.parent / ".env"
load_dotenv(dotenv_path=env_path)

db_url = os.getenv("SUPABASE_POSTGRES_URL") or os.getenv("SUPABASE_DB_URL")

try:
    conn = psycopg2.connect(db_url)
    cur = conn.cursor()
    query = """
    SELECT column_name, data_type 
    FROM information_schema.columns 
    WHERE table_name = 'satis';
    """
    cur.execute(query)
    columns = cur.fetchall()
    
    print("\n--- 'satis' TABLOSU KOLONLARI ---")
    if not columns:
        print("Tablo bulunamadı veya kolon yok.")
    else:
        for col in columns:
            print(f"- {col[0]} : {col[1]}")
            
    cur.close()
    conn.close()
except Exception as e:
    print(f"Error: {e}")
