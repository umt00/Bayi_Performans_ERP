import pandas as pd
import numpy as np
import re
import unicodedata
from pathlib import Path

# ==========================================
# 1. TEMİZLİK FONKSİYONLARI (HELPER FUNCTIONS)
# ==========================================

def tarih_temizle(df: pd.DataFrame, kolon: str) -> pd.DataFrame:
    df = df.copy()
    
    # Bozuk encoding ihtimaline karsi regex kaliplari (Örn: A.*ustos)
    aylar_regex = {
        r"ocak": "January", r"şubat|subat": "February", r"mart": "March", r"nisan": "April",
        r"mayıs|mayis": "May", r"haziran": "June", r"temmuz": "July", 
        r"a.*ustos|a.*u": "August",  # Ağustos encoding bozulmalarını yakalar
        r"eylül|eylul": "September", r"ekim": "October", 
        r"kasım|kasim": "November", r"aralık|aralik": "December"
    }
    
    tarih = df[kolon].astype(str).str.lower().str.strip()
    for tr_ay, en_ay in aylar_regex.items():
        tarih = tarih.str.replace(tr_ay, en_ay, regex=True)
    
    df[kolon] = pd.to_datetime(tarih, format="mixed", dayfirst=True, errors="coerce")
    
    # Eger NaT olan varsa bugunun tarihini at (NULL engelleme)
    if df[kolon].isna().any():
        print(f"Uyari: {df[kolon].isna().sum()} adet parse edilemeyen tarih bulundu. Bugunun tarihi atandi.")
        df[kolon] = df[kolon].fillna(pd.to_datetime("today"))
        
    df[kolon] = df[kolon].dt.strftime("%Y-%m-%d")
    return df

def fiyat_temizle(df: pd.DataFrame, kolon: str) -> pd.DataFrame:
    df = df.copy()
    temp = df[kolon].astype(str).str.replace("TL", "", regex=False).str.replace("₺", "", regex=False).str.strip()
    temp = temp.str.replace(r"\.(?=\d{3}(?:,|$))", "", regex=True)
    temp = temp.str.replace(",", ".", regex=False)
    
    df[kolon] = pd.to_numeric(temp, errors="coerce")
    df.loc[df[kolon] < 0, kolon] = np.nan
    df[kolon] = df[kolon].fillna(df[kolon].median())
    return df

def miktar_temizle(df: pd.DataFrame, kolon: str) -> pd.DataFrame:
    df = df.copy()
    temp = df[kolon].astype(str).str.strip()
    temp = temp.str.replace(" ", "", regex=False).str.replace(".", "", regex=False).str.replace(",", ".", regex=False)
    
    df[kolon] = pd.to_numeric(temp, errors="coerce")
    df = df.dropna(subset=[kolon])
    return df

def yuzde_temizle(df: pd.DataFrame, kolon: str) -> pd.DataFrame:
    df = df.copy()
    temp = df[kolon].astype(str).str.replace("%", "", regex=False).str.strip()
    temp = temp.str.replace(",", ".", regex=False)
    
    degerler = pd.to_numeric(temp, errors="coerce")
    degerler = np.where(degerler > 1, degerler / 100.0, degerler)
    
    df[kolon] = degerler
    df.loc[(df[kolon] < 0) | (df[kolon] > 1), kolon] = np.nan
    df[kolon] = df[kolon].fillna(df[kolon].median())
    return df

def metin_temizle(df: pd.DataFrame, kolon: str) -> pd.DataFrame:
    df = df.copy()
    def turkce_title(metin: str) -> str:
        if not metin or pd.isna(metin):
            return ""
        metin = re.sub(r"\s+", " ", str(metin)).strip()
        kelimeler = metin.split(" ")
        temiz_kelimeler = []
        for kelime in kelimeler:
            if not kelime:
                continue
            ilk_harf = kelime[0]
            if ilk_harf == "i": ilk_harf = "İ"
            elif ilk_harf == "ı": ilk_harf = "I"
            else: ilk_harf = ilk_harf.upper()

            kalan = kelime[1:].lower()
            temiz_kelimeler.append(ilk_harf + kalan)
        return " ".join(temiz_kelimeler)

    df[kolon] = df[kolon].astype(str).apply(turkce_title)
    return df

