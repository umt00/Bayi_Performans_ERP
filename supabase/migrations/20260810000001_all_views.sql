-- ==============================================================================
-- RULES ENGINE DESTEKLİ VERİTABANI GÖRÜNÜMLERİ (VIEWS)
-- ==============================================================================

-- 1. Satış Kârlılık ve Risk Analizi
-- Rules Engine: Marj ve İskonto risk hesaplamaları için
CREATE OR REPLACE VIEW vw_satis_karlilik_ve_risk AS
SELECT 
    s."SatisID", 
    s."Tarih"::date AS "Tarih", 
    s."BayiKodu", 
    s."UrunKodu", 
    s."Miktar", 
    s."BirimFiyat", 
    COALESCE(s."IskontoOrani", 0) AS "IskontoOrani", 
    u."BirimMaliyet",
    (s."BirimFiyat" * (1 - COALESCE(s."IskontoOrani", 0))) AS "NetBirimFiyat",
    (s."Miktar" * s."BirimFiyat" * (1 - COALESCE(s."IskontoOrani", 0))) AS "NetTutar",
    ((s."BirimFiyat" * (1 - COALESCE(s."IskontoOrani", 0))) - u."BirimMaliyet") AS "BirimKar_TL",
    ROUND(((((s."BirimFiyat" * (1 - COALESCE(s."IskontoOrani", 0))) - u."BirimMaliyet") 
         / NULLIF((s."BirimFiyat" * (1 - COALESCE(s."IskontoOrani", 0))), 0)) * 100)::numeric, 2) AS "Marj_Yuzdesi"
FROM "satis" s
JOIN "urunler" u ON s."UrunKodu" = u."UrunKodu";

-- 2. Ürün İade Oranları
-- Rules Engine: Yüksek iade oranlı (riskli) ürünleri tespit etmek için
CREATE OR REPLACE VIEW vw_urun_iade_oranlari AS
SELECT 
    s."UrunKodu",
    u."UrunAdi",
    SUM(CASE WHEN s."Miktar" > 0 THEN s."Miktar" ELSE 0 END) AS "ToplamSatis",
    ABS(SUM(CASE WHEN s."Miktar" < 0 THEN s."Miktar" ELSE 0 END)) AS "ToplamIade",
    ROUND((ABS(SUM(CASE WHEN s."Miktar" < 0 THEN s."Miktar" ELSE 0 END)) / NULLIF(SUM(CASE WHEN s."Miktar" > 0 THEN s."Miktar" ELSE 0 END), 0) * 100)::numeric, 2) AS "IadeOrani_Yuzdesi"
FROM "satis" s
JOIN "urunler" u ON s."UrunKodu" = u."UrunKodu"
GROUP BY s."UrunKodu", u."UrunAdi";

-- 3. Bayi Hedef Durumu
-- Rules Engine: Bayinin aylık hedef tutturma performansını (HEDEF_ALTINDA riskini) ölçmek için
CREATE OR REPLACE VIEW vw_bayi_hedef_durumu AS
SELECT 
    s."BayiKodu", 
    b."BayiAdi", 
    EXTRACT(YEAR FROM s."Tarih"::date) AS "Yil", 
    EXTRACT(MONTH FROM s."Tarih"::date) AS "Ay",
    SUM(s."Miktar" * s."BirimFiyat" * (1 - COALESCE(s."IskontoOrani", 0))) AS "GerceklesenCiro",
    COALESCE(MAX(h."Hedef"), 0) AS "AylikHedef",
    ROUND((SUM(s."Miktar" * s."BirimFiyat" * (1 - COALESCE(s."IskontoOrani", 0))) / NULLIF(MAX(h."Hedef"), 0) * 100)::numeric, 2) AS "HedefTutturmaOrani_Yuzdesi"
FROM "satis" s
JOIN "bayiler" b ON s."BayiKodu" = b."BayiKodu"
LEFT JOIN "hedefler" h 
    ON s."BayiKodu" = h."BayiKodu" 
   AND EXTRACT(YEAR FROM s."Tarih"::date) = h."Yil" 
   AND EXTRACT(MONTH FROM s."Tarih"::date) = h."Ay"
GROUP BY s."BayiKodu", b."BayiAdi", EXTRACT(YEAR FROM s."Tarih"::date), EXTRACT(MONTH FROM s."Tarih"::date);

-- 4. Dinamik Stok Eşiği
-- Rules Engine: Son 90 günün günlük ortalaması üzerinden 7 günlük (stok_gun_esigi) eşik değeri belirlemek için
CREATE OR REPLACE VIEW vw_dinamik_stok_esigi AS
WITH son_90_gun_satis AS (
    SELECT 
        s."UrunKodu",
        SUM(s."Miktar") AS "Son90GunToplamSatis"
    FROM "satis" s
    WHERE s."Tarih"::date >= (SELECT MAX("Tarih"::date) FROM "satis") - INTERVAL '90 days'
    GROUP BY s."UrunKodu"
),
stok_toplam AS (
    SELECT 
        "UrunKodu",
        SUM("MevcutStok") AS "ToplamMevcutStok"
    FROM "stok"
    GROUP BY "UrunKodu"
)
SELECT 
    u."UrunKodu",
    u."UrunAdi",
    COALESCE(s90."Son90GunToplamSatis", 0) AS "Son90GunToplamSatis",
    ROUND((COALESCE(s90."Son90GunToplamSatis", 0) / 90.0)::numeric, 2) AS "GunlukOrtalamaSatis",
    ROUND(GREATEST((COALESCE(s90."Son90GunToplamSatis", 0) / 90.0) * 7, 1)::numeric, 2) AS "DinamikStokEsigi",
    COALESCE(st."ToplamMevcutStok", 0) AS "MevcutStok",
    CASE WHEN COALESCE(st."ToplamMevcutStok", 0) < GREATEST((COALESCE(s90."Son90GunToplamSatis", 0) / 90.0) * 7, 1) THEN TRUE ELSE FALSE END AS "StokYetersizMi"
FROM "urunler" u
LEFT JOIN son_90_gun_satis s90 ON u."UrunKodu" = s90."UrunKodu"
LEFT JOIN stok_toplam st ON u."UrunKodu" = st."UrunKodu";