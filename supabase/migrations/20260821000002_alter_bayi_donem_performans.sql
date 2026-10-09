-- 20260821000002_alter_bayi_donem_performans.sql
-- Ad ve yapı tutarsızlıklarına karşı mevcut tabloya kolonları güvenli şekilde ekleyen (idempotent) migration

ALTER TABLE public.bayi_donem_performans
    ADD COLUMN IF NOT EXISTS manuel_karar text,
    ADD COLUMN IF NOT EXISTS override_audit jsonb,
    ADD COLUMN IF NOT EXISTS hesaplanan_karar text,
    ADD COLUMN IF NOT EXISTS nihai_karar text,
    ADD COLUMN IF NOT EXISTS ai_comment text,
    ADD COLUMN IF NOT EXISTS llm_fallback_used boolean DEFAULT false,
    ADD COLUMN IF NOT EXISTS deneme_sayisi integer DEFAULT 0,
    ADD COLUMN IF NOT EXISTS islenme_zamani timestamp with time zone;
