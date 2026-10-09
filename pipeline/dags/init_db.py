from airflow.providers.postgres.hooks.postgres import PostgresHook

hook = PostgresHook(postgres_conn_id='supabase')
conn = hook.get_conn()
conn.autocommit = True
cursor = conn.cursor()

try:
    cursor.execute('ALTER TABLE satis ADD CONSTRAINT unique_satis_tarih_bayi_urun UNIQUE ("Tarih", "BayiKodu", "UrunKodu");')
    print('added unique to satis')
except Exception as e:
    print('unique constraint failed on satis:', e)

try:
    create_bayi_sql = """
    CREATE TABLE IF NOT EXISTS public.bayi_donem_performans (
        bayi_kodu text NOT NULL,
        donem date NOT NULL,
        ciro numeric DEFAULT 0,
        hedef numeric DEFAULT 0,
        hedef_tutma_orani numeric DEFAULT 0,
        ortalama_kar_marji numeric DEFAULT 0,
        ortalama_iskonto_orani numeric DEFAULT 0,
        ortalama_iade_orani numeric DEFAULT 0,
        kritik_stok_kalan_urun_sayisi integer DEFAULT 0,
        risk_skoru integer DEFAULT 0,
        risk_seviyesi text DEFAULT 'DUSUK',
        gerekce_kodlari text[] DEFAULT '{}',
        PRIMARY KEY (bayi_kodu, donem)
    );
    """
    cursor.execute(create_bayi_sql)
    print('bayi_donem_performans table created successfully')
except Exception as e:
    print('error creating bayi_donem_performans:', e)