def hedefleri_dikey_formata_cevir(hedef_df: pd.DataFrame) -> pd.DataFrame:
    aylar = ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"]
    hedef_uzun = hedef_df.melt(
        id_vars=["BayiKodu", "Yil", "Aciklama"], 
        value_vars=aylar, 
        var_name="Ay_Adi", 
        value_name="Hedef"
    )
    ay_map = {ay: i + 1 for i, ay in enumerate(aylar)}
    hedef_uzun["Ay"] = hedef_uzun["Ay_Adi"].map(ay_map)
    return hedef_uzun

def sehir_isimlerini_duzelt(df: pd.DataFrame, kolon: str) -> pd.DataFrame:
    df = df.copy()
    df[kolon] = df[kolon].astype(str).apply(lambda x: unicodedata.normalize("NFKC", x)).str.strip()
    duzeltmeler = {
        "Mersi̇n": "Mersin", "Izmir": "İzmir", "Istanbul": "İstanbul", "Di̇yarbakir": "Diyarbakır",
        "Kocaeli̇": "Kocaeli", "Eski̇şehi̇r": "Eskişehir", "İzmi̇r": "İzmir", "Kayseri̇": "Kayseri",
        "Gazi̇antep": "Gaziantep", "Diyarbakir": "Diyarbakır"
    }
    df[kolon] = df[kolon].replace(duzeltmeler)
    return df

# ==========================================
# 2. ANA TEMİZLİK SÜRECİ (DATA EXECUTION)
# ==========================================

