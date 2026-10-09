import os
import psycopg2
from dotenv import load_dotenv
from pathlib import Path

# Load env variables
env_path = Path(__file__).resolve().parent.parent / ".env"
load_dotenv(dotenv_path=env_path)
DB_URL = os.getenv("SUPABASE_POSTGRES_URL") or os.getenv("SUPABASE_DB_URL")

def run_cleansing():
    print("Veri temizliği başlatılıyor...")
    conn = psycopg2.connect(DB_URL)
    cur = conn.cursor()

    # Find duplicates
    cur.execute("""
        SELECT "BayiAdi", array_agg("BayiKodu") 
        FROM bayiler 
        GROUP BY "BayiAdi" 
        HAVING COUNT(DISTINCT "BayiKodu") > 1;
    """)
    duplicates = cur.fetchall()

    if not duplicates:
        print("Mükerrer bayi bulunamadı. İşlem sonlandırılıyor.")
        cur.close()
        conn.close()
        return

    print(f"Toplam {len(duplicates)} adet mükerrer isimli bayi tespit edildi.")
    
    total_sales_updated = 0
    total_dealers_deleted = 0

    for dup in duplicates:
        bayi_adi = dup[0]
        kodlar = dup[1]
        
        # Determine primary code (e.g., first one alphabetically)
        kodlar.sort()
        primary_code = kodlar[0]
        duplicate_codes = kodlar[1:]
        
        print(f"'{bayi_adi}' için Ana Kod: {primary_code}, Kopyalar: {duplicate_codes}")

        for dup_code in duplicate_codes:
            # 1. Çakışan (aynı gün aynı ürün) faturaları miktar toplayarak birleştir ve kopya faturayı sil
            cur.execute("""
                WITH conflicts AS (
                    SELECT s2."SatisID" as dup_satis_id, s1."SatisID" as primary_satis_id, s2."Miktar" as dup_miktar
                    FROM satis s1
                    JOIN satis s2 ON s1."Tarih" = s2."Tarih" AND s1."UrunKodu" = s2."UrunKodu"
                    WHERE s1."BayiKodu" = %s AND s2."BayiKodu" = %s
                ),
                update_primary AS (
                    UPDATE satis s1
                    SET "Miktar" = s1."Miktar" + conflicts.dup_miktar
                    FROM conflicts
                    WHERE s1."SatisID" = conflicts.primary_satis_id
                )
                DELETE FROM satis s2
                USING conflicts
                WHERE s2."SatisID" = conflicts.dup_satis_id;
            """, (primary_code, dup_code))
            merged_rows = cur.rowcount

            # 2. Çakışmayan geriye kalan tüm satışların sadece Bayi Kodunu güncelle
            cur.execute("""
                UPDATE satis 
                SET "BayiKodu" = %s 
                WHERE "BayiKodu" = %s;
            """, (primary_code, dup_code))
            updated_rows = cur.rowcount
            total_sales_updated += (updated_rows + merged_rows)
            print(f"   -> {dup_code} kodlu satışlar aktarıldı: {updated_rows} güncellendi, {merged_rows} çakışan fatura birleştirildi.")

            # 3. Eski kopya bayiyi tamamen sil
            cur.execute("""
                DELETE FROM bayiler 
                WHERE "BayiKodu" = %s;
            """, (dup_code,))
            total_dealers_deleted += cur.rowcount
            print(f"   -> Kopya kod {dup_code} bayiler tablosundan silindi.")

    # Commit the transaction
    conn.commit()
    print("\nTemizlik tamamlandı!")
    print(f"Güncellenen toplam satış kaydı (Fatura): {total_sales_updated}")
    print(f"Silinen kopya bayi kaydı: {total_dealers_deleted}")

    cur.close()
    conn.close()

if __name__ == "__main__":
    run_cleansing()
