-- ==========================================
-- 1. MEVCUT TABLO VE GÖRÜNÜMLERİ SİLME (CLEANUP)
-- ==========================================
-- CASCADE kullanarak bu tablolara bağlı olan view'ları ve ilişikleri (foreign keys) temizliyoruz.

DROP TABLE IF EXISTS satis CASCADE;
DROP TABLE IF EXISTS stok CASCADE;
DROP TABLE IF EXISTS hedefler CASCADE;
DROP TABLE IF EXISTS urunler CASCADE;
DROP TABLE IF EXISTS depolar CASCADE;
DROP TABLE IF EXISTS bayiler CASCADE;

-- ==========================================
-- 2. TABLO OLUŞTURMA (DDL)
-- ==========================================

-- Urunler Tablosu
CREATE TABLE urunler (
    "UrunKodu" VARCHAR(50) PRIMARY KEY,
    "UrunAdi" VARCHAR(255) NOT NULL,
    "Kategori" VARCHAR(100),
    "AltKategori" VARCHAR(100),
    "Marka" VARCHAR(100),
    "BirimMaliyet" NUMERIC(15,2)
);

-- Bayiler Tablosu
CREATE TABLE bayiler (
    "BayiKodu" VARCHAR(50) PRIMARY KEY,
    "BayiAdi" VARCHAR(255) NOT NULL,
    "Sehir" VARCHAR(100),
    "Bolge" VARCHAR(100),
    "Segment" VARCHAR(50)
);

-- Depolar Tablosu
CREATE TABLE depolar (
    "DepoKodu" VARCHAR(50) PRIMARY KEY,
    "DepoAdi" VARCHAR(255) NOT NULL,
    "Sehir" VARCHAR(100),
    "Bolge" VARCHAR(100)
);

-- Stok Tablosu
CREATE TABLE stok (
    "UrunKodu" VARCHAR(50) NOT NULL,
    "DepoKodu" VARCHAR(50) NOT NULL,
    "MevcutStok" NUMERIC(15,2) DEFAULT 0,
    "MinStokSeviyesi" NUMERIC(15,2) DEFAULT 20,
    "SonSayimTarihi" DATE,
    PRIMARY KEY ("UrunKodu", "DepoKodu"),
    CONSTRAINT fk_stok_urun FOREIGN KEY ("UrunKodu") REFERENCES urunler("UrunKodu") ON DELETE CASCADE,
    CONSTRAINT fk_stok_depo FOREIGN KEY ("DepoKodu") REFERENCES depolar("DepoKodu") ON DELETE CASCADE
);

-- Hedefler Tablosu (Long format)
CREATE TABLE hedefler (
    "BayiKodu" VARCHAR(50) NOT NULL,
    "Yil" INT NOT NULL,
    "Ay" INT NOT NULL,
    "Ay_Adi" VARCHAR(20),
    "Aciklama" TEXT,
    "Hedef" NUMERIC(15,2) DEFAULT 0,
    PRIMARY KEY ("BayiKodu", "Yil", "Ay"),
    CONSTRAINT fk_hedefler_bayi FOREIGN KEY ("BayiKodu") REFERENCES bayiler("BayiKodu") ON DELETE CASCADE
);

-- Satislar Tablosu
CREATE TABLE satis (
    "SatisID" VARCHAR(100) PRIMARY KEY,
    "Tarih" DATE NOT NULL,
    "BayiKodu" VARCHAR(50) NOT NULL,
    "UrunKodu" VARCHAR(50) NOT NULL,
    "DepoKodu" VARCHAR(50),
    "Miktar" NUMERIC(15,2) NOT NULL,
    "BirimFiyat" NUMERIC(15,2) NOT NULL,
    "IskontoOrani" NUMERIC(5,4) DEFAULT 0,
    "SatisKanali" VARCHAR(100),
    "IsIade" BOOLEAN DEFAULT FALSE,
    CONSTRAINT fk_satis_bayi FOREIGN KEY ("BayiKodu") REFERENCES bayiler("BayiKodu") ON DELETE CASCADE,
    CONSTRAINT fk_satis_urun FOREIGN KEY ("UrunKodu") REFERENCES urunler("UrunKodu") ON DELETE CASCADE,
    CONSTRAINT fk_satis_depo FOREIGN KEY ("DepoKodu") REFERENCES depolar("DepoKodu") ON DELETE CASCADE
);

