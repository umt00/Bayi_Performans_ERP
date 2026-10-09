import os
from pathlib import Path
import pandas as pd
import numpy as np
from sqlalchemy import create_engine
from sqlalchemy.exc import SQLAlchemyError

def load_environment_custom() -> str:
    """Windows BOM karakteri engeline takılmayan özel .env okuyucu."""
    base_dir = Path(__file__).resolve().parent
    root_dir = (base_dir / "..").resolve()
    dotenv_path = root_dir / ".env"

    if not dotenv_path.exists():
        raise FileNotFoundError(f"[HATA] .env dosyası bulunamadı! Aranılan yol: {dotenv_path}")

    # "utf-8-sig" ile Windows'un eklediği gizli karakterleri temizleyerek okuyoruz
    with open(dotenv_path, "r", encoding="utf-8-sig") as file:
        for line in file:
            line = line.strip()
            if line and not line.startswith("#") and "=" in line:
                key, value = line.split("=", 1)
                # Value içindeki varsa baştaki ve sondaki tırnakları temizle
                clean_value = value.strip().strip('"').strip("'")
                os.environ[key.strip()] = clean_value

    db_uri = os.getenv("SUPABASE_POSTGRES_URL")

    if not db_uri:
        raise ValueError("[HATA] .env okundu fakat 'SUPABASE_POSTGRES_URL' değişkeni bulunamadı!")

    return db_uri

def run_to_sql() -> None:
    # 1. Bağlantı Linkini Çek ve Engine Oluştur
    db_uri = load_environment_custom()
    engine = create_engine(db_uri)

    # 2. CSV Dosyalarının Bulunduğu Tam Klasör Yolu: data/processed/csv
    base_dir = Path(__file__).resolve().parent
    processed_dir = (base_dir / ".." / "data" / "processed" / "csv").resolve()

    # 3. Dosya -> Tablo Adı Eşleştirmesi
    file_table_mapping = {
        processed_dir / "temiz_urunler.csv": "urunler",
        processed_dir / "temiz_bayiler.csv": "bayiler",
        processed_dir / "temiz_depolar.csv": "depolar",
        processed_dir / "temiz_stok.csv": "stok",
        processed_dir / "temiz_satis.csv": "satis",
        processed_dir / "temiz_hedefler.csv": "hedefler",
    }

    # 4. Veri Yükleme Döngüsü
    print("[+] Supabase Veri Aktarım Süreci Başlatılıyor...\n")

    for csv_path, table_name in file_table_mapping.items():
        try:
            print(f"[*] İşleniyor: {csv_path.name} -> '{table_name}' tablosuna aktarılıyor...")

            if not csv_path.exists():
                print(f"[UYARI] Hata: '{csv_path.name}' bulunamadı. Bu dosya atlanıyor.\n")
                continue

            # CSV'yi Oku - etl/clean.py dosyası csv'yi ',' (virgül) ile kaydediyor.
            df = pd.read_csv(csv_path, sep=",")

            # Pandas NaN değerlerini SQL NULL'a çevir
            df.replace({np.nan: None}, inplace=True)

            # Supabase Postgres'e Yaz
            # if_exists='append' kullanarak mevcut şemayı ve constraint'leri bozmuyoruz
            df.to_sql(
                name=table_name,
                con=engine,
                if_exists="append",
                index=False,
                chunksize=1000,
                method="multi",
            )
            print(f"[OK] Başarılı: {len(df)} satır '{table_name}' tablosuna yüklendi.\n")

        except SQLAlchemyError as e:
            print(f"[HATA] Veritabanı Hatası ({table_name}): {e}\n")
        except Exception as e:
            print(f"[HATA] Beklenmeyen bir hata oluştu ({csv_path.name}): {e}\n")

    print("[+] Tüm tabloların aktarım işlemi tamamlandı!")

if __name__ == "__main__":
    run_to_sql()
