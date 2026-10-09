-- 20260824000002_clean_bayi_donem_performans.sql
-- Bayi performans tablosunu asil amaci olan snapshot amacina donduren, eski AI kolonlarini silen migration

ALTER TABLE public.bayi_donem_performans
    DROP COLUMN IF EXISTS ai_comment,
    DROP COLUMN IF EXISTS llm_fallback_used,
    DROP COLUMN IF EXISTS deneme_sayisi,
    DROP COLUMN IF EXISTS islenme_zamani,
    DROP COLUMN IF EXISTS hesaplanan_karar,
    DROP COLUMN IF EXISTS manuel_karar,
    DROP COLUMN IF EXISTS nihai_karar,
    DROP COLUMN IF EXISTS override_audit;