-- ==========================================
-- 3. VİEWLAR (ANALİZ GÖRÜNÜMLERİ)
-- ==========================================

-- 1. Satış ve Kârlılık Analizi
CREATE OR REPLACE VIEW vw_satis_analiz AS
SELECT 
    s."SatisID",
    s."Tarih",
    s."BayiKodu",
    b."BayiAdi",
    b."Segment",
    b."Sehir",
    s."UrunKodu",
    u."UrunAdi",
    u."Kategori",
    s."Miktar",
    s."BirimFiyat",
    COALESCE(s."IskontoOrani", 0) AS "IskontoOrani",
    u."BirimMaliyet",
    (s."Miktar" * s."BirimFiyat" * (1 - COALESCE(s."IskontoOrani", 0))) AS "NetTutar",
    (s."Miktar" * u."BirimMaliyet") AS "ToplamMaliyet",
    ((s."Miktar" * s."BirimFiyat" * (1 - COALESCE(s."IskontoOrani", 0))) - (s."Miktar" * u."BirimMaliyet")) AS "Marj_TL",
    CASE 
        WHEN (s."Miktar" * s."BirimFiyat" * (1 - COALESCE(s."IskontoOrani", 0))) = 0 THEN 0
        ELSE ROUND(((((s."Miktar" * s."BirimFiyat" * (1 - COALESCE(s."IskontoOrani", 0))) - (s."Miktar" * u."BirimMaliyet")) / 
             (s."Miktar" * s."BirimFiyat" * (1 - COALESCE(s."IskontoOrani", 0)))) * 100)::NUMERIC, 2)
    END AS "Marj_Yuzdesi"
FROM satis s
LEFT JOIN urunler u ON s."UrunKodu" = u."UrunKodu"
LEFT JOIN bayiler b ON s."BayiKodu" = b."BayiKodu";

-- 2. Hedef ve Performans Takibi
CREATE OR REPLACE VIEW vw_bayi_hedef_performans AS
WITH aylik_ciro AS (
    SELECT 
        "BayiKodu",
        EXTRACT(YEAR FROM "Tarih") AS "Yil",
        EXTRACT(MONTH FROM "Tarih") AS "Ay",
        SUM("Miktar" * "BirimFiyat" * (1 - COALESCE("IskontoOrani", 0))) AS "GerceklesenCiro"
    FROM satis
    GROUP BY "BayiKodu", EXTRACT(YEAR FROM "Tarih"), EXTRACT(MONTH FROM "Tarih")
)
SELECT 
    c."BayiKodu",
    b."BayiAdi",
    b."Segment",
    c."Yil",
    c."Ay",
    c."GerceklesenCiro",
    COALESCE(h."Hedef", 0) AS "Hedef",
    CASE 
        WHEN COALESCE(h."Hedef", 0) = 0 THEN 0
        ELSE ROUND(((c."GerceklesenCiro" / h."Hedef") * 100)::NUMERIC, 2)
    END AS "HedefGerceklesmeYuzdesi",
    CASE
        WHEN COALESCE(h."Hedef", 0) = 0 THEN 'Hedef Yok'
        WHEN (c."GerceklesenCiro" / h."Hedef") >= 1 THEN 'Hedef Aşıldı'
        WHEN (c."GerceklesenCiro" / h."Hedef") >= 0.8 THEN 'Kılpayı Kaçtı'
        ELSE 'Geride Kaldı'
    END AS "PerformansDurumu"
FROM aylik_ciro c
LEFT JOIN hedefler h ON c."BayiKodu" = h."BayiKodu" AND c."Yil" = h."Yil" AND c."Ay" = h."Ay"
LEFT JOIN bayiler b ON c."BayiKodu" = b."BayiKodu";

