-- 20260821000001_bayi_donem_performans.sql
-- Create bayi_donem_performans table for the ETL and LLM Pipeline (Snapshot Architecture)

CREATE TABLE IF NOT EXISTS public.bayi_donem_performans (
    bayi_kodu text NOT NULL,
    donem date NOT NULL,
    snapshot_tarihi date NOT NULL,
    
    segment text,
    bolge text,
    gerceklesen_ciro numeric,
    hedef_ciro numeric,
    hedefe_ulasma_pct numeric,
    ortalama_marj_pct numeric,
    marj_esigi_pct numeric,
    ortalama_iskonto_pct numeric,
    iade_orani_pct numeric,
    kritik_stok_urun_sayisi integer,
    
    -- ETL Computed Metrics
    risk_skoru integer,
    risk_seviyesi text,
    gerekce_kodlari text[],
    
    -- Auditing & Decision Overrides
    job_run_id text,
    hesaplanan_karar text,
    manuel_karar text,
    nihai_karar text,
    override_audit jsonb,
    
    -- LLM Automation & State Columns
    ai_comment text,
    llm_fallback_used boolean DEFAULT false,
    deneme_sayisi integer DEFAULT 0,
    islenme_zamani timestamp with time zone,
    
    -- Primary Key enables storing multiple daily snapshots for the same dealer and period
    PRIMARY KEY (bayi_kodu, donem, snapshot_tarihi)
);

-- Note: The daily ETL DAG will INSERT a new snapshot each day. 
-- The AI task will then UPDATE the ai_comment for that specific snapshot.
