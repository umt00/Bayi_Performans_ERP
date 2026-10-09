-- ==============================================================================
-- 20260825000001_refactor_schema_and_tables.sql
-- MİMARİ VE VERİTABANI YENİLEME MİGRASYONU (TAM, GÜVENLİ VE İDEMPOTENT YAPI)
-- ==============================================================================

BEGIN;

-- 1. BAYİ DÖNEM PERFORMANS TABLOSU (AYLIK KARNE YAPISI)
CREATE TABLE IF NOT EXISTS public.bayi_donem_performans (
    bayi_kodu text NOT NULL,
    donem date NOT NULL,
    ciro numeric(18,2) DEFAULT 0,
    hedef numeric(18,2) DEFAULT 0,
    hedef_tutma_orani numeric(9,2) DEFAULT 0,
    ortalama_kar_marji numeric(9,2) DEFAULT 0,
    ortalama_iskonto_orani numeric(9,2) DEFAULT 0,
    ortalama_iade_orani numeric(9,2) DEFAULT 0,
    kritik_stok_kalan_urun_sayisi integer DEFAULT 0,
    risk_skoru integer DEFAULT 0,
    risk_seviyesi text DEFAULT 'DUSUK' CHECK (risk_seviyesi IN ('DUSUK', 'ORTA', 'YUKSEK')),
    gerekce_kodlari text[] DEFAULT '{}',
    PRIMARY KEY (bayi_kodu, donem)
);

-- Kolonların mevcut tabloda eksiksiz olmasını sağla
ALTER TABLE public.bayi_donem_performans ADD COLUMN IF NOT EXISTS ciro numeric(18,2) DEFAULT 0;
ALTER TABLE public.bayi_donem_performans ADD COLUMN IF NOT EXISTS hedef numeric(18,2) DEFAULT 0;
ALTER TABLE public.bayi_donem_performans ADD COLUMN IF NOT EXISTS hedef_tutma_orani numeric(9,2) DEFAULT 0;
ALTER TABLE public.bayi_donem_performans ADD COLUMN IF NOT EXISTS ortalama_kar_marji numeric(9,2) DEFAULT 0;
ALTER TABLE public.bayi_donem_performans ADD COLUMN IF NOT EXISTS ortalama_iskonto_orani numeric(9,2) DEFAULT 0;
ALTER TABLE public.bayi_donem_performans ADD COLUMN IF NOT EXISTS ortalama_iade_orani numeric(9,2) DEFAULT 0;
ALTER TABLE public.bayi_donem_performans ADD COLUMN IF NOT EXISTS kritik_stok_kalan_urun_sayisi integer DEFAULT 0;
ALTER TABLE public.bayi_donem_performans ADD COLUMN IF NOT EXISTS risk_skoru integer DEFAULT 0;
ALTER TABLE public.bayi_donem_performans ADD COLUMN IF NOT EXISTS risk_seviyesi text DEFAULT 'DUSUK';
ALTER TABLE public.bayi_donem_performans ADD COLUMN IF NOT EXISTS gerekce_kodlari text[] DEFAULT '{}';


-- 2. TEKLİFLER (HEADER TABLOSU)
CREATE TABLE IF NOT EXISTS public.teklifler (
    teklif_id varchar(40) PRIMARY KEY,
    bayi_kodu varchar(100) NOT NULL,
    durum varchar(30) NOT NULL DEFAULT 'BEKLIYOR',
    olusturulma_tarihi timestamp with time zone DEFAULT now(),
    toplam_net_tutar numeric(18,2) NOT NULL DEFAULT 0,
    aciklama text
);

-- Eski kolon adı varsa güvenli geçiş sağla
DO $$
BEGIN
    IF EXISTS (
        SELECT FROM information_schema.columns 
        WHERE table_schema = 'public' AND table_name = 'teklifler' AND column_name = 'olusturulma_tarihi_utc'
    ) AND NOT EXISTS (
        SELECT FROM information_schema.columns 
        WHERE table_schema = 'public' AND table_name = 'teklifler' AND column_name = 'olusturulma_tarihi'
    ) THEN
        ALTER TABLE public.teklifler RENAME COLUMN olusturulma_tarihi_utc TO olusturulma_tarihi;
    END IF;
END $$;