def run_cleaning():
    print("1. Ham veriler okunuyor...")
    
    # Kendi bulunduğu dosyanın tam yolunu dinamik olarak bul
    BASE_DIR = Path(__file__).resolve().parent
    DATA_DIR = (BASE_DIR / ".." / "data").resolve()
    RAW_DIR = DATA_DIR / "raw"
    PROCESSED_DIR = DATA_DIR / "processed"

    # Dinamik dosya yolları ile okuma
    satis = pd.read_excel(RAW_DIR / 'Satislar.xlsx')
    urunler = pd.read_excel(RAW_DIR / 'Urunler.xlsx')
    stok = pd.read_excel(RAW_DIR / 'Stok.xlsx')
    depolar = pd.read_excel(RAW_DIR / 'Depolar.xlsx')
    bayiler = pd.read_excel(RAW_DIR / 'Bayiler.xlsx')
    hedefler = pd.read_excel(RAW_DIR / 'AylikHedefler.xlsx')

    print("2. Veriler temizleniyor...")
    
    # --- ÜRÜNLER TEMİZLİĞİ ---
    urunler = metin_temizle(urunler, 'UrunKodu')
    urunler = urunler.drop_duplicates(subset=["UrunKodu"], keep="first")
    urunler = metin_temizle(urunler, 'UrunAdi')
    urunler = metin_temizle(urunler, 'Kategori')
    urunler = metin_temizle(urunler, 'AltKategori')
    urunler["BirimMaliyet"] = urunler["BirimMaliyet"].astype(str).str.replace("TL", "", regex=False).str.replace("₺", "", regex=False).str.strip().str.replace(".", "", regex=False).str.replace(",", ".", regex=False)
    urunler["BirimMaliyet"] = pd.to_numeric(urunler["BirimMaliyet"], errors="coerce")
    urunler["BirimMaliyet"] = urunler["BirimMaliyet"].fillna(urunler.groupby("AltKategori")["BirimMaliyet"].transform("median"))

    # --- BAYİLER TEMİZLİĞİ ---
    bayiler = metin_temizle(bayiler, 'BayiKodu')
    bayiler = bayiler.drop_duplicates(subset=["BayiKodu"], keep="first")
    bayiler = metin_temizle(bayiler, 'Sehir')
    bayiler = sehir_isimlerini_duzelt(bayiler, 'Sehir')
    bayiler = metin_temizle(bayiler, 'BayiAdi')
    bayiler['Segment'] = bayiler['Segment'].replace(r"^\s*$", pd.NA, regex=True).fillna("Bilinmiyor")
    bayiler = metin_temizle(bayiler, 'Segment')
    bayiler["Segment"] = bayiler["Segment"].replace({"Kobi̇": "KOBİ", "Kobi": "KOBİ"})

    # --- DEPOLAR TEMİZLİĞİ ---
    depolar = metin_temizle(depolar, 'DepoAdi')
    depolar["DepoAdi"] = depolar["DepoAdi"].replace({"Ana Depo (hadımköy)": "Ana Depo (Hadımköy)"})
    depolar = metin_temizle(depolar, 'Sehir')
    depolar = metin_temizle(depolar, 'Bolge')

    # --- SATIŞLAR TEMİZLİĞİ ---
    satis = tarih_temizle(satis, 'Tarih')
    satis = metin_temizle(satis, 'BayiKodu')
    satis = satis[satis["BayiKodu"].isin(bayiler["BayiKodu"])]
    satis = metin_temizle(satis, 'UrunKodu')
    satis = satis[satis["UrunKodu"].isin(urunler["UrunKodu"])]
    satis = satis.dropna(subset=["Miktar"]).copy()
    satis = miktar_temizle(satis, "Miktar")
    satis["IsIade"] = satis["Miktar"] < 0
    satis = yuzde_temizle(satis, 'IskontoOrani')
    satis = fiyat_temizle(satis, 'BirimFiyat')
    satis = metin_temizle(satis, 'SatisKanali')

    # --- STOK TEMİZLİĞİ ---
    stok = metin_temizle(stok, 'UrunKodu')
    stok = stok[stok["UrunKodu"].isin(urunler["UrunKodu"]) & stok["DepoKodu"].isin(depolar["DepoKodu"])]
    stok = miktar_temizle(stok, 'MevcutStok')
    stok = miktar_temizle(stok, "MinStokSeviyesi")
    stok["MinStokSeviyesi"] = stok["MinStokSeviyesi"].fillna(20)
    stok = tarih_temizle(stok, 'SonSayimTarihi')

    # --- HEDEFLER TEMİZLİĞİ ---
    hedefler["BayiKodu"] = hedefler["BayiKodu"].astype(str).str.strip().str.upper()
    hedefler_long = hedefleri_dikey_formata_cevir(hedefler)

    print("3. Temizlenmiş veriler CSV olarak kaydediliyor...")
    PROCESSED_DIR.mkdir(parents=True, exist_ok=True)
    
    # separator olarak virgül (,) kullanıyoruz
    satis.to_csv(PROCESSED_DIR / "temiz_satis.csv", index=False, encoding="utf-8")
    urunler.to_csv(PROCESSED_DIR / "temiz_urunler.csv", index=False, encoding="utf-8")
    bayiler.to_csv(PROCESSED_DIR / "temiz_bayiler.csv", index=False, encoding="utf-8")
    depolar.to_csv(PROCESSED_DIR / "temiz_depolar.csv", index=False, encoding="utf-8")
    stok.to_csv(PROCESSED_DIR / "temiz_stok.csv", index=False, encoding="utf-8")
    hedefler_long.to_csv(PROCESSED_DIR / "temiz_hedefler.csv", index=False, encoding="utf-8")

    print("✅ Temizlik işlemi başarıyla tamamlandı ve CSV dosyaları oluşturuldu!")

if __name__ == "__main__":
    run_cleaning()
