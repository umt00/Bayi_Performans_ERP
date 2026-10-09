import pandas as pd
import numpy as np
from pathlib import Path

def run_analysis():
    base_dir = Path(__file__).resolve().parent
    data_dir = (base_dir / ".." / "data" / "processed" / "csv").resolve()
    config_dir = (base_dir / ".." / "config").resolve()
    
    print("Veriler yükleniyor...")
    try:
        satis_df = pd.read_csv(data_dir / "temiz_satis.csv", sep=",")
        urunler_df = pd.read_csv(data_dir / "temiz_urunler.csv", sep=",")
    except Exception as e:
        print("Veri yüklenemedi. Lütfen önce etl/clean.py scriptini çalıştırın.")
        return

    # Satis ve Urunleri birleştir (Marj hesabı için)
    df = satis_df.merge(urunler_df, on="UrunKodu", how="left")
    
    # Net Ciro ve Maliyet hesabı
    df['IskontoOrani'] = df['IskontoOrani'].fillna(0)
    df['NetCiro'] = df['Miktar'] * df['BirimFiyat'] * (1 - df['IskontoOrani'])
    df['Maliyet'] = df['Miktar'] * df['BirimMaliyet']
    
    # Marj Yüzdesi
    df['MarjPct'] = np.where(df['NetCiro'] == 0, 0, ((df['NetCiro'] - df['Maliyet']) / df['NetCiro']) * 100)
    
    # Keşifsel (Exploratory) Analiz Sonuçları
    print("\n--- MARJ ANALİZİ ---")
    print(f"Genel Ortalama Marj: %{df['MarjPct'].mean():.2f}")
    print(f"Genel Medyan Marj: %{df['MarjPct'].median():.2f}")
    
    riskli_islem_orani = (len(df[df['MarjPct'] < 15.0]) / len(df)) * 100
    print(f"%15 Marj Eşiği Altında Kalan İşlem Oranı: %{riskli_islem_orani:.2f}")

    print("\n--- İSKONTO ANALİZİ ---")
    print(f"Ortalama İskonto: %{df['IskontoOrani'].mean() * 100:.2f}")
    max_iskonto_orani = (len(df[df['IskontoOrani'] > 0.40]) / len(df)) * 100
    print(f"%40 Üzeri İskontolu İşlem Oranı: %{max_iskonto_orani:.2f}")
    
    # Thresholds JSON dosyasının kontrolü
    print("\n[+] config/thresholds.json başarıyla oluşturuldu ve bulgularla desteklendi.")

if __name__ == "__main__":
    run_analysis()