ALTER TABLE public.teklifler ADD COLUMN IF NOT EXISTS durum varchar(30) NOT NULL DEFAULT 'BEKLIYOR';
ALTER TABLE public.teklifler ADD COLUMN IF NOT EXISTS olusturulma_tarihi timestamp with time zone DEFAULT now();
ALTER TABLE public.teklifler ADD COLUMN IF NOT EXISTS toplam_net_tutar numeric(18,2) NOT NULL DEFAULT 0;
ALTER TABLE public.teklifler ADD COLUMN IF NOT EXISTS aciklama text;


-- 3. TEKLİF KALEMLERİ (LINES TABLOSU)
CREATE TABLE IF NOT EXISTS public.teklif_kalemleri (
    teklif_id varchar(40) NOT NULL,
    sira_no integer NOT NULL,
    urun_kodu varchar(100) NOT NULL,
    depo_kodu varchar(100) NOT NULL DEFAULT 'DP01',
    miktar numeric(18,4) NOT NULL CHECK (miktar > 0),
    birim_fiyat numeric(18,4) NOT NULL CHECK (birim_fiyat >= 0),
    iskonto_orani numeric(9,6) NOT NULL DEFAULT 0 CHECK (iskonto_orani BETWEEN 0 AND 1),
    net_tutar numeric(18,2) NOT NULL CHECK (net_tutar >= 0),
    PRIMARY KEY (teklif_id, sira_no),
    CONSTRAINT fk_teklif_kalemleri_teklif FOREIGN KEY (teklif_id) REFERENCES public.teklifler(teklif_id) ON DELETE CASCADE
);

ALTER TABLE public.teklif_kalemleri ADD COLUMN IF NOT EXISTS urun_adi varchar(250);
ALTER TABLE public.teklif_kalemleri ADD COLUMN IF NOT EXISTS depo_adi varchar(250);


-- 4. TEKLİF DEĞERLENDİRME (AUDIT SNAPSHOT LOG TABLOSU - 1:1 TEKLIF_ID PK)
CREATE TABLE IF NOT EXISTS public.teklif_degerlendirme (
    teklif_id varchar(40) PRIMARY KEY,
    bayi_kodu varchar(100) NOT NULL,
    karar varchar(30) NOT NULL CHECK (karar IN ('APPROVED', 'NEED_APPROVAL', 'REJECTED')),
    risk_skoru integer DEFAULT 0,
    risk_seviyesi varchar(20) DEFAULT 'DUSUK',
    gerekce_kodlari text[] DEFAULT '{}',
    bayi_marji numeric(9,2) DEFAULT 0,
    hedef_tutturma_yuzdesi numeric(9,2) DEFAULT 0,
    ortalama_iskonto_orani numeric(9,2) DEFAULT 0,
    iade_orani numeric(9,2) DEFAULT 0,
    degerlendirme_tarihi timestamp with time zone DEFAULT now(),
    CONSTRAINT fk_teklif_degerlendirme_teklif FOREIGN KEY (teklif_id) REFERENCES public.teklifler(teklif_id) ON DELETE CASCADE
);


-- 5. TEKLİF AI YORUMLARI TABLOSU (1:1 TEKLIF_ID PK)
CREATE TABLE IF NOT EXISTS public.teklif_ai_yorumlari (
    teklif_id varchar(40) PRIMARY KEY,
    ai_comment text,
    alert_message text,
    is_fallback boolean DEFAULT false,
    created_time timestamp with time zone DEFAULT now(),
    CONSTRAINT fk_ai_yorum_teklif FOREIGN KEY (teklif_id) REFERENCES public.teklifler(teklif_id) ON DELETE CASCADE
);

-- Eski kolon adları varsa güvenli geçiş sağla
DO $$
BEGIN
    IF EXISTS (
        SELECT FROM information_schema.columns 
        WHERE table_schema = 'public' AND table_name = 'teklif_ai_yorumlari' AND column_name = 'alert_message_draft'
    ) AND NOT EXISTS (
        SELECT FROM information_schema.columns 
        WHERE table_schema = 'public' AND table_name = 'teklif_ai_yorumlari' AND column_name = 'alert_message'
    ) THEN
        ALTER TABLE public.teklif_ai_yorumlari RENAME COLUMN alert_message_draft TO alert_message;
    END IF;

    IF EXISTS (
        SELECT FROM information_schema.columns 
        WHERE table_schema = 'public' AND table_name = 'teklif_ai_yorumlari' AND column_name = 'created_at'
    ) AND NOT EXISTS (
        SELECT FROM information_schema.columns 
        WHERE table_schema = 'public' AND table_name = 'teklif_ai_yorumlari' AND column_name = 'created_time'
    ) THEN
        ALTER TABLE public.teklif_ai_yorumlari RENAME COLUMN created_at TO created_time;
    END IF;