-- 3. Stok Kontrol ve Alarm
CREATE OR REPLACE VIEW vw_stok_durum_analiz AS
SELECT 
    s."UrunKodu",
    u."UrunAdi",
    s."DepoKodu",
    d."DepoAdi",
    s."MevcutStok",
    s."MinStokSeviyesi",
    GREATEST(0, s."MinStokSeviyesi" - s."MevcutStok") AS "StokAcigi",
    (s."MevcutStok" <= s."MinStokSeviyesi") AS "KritikStokMu",
    CASE 
        WHEN s."MevcutStok" = 0 THEN 'Tükendi (Out of Stock)'
        WHEN s."MevcutStok" <= s."MinStokSeviyesi" THEN 'Kritik Seviye'
        ELSE 'Stok Yeterli'
    END AS "StokDurumEtiketi"
FROM stok s
LEFT JOIN urunler u ON s."UrunKodu" = u."UrunKodu"
LEFT JOIN depolar d ON s."DepoKodu" = d."DepoKodu";

-- 4. Kategori ve Bölge Analizi
CREATE OR REPLACE VIEW vw_kategori_bolge AS
SELECT 
    b."Sehir",
    b."Segment",
    u."Kategori",
    SUM(s."Miktar") AS "ToplamSatisAdedi",
    SUM(s."Miktar" * s."BirimFiyat" * (1 - COALESCE(s."IskontoOrani", 0))) AS "NetCiro"
FROM satis s
LEFT JOIN bayiler b ON s."BayiKodu" = b."BayiKodu"
LEFT JOIN urunler u ON s."UrunKodu" = u."UrunKodu"
GROUP BY b."Sehir", b."Segment", u."Kategori";

-- 5. Riskli İşlem Tespit
CREATE OR REPLACE VIEW vw_riskli_islem AS
SELECT 
    s."SatisID",
    s."Tarih",
    s."BayiKodu",
    u."UrunAdi",
    COALESCE(s."IskontoOrani", 0) AS "IskontoOrani",
    ((s."Miktar" * s."BirimFiyat" * (1 - COALESCE(s."IskontoOrani", 0))) - (s."Miktar" * u."BirimMaliyet")) AS "Marj_TL"
FROM satis s
LEFT JOIN urunler u ON s."UrunKodu" = u."UrunKodu"
WHERE 
    ((s."Miktar" * s."BirimFiyat" * (1 - COALESCE(s."IskontoOrani", 0))) - (s."Miktar" * u."BirimMaliyet")) < 0 
    OR COALESCE(s."IskontoOrani", 0) > 0.40;

-- 6. Yapay Zeka (AI) Maskelenmiş Özet
CREATE OR REPLACE VIEW vw_ai_ozet AS
SELECT 
    s."BayiKodu",
    b."Segment",
    COUNT(s."SatisID") AS "IslemSayisi",
    SUM(s."Miktar" * s."BirimFiyat" * (1 - COALESCE(s."IskontoOrani", 0))) AS "ToplamCiro"
FROM satis s
LEFT JOIN bayiler b ON s."BayiKodu" = b."BayiKodu"
GROUP BY s."BayiKodu", b."Segment";

-- ==========================================
-- 4. PERFORMANS İNDEKSLERİ (INDEXES)
-- ==========================================
CREATE INDEX IF NOT EXISTS ix_satis_tarih ON satis ("Tarih");
CREATE INDEX IF NOT EXISTS ix_satis_bayikodu ON satis ("BayiKodu");
CREATE INDEX IF NOT EXISTS ix_satis_urunkodu ON satis ("UrunKodu");
CREATE INDEX IF NOT EXISTS ix_satis_depokodu ON satis ("DepoKodu");
CREATE INDEX IF NOT EXISTS ix_stok_urun_depo ON stok ("UrunKodu", "DepoKodu");
CREATE INDEX IF NOT EXISTS ix_hedefler_bayi_donem ON hedefler ("BayiKodu", "Yil", "Ay");

