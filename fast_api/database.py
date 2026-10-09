"""
Veritabani baglanti katmani.
"""
import os                          
from pathlib import Path           
from dotenv import load_dotenv     
from sqlalchemy import create_engine  

# Proje kök dizinindeki .env dosyasını oku
BASE_DIR = Path(__file__).resolve().parent.parent
load_dotenv(BASE_DIR / ".env")

_engine = None

def get_database_url() -> str:
    url = os.environ.get("SUPABASE_DB_URL") or os.environ.get("DATABASE_URL")
    if not url:
        return ""
    url = url.strip().strip('"').strip("'")
    if url.startswith("postgres://"):
        url = "postgresql://" + url[len("postgres://"):]
    return url

def engine_al():
    global _engine
    if _engine is None:
        db_url = get_database_url()
        if not db_url:
            raise RuntimeError(
                "DATABASE_URL veya SUPABASE_DB_URL bulunamadi. .env dosyasini kontrol edin."
            )
        _engine = create_engine(
            db_url,
            pool_pre_ping=True,
            pool_size=10,
            max_overflow=20,
            pool_recycle=300,
        )
    return _engine