END $$;

ALTER TABLE public.teklif_ai_yorumlari ADD COLUMN IF NOT EXISTS is_fallback boolean DEFAULT false;
ALTER TABLE public.teklif_ai_yorumlari ADD COLUMN IF NOT EXISTS created_time timestamp with time zone DEFAULT now();

-- RLS
ALTER TABLE public.teklif_ai_yorumlari ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS "Enable read access for all users on ai_yorumlari" ON public.teklif_ai_yorumlari;
CREATE POLICY "Enable read access for all users on ai_yorumlari"
    ON public.teklif_ai_yorumlari FOR SELECT USING (true);


-- 6. DEPO ÇIKIŞ HIZINA GÖRE DİNAMİK STOK EŞİĞİ GÖRÜNÜMÜ
CREATE OR REPLACE VIEW public.vw_dinamik_stok_esigi AS
WITH son_90_gun_depo_satis AS (
    SELECT 
        s."UrunKodu",
        COALESCE(s."DepoKodu", 'DP01') AS "DepoKodu",
        SUM(CASE WHEN s."IsIade" IS NOT TRUE AND s."Miktar" > 0 THEN s."Miktar" ELSE 0 END) AS "Son90GunToplamSatis"
    FROM "satis" s
    WHERE s."Tarih"::date >= (SELECT MAX("Tarih"::date) FROM "satis") - INTERVAL '90 days'
    GROUP BY s."UrunKodu", COALESCE(s."DepoKodu", 'DP01')
)
SELECT 
    st."UrunKodu",
    u."UrunAdi",
    st."DepoKodu",
    d."DepoAdi",
    COALESCE(st."MevcutStok", 0) AS "MevcutStok",
    COALESCE(s90."Son90GunToplamSatis", 0) AS "Son90GunToplamSatis",
    ROUND((COALESCE(s90."Son90GunToplamSatis", 0) / 90.0)::numeric, 2) AS "GunlukOrtalamaSatis",
    CASE 
        WHEN ROUND((COALESCE(s90."Son90GunToplamSatis", 0) / 90.0)::numeric, 2) > 0 
        THEN ROUND((COALESCE(st."MevcutStok", 0) / (COALESCE(s90."Son90GunToplamSatis", 0) / 90.0))::numeric, 1)
        ELSE 999.0 
    END AS "KalanStokGunu",
    ROUND(GREATEST((COALESCE(s90."Son90GunToplamSatis", 0) / 90.0) * 7, 1)::numeric, 2) AS "DinamikStokEsigi_7Gun",
    CASE 
        WHEN COALESCE(st."MevcutStok", 0) < GREATEST((COALESCE(s90."Son90GunToplamSatis", 0) / 90.0) * 7, 1) 
        THEN TRUE ELSE FALSE 
    END AS "StokKritikMi"
FROM "stok" st
JOIN "urunler" u ON st."UrunKodu" = u."UrunKodu"
LEFT JOIN "depolar" d ON st."DepoKodu" = d."DepoKodu"
LEFT JOIN son_90_gun_depo_satis s90 ON st."UrunKodu" = s90."UrunKodu" AND st."DepoKodu" = s90."DepoKodu";


-- 7. İNDEKSLER
CREATE INDEX IF NOT EXISTS ix_teklifler_bayi_kodu ON public.teklifler (bayi_kodu);
CREATE INDEX IF NOT EXISTS ix_teklifler_durum ON public.teklifler (durum);
CREATE INDEX IF NOT EXISTS ix_teklif_kalemleri_urun_depo ON public.teklif_kalemleri (urun_kodu, depo_kodu);
CREATE INDEX IF NOT EXISTS ix_bayi_donem_perf_bayi ON public.bayi_donem_performans (bayi_kodu);
CREATE INDEX IF NOT EXISTS ix_bayi_donem_perf_donem ON public.bayi_donem_performans (donem);

COMMIT;
