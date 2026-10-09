from typing import Optional, List
from fastapi import FastAPI, HTTPException, Query, BackgroundTasks, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.openapi.utils import get_openapi
from fastapi.responses import JSONResponse
from starlette.concurrency import run_in_threadpool
from sqlalchemy import text
from .database import engine_al
from .schemas import TeklifCreate, KalemCreate, TeklifDurumUpdate
from .auth import authenticate_authorization_header, authentication_is_required
from datetime import datetime, timezone
import logging
import sys
import os
import time
import threading

logger = logging.getLogger(__name__)

# app/ai modüllerini yükleyebilmek için sys.path ekliyoruz
BASE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if BASE_DIR not in sys.path:
    sys.path.append(BASE_DIR)
    sys.path.append(os.path.join(BASE_DIR, 'app', 'ai'))

from app.ai.offer_evaluator import evaluate_teklif, get_default_warehouse

app = FastAPI(
    title="Bayi Performans Platformu - B2B Teklif & Performans API'si",
    description="Depo bazlı stok, kural motoru, teklif değerlendirme ve bayi performans servisleri.",
    version="2.1.0",
)

def get_allowed_origins() -> list[str]:
    raw_origins = os.getenv(
        "ALLOWED_ORIGINS",
        "http://localhost:5173,http://localhost:3000",
    )
    origins = [origin.strip().rstrip("/") for origin in raw_origins.split(",") if origin.strip()]
    if "*" in origins:
        raise RuntimeError("ALLOWED_ORIGINS acik origin listesi icermelidir; '*' kullanilamaz.")
    return origins


PUBLIC_PATHS = {
    "/health",
    "/health/ready",
    "/api/v1/health",
    "/docs",
    "/docs/oauth2-redirect",
    "/redoc",
    "/openapi.json",
}


@app.middleware("http")
async def require_supabase_access_token(request: Request, call_next):
    if (
        request.method == "OPTIONS"
        or request.url.path in PUBLIC_PATHS
        or not authentication_is_required()
    ):
        return await call_next(request)

    try:
        request.state.user = await run_in_threadpool(
            authenticate_authorization_header,
            request.headers.get("Authorization"),
        )
    except HTTPException as exc:
        return JSONResponse(
            status_code=exc.status_code,
            content={
                "title": "Kimlik dogrulanamadi." if exc.status_code == 401 else "Kimlik servisi kullanilamiyor.",
                "status": exc.status_code,
                "detail": exc.detail,
                "hata_kodu": getattr(exc, "error_code", "KIMLIK_DOGRULANAMADI"),
            },
            headers=exc.headers,
        )

    return await call_next(request)


# CORS, kimlik middleware'inin dışındadır; böylece 401/503 cevapları da tarayıcı
# tarafından okunabilir. Kimlik bilgisi taşıyan isteklerde wildcard kullanılmaz.
app.add_middleware(
    CORSMiddleware,
    allow_origins=get_allowed_origins(),
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


def custom_openapi():
    if app.openapi_schema:
        return app.openapi_schema

    schema = get_openapi(
        title=app.title,
        version=app.version,
        description=app.description,
        routes=app.routes,
    )
    schema.setdefault("components", {}).setdefault("securitySchemes", {})["BearerAuth"] = {
        "type": "http",
        "scheme": "bearer",
        "bearerFormat": "Supabase access token",
    }
    for path, path_item in schema.get("paths", {}).items():
        for operation in path_item.values():
            if isinstance(operation, dict) and "responses" in operation:
                operation["security"] = [] if path in PUBLIC_PATHS else [{"BearerAuth": []}]

    app.openapi_schema = schema
    return schema


app.openapi = custom_openapi

# --- IN-MEMORY CACHING KATMANI ---
_cache = {}
_cache_lock = threading.Lock()

def cache_get(key: str):
    with _cache_lock:
        item = _cache.get(key)
        if item:
            val, exp = item
            if exp > time.time():
                return val
            else:
                del _cache[key]
        return None

def cache_set(key: str, val, ttl: int = 60):
    with _cache_lock:
        _cache[key] = (val, time.time() + ttl)

def cache_clear(prefix: Optional[str] = None):
    with _cache_lock:
        if prefix:
            keys_to_del = [k for k in list(_cache.keys()) if k.startswith(prefix)]
            for k in keys_to_del:
                del _cache[k]
        else:
            _cache.clear()

def sorgula(sql, params=None):
    engine = engine_al()
    with engine.connect() as conn:
        sonuc = conn.execute(text(sql), params or {})
        return [dict(satir._mapping) for satir in sonuc]

def execute_sql(sql, params=None):
    engine = engine_al()
    with engine.begin() as conn:  # begin() otomatik commit yapar
        conn.execute(text(sql), params or {})

def get_default_warehouse_for_dealer(bayi_kodu: str) -> str:
    """
    Bayinin Sehir ve Bolge bilgisine göre en yakın/yetkili sevk deposunu CITY_TO_DEPO_MAP ile otomatik belirler.
    """
    ckey = f"dealer_wh_{bayi_kodu}"
    cached = cache_get(ckey)
    if cached is not None:
        return cached
    try:
        bayi = sorgula('SELECT "Sehir", "Bolge" FROM bayiler WHERE "BayiKodu" = :kodu', {"kodu": bayi_kodu})
        if not bayi:
            return "DP01"
        sehir = bayi[0].get("Sehir") or ""
        bolge = bayi[0].get("Bolge") or ""
        wh = get_default_warehouse(sehir, bolge)
        cache_set(ckey, wh, ttl=300)
        return wh
    except Exception:
        return "DP01"

# --- HEALTH ENDPOINTLERİ ---

@app.get("/health")
def liveness_check():
    """Render'ın prosesin ayakta olduğunu anlaması için bağımlılıksız kontrol."""

    return {"durum": "calisiyor"}


@app.get("/health/ready")
@app.get("/api/v1/health")
def readiness_check():
    """Canlı isteklerden önce PostgreSQL erişiminin de hazır olduğunu doğrular."""

    try:
        sorgula("SELECT 1")
    except Exception:
        logger.exception("FastAPI veritabani hazirlik kontrolu basarisiz oldu.")
        raise HTTPException(
            status_code=503,
            detail="Veritabani baglantisi hazir degil.",
        )
    return {"durum": "calisiyor", "veritabani": "baglandi"}

# --- MASTER TANIMLAR & KATALOG ENDPOINTLERİ ---

@app.get("/bayiler")
@app.get("/api/v1/bayiler")
def bayileri_getir(bayi_kodu: Optional[str] = Query(default=None)):
    ckey = f"bayiler_{bayi_kodu or 'all'}"
    cached = cache_get(ckey)
    if cached is not None:
        return cached

    sql = 'SELECT "BayiKodu" AS bayi_kodu, "BayiAdi" AS bayi_adi, "Segment" AS segment, "Bolge" AS bolge, "Sehir" AS sehir FROM bayiler'
    params = {}
    if bayi_kodu:
        sql += ' WHERE "BayiKodu" = :bayi_kodu'
        params["bayi_kodu"] = bayi_kodu
    sql += ' ORDER BY "BayiKodu"'
    try:
        kayitlar = sorgula(sql, params)
        for k in kayitlar:
            k["varsayilan_depo_kodu"] = get_default_warehouse(k.get("sehir") or "", k.get("bolge") or "")
        cache_set(ckey, kayitlar, ttl=300)
        return kayitlar
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Veri cekilemedi: {e}")

@app.get("/urunler")
@app.get("/api/v1/urunler")
def urunleri_getir():
    cached = cache_get("urunler_all")
    if cached is not None:
        return cached
    # Frontend'e maliyet gönderilmez, sadece ürün listesi döner
    sql = 'SELECT "UrunKodu" AS urun_kodu, "UrunAdi" AS urun_adi, "Kategori" AS kategori FROM urunler ORDER BY "UrunKodu"'
    try:
        kayitlar = sorgula(sql)
        cache_set("urunler_all", kayitlar, ttl=300)
        return kayitlar
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Urunler cekilemedi: {e}")

@app.get("/depolar")
@app.get("/api/v1/depolar")
def depolari_getir():
    cached = cache_get("depolar_all")
    if cached is not None:
        return cached
    sql = 'SELECT "DepoKodu" AS depo_kodu, "DepoAdi" AS depo_adi, "Bolge" AS bolge, "Sehir" AS sehir FROM depolar ORDER BY "DepoKodu"'
    try:
        kayitlar = sorgula(sql)
        cache_set("depolar_all", kayitlar, ttl=300)
        return kayitlar
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Depolar cekilemedi: {e}")

@app.get("/stok")
@app.get("/stok/kritik")
@app.get("/api/v1/stok")
@app.get("/api/v1/stok/kritik")
def stok_getir(
    depo_kodu: Optional[str] = None, 
    urun_kodu: Optional[str] = None,
    bayi_kodu: Optional[str] = None,
    sadece_kritik: Optional[bool] = None
):
    """
    Şirket depolarındaki dinamik stok çıkış hızını ve kritik stok durumunu vw_dinamik_stok_esigi view'ından getirir.
    - 7 günlük satış hızının altına inen (StokKritikMi = TRUE veya KalanStokGunu < 7) ürünleri tespit eder.
    - Bayi seçildiğinde bayinin bölgesine/şehrine bağlı deponun dinamik kritik stoklarını döner.
    """
    ckey = f"stok_{depo_kodu}_{urun_kodu}_{bayi_kodu}_{sadece_kritik}"
    cached = cache_get(ckey)
    if cached is not None:
        return cached

    # Eğer bayi_kodu verilmişse ve depo_kodu belirtilmemişse, bayinin eşleştiği depoyu bul
    if bayi_kodu and not depo_kodu:
        bayi_res = sorgula("""
            SELECT 
                CASE 
                    WHEN "Sehir" ILIKE '%istanbul%' OR "Sehir" ILIKE '%kocaeli%' OR "Sehir" ILIKE '%tekirdağ%' OR "Sehir" ILIKE '%bursa%' OR "Bolge" ILIKE '%marmara%' THEN 'DP01'
                    WHEN "Sehir" ILIKE '%ankara%' OR "Sehir" ILIKE '%eskişehir%' OR "Sehir" ILIKE '%konya%' OR "Sehir" ILIKE '%kayseri%' OR "Bolge" ILIKE '%iç anadolu%' OR "Bolge" ILIKE '%ic anadolu%' OR "Bolge" ILIKE '%doğu anadolu%' OR "Bolge" ILIKE '%dogu anadolu%' THEN 'DP02'
                    WHEN "Sehir" ILIKE '%izmir%' OR "Sehir" ILIKE '%manisa%' OR "Sehir" ILIKE '%aydın%' OR "Sehir" ILIKE '%denizli%' OR "Bolge" ILIKE '%ege%' THEN 'DP03'
                    WHEN "Sehir" ILIKE '%adana%' OR "Sehir" ILIKE '%antalya%' OR "Sehir" ILIKE '%mersin%' OR "Sehir" ILIKE '%hatay%' OR "Bolge" ILIKE '%akdeniz%' THEN 'DP04'
                    WHEN "Sehir" ILIKE '%samsun%' OR "Sehir" ILIKE '%trabzon%' OR "Sehir" ILIKE '%ordu%' OR "Sehir" ILIKE '%rize%' OR "Bolge" ILIKE '%karadeniz%' THEN 'DP05'
                    WHEN "Sehir" ILIKE '%gaziantep%' OR "Sehir" ILIKE '%şanlıurfa%' OR "Sehir" ILIKE '%sanliurfa%' OR "Sehir" ILIKE '%diyarbakır%' OR "Sehir" ILIKE '%mardin%' OR "Bolge" ILIKE '%güneydoğu%' OR "Bolge" ILIKE '%guneydogu%' THEN 'DP06'
                    ELSE 'DP01'
                END AS depo_kodu
            FROM bayiler WHERE "BayiKodu" = :bk
        """, {"bk": bayi_kodu})
        if bayi_res and bayi_res[0].get("depo_kodu"):
            depo_kodu = bayi_res[0]["depo_kodu"]

    sql = """
        SELECT 
            v."UrunKodu" AS urun_kodu,
            v."UrunAdi" AS urun_adi,
            v."DepoKodu" AS depo_kodu,
            v."DepoAdi" AS depo_adi,
            v."MevcutStok" AS mevcut_stok,
            v."Son90GunToplamSatis" AS son_90_gun_satis,
            v."Son90GunToplamSatis" AS son_uc_ay_satis_adedi,
            v."Son90GunToplamSatis" AS son90GunToplamSatis,
            v."GunlukOrtalamaSatis" AS gunluk_ortalama_satis,
            v."GunlukOrtalamaSatis" AS ortalama_gunluk_satis,
            v."KalanStokGunu" AS kalan_stok_gunu,
            v."KalanStokGunu" AS tahmini_stok_gunu,
            v."StokKritikMi" AS stok_kritik_mi
        FROM vw_dinamik_stok_esigi v
        WHERE 1=1
    """
    params = {}
    if depo_kodu:
        sql += ' AND v."DepoKodu" = :depo'
        params["depo"] = depo_kodu
    if urun_kodu:
        sql += ' AND v."UrunKodu" = :urun'
        params["urun"] = urun_kodu

    # Eğer sadece kritik stok isteniyorsa (veya default olarak kritik view üzerinden çağrıldıysa)
    if sadece_kritik is not False:
        sql += ' AND v."StokKritikMi" = TRUE'

    sql += ' ORDER BY v."KalanStokGunu" ASC, v."UrunKodu" ASC'
    try:
        kayitlar = sorgula(sql, params)
        for k in kayitlar:
            gerekceler = []
            if k.get("stok_kritik_mi") or (k.get("kalan_stok_gunu") is not None and float(k.get("kalan_stok_gunu")) <= 7.0):
                gerekceler.append("DINAMIK_ESIK_ALTI")
                gerekceler.append("STOK_GUNU_KRITIK")
            k["gerekce_kodlari"] = gerekceler
        res = {"toplam": len(kayitlar), "urunler": kayitlar}
        cache_set(ckey, res, ttl=60)
        return res
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Stok verisi çekilemedi: {e}")

@app.get("/tanimlar/filtre-secenekleri")
@app.get("/api/v1/tanimlar/filtre-secenekleri")
def filtre_secenekleri_getir():
    cached = cache_get("filtre_secenekleri")
    if cached is not None:
        return cached
    try:
        sql = "SELECT DISTINCT TO_CHAR(donem, 'YYYY-MM') AS donem FROM public.bayi_donem_performans ORDER BY donem DESC"
        rows = sorgula(sql)
        donemler = [r["donem"] for r in rows] if rows else []
    except Exception:
        donemler = []

    res = {
        "segmentler": ["Bireysel", "KOBİ", "Kurumsal"],
        "bolgeler": ["Marmara", "Ege", "İç Anadolu", "Akdeniz", "Karadeniz", "Güneydoğu Anadolu", "Doğu Anadolu"],
        "donemler": donemler
    }
    cache_set("filtre_secenekleri", res, ttl=300)
    return res

@app.get("/donemler")
@app.get("/api/v1/donemler")
def donemleri_getir():
    cached = cache_get("donemler_list")
    if cached is not None:
        return cached
    try:
        sql = "SELECT DISTINCT TO_CHAR(donem, 'YYYY-MM') AS donem FROM public.bayi_donem_performans ORDER BY donem DESC"
        rows = sorgula(sql)
        donemler = [r["donem"] for r in rows] if rows else []
        res = {"donemler": donemler, "toplam": len(donemler)}
        cache_set("donemler_list", res, ttl=300)
        return res
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Dönemler çekilemedi: {e}")

@app.post("/cache/clear")
@app.post("/api/v1/cache/clear")
def clear_cache_endpoint():
    cache_clear()
    return {"status": "success", "message": "In-memory cache temizlendi."}


# --- BAYİ PERFORMANS ENDPOINTLERİ ---

@app.get("/bayi-performans")
@app.get("/api/v1/bayi-performans")
def bayi_performans_getir(
    bayi_kodu: Optional[str] = Query(default=None),
    donem: Optional[str] = Query(default=None),
):
    """
    Aylık karne tablosu (bayi_donem_performans) üzerinden performans kayıtlarını döner.
    """
    ckey = f"bayi_perf_{bayi_kodu}_{donem}"
    cached = cache_get(ckey)
    if cached is not None:
        return cached

    sql = """
        SELECT 
            p.bayi_kodu,
            p.donem,
            p.ciro,
            p.ciro AS gerceklesen_ciro,
            p.hedef,
            p.hedef AS hedef_ciro,
            p.hedef_tutma_orani,
            p.hedef_tutma_orani AS hedefe_ulasma_pct,
            p.ortalama_kar_marji,
            p.ortalama_kar_marji AS ortalama_marj_pct,
            p.ortalama_iskonto_orani,
            p.ortalama_iskonto_orani AS ortalama_iskonto_pct,
            p.ortalama_iade_orani,
            p.ortalama_iade_orani AS iade_orani_pct,
            p.kritik_stok_kalan_urun_sayisi,
            p.risk_skoru,
            p.risk_seviyesi,
            p.gerekce_kodlari,
            b."BayiAdi" AS bayi_adi,
            b."Segment" AS segment,
            b."Bolge" AS bolge
        FROM public.bayi_donem_performans p
        LEFT JOIN bayiler b ON p.bayi_kodu = b."BayiKodu"
        WHERE 1=1
    """
    params = {}
    if bayi_kodu:
        sql += " AND p.bayi_kodu = :bayi_kodu"
        params["bayi_kodu"] = bayi_kodu
    if donem:
        if len(donem) == 7: # YYYY-MM
            sql += " AND TO_CHAR(p.donem, 'YYYY-MM') = :donem"
        else:
            sql += " AND (p.donem = :donem::date OR TO_CHAR(p.donem, 'YYYY-MM') = :donem)"
        params["donem"] = donem
    sql += " ORDER BY p.donem DESC, p.bayi_kodu ASC"
    try:
        kayitlar = sorgula(sql, params)
        cache_set(ckey, kayitlar, ttl=120)
        return kayitlar
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Performans verisi cekilemedi: {e}")

@app.get("/bayiler/risk-listesi")
@app.get("/api/v1/bayiler/risk-listesi")
def risk_listesi_getir(
    donem: Optional[str] = Query(default=None),
    segment: Optional[str] = Query(default=None),
    bolge: Optional[str] = Query(default=None),
):
    ckey = f"risk_list_{donem}_{segment}_{bolge}"
    cached = cache_get(ckey)
    if cached is not None:
        return cached

    base_sql = """
        SELECT 
            p.bayi_kodu,
            COALESCE(b."BayiAdi", p.bayi_kodu) AS bayi_adi,
            COALESCE(b."Segment", 'Genel') AS segment,
            COALESCE(b."Bolge", 'Genel') AS bolge,
            p.donem,
            p.ciro,
            p.ciro AS gerceklesen_ciro,
            p.hedef,
            p.hedef AS hedef_ciro,
            p.hedef_tutma_orani AS hedefe_ulasma_pct,
            p.ortalama_kar_marji AS ortalama_marj_pct,
            p.ortalama_iskonto_orani AS ortalama_iskonto_pct,
            p.ortalama_iade_orani AS iade_orani_pct,
            p.kritik_stok_kalan_urun_sayisi,
            p.risk_skoru,
            p.risk_seviyesi,
            p.gerekce_kodlari
        FROM public.bayi_donem_performans p
        LEFT JOIN bayiler b ON p.bayi_kodu = b."BayiKodu"
        WHERE 1=1
    """
    params = {}
    where_extra = ""
    if donem:
        if len(donem) == 7:
            where_extra += " AND TO_CHAR(p.donem, 'YYYY-MM') = :donem"
        else:
            where_extra += " AND (p.donem = :donem::date OR TO_CHAR(p.donem, 'YYYY-MM') = :donem)"
        params["donem"] = donem
    if segment:
        where_extra += ' AND b."Segment" = :segment'
        params["segment"] = segment
    if bolge:
        where_extra += ' AND b."Bolge" = :bolge'
        params["bolge"] = bolge
        
    sql = base_sql + where_extra + " ORDER BY p.risk_skoru DESC, p.bayi_kodu ASC"
    try:
        kayitlar = sorgula(sql, params)
        # Belirtilen donemde kayit yoksa, en guncel donemin gercek verilerini don
        if not kayitlar and donem:
            fallback_sql = base_sql + " AND p.donem = (SELECT MAX(donem) FROM public.bayi_donem_performans)"
            if segment:
                fallback_sql += ' AND b."Segment" = :segment'
            if bolge:
                fallback_sql += ' AND b."Bolge" = :bolge'
            fallback_sql += " ORDER BY p.risk_skoru DESC, p.bayi_kodu ASC"
            kayitlar = sorgula(fallback_sql, params)

        res = {"donem": donem or "2026-08", "toplam": len(kayitlar), "bayiler": kayitlar}
        cache_set(ckey, res, ttl=120)
        return res
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Risk listesi cekilemedi: {e}")

@app.get("/bayiler/ozet")
@app.get("/api/v1/bayiler/ozet")
def bayiler_ozet_getir(donem: Optional[str] = Query(default=None)):
    """
    Dashboard üst kartları için toplam ciro, bayi sayısı, genel hedef tutturma ve ortalama marj döner.
    """
    ckey = f"ozet_{donem}"
    cached = cache_get(ckey)
    if cached is not None:
        return cached

    sql = """
        SELECT 
            COUNT(DISTINCT p.bayi_kodu) AS toplam_bayi_sayisi,
            COALESCE(SUM(p.ciro), 0) AS toplam_gerceklesen_ciro,
            COALESCE(SUM(p.hedef), 0) AS toplam_hedef_ciro,
            ROUND(AVG(p.hedef_tutma_orani)::numeric, 2) AS genel_hedefe_ulasma_pct,
            ROUND(AVG(p.ortalama_kar_marji)::numeric, 2) AS genel_ortalama_marj_pct,
            COUNT(CASE WHEN p.risk_seviyesi = 'YUKSEK' THEN 1 END) AS yuksek_riskli_bayi_sayisi
        FROM public.bayi_donem_performans p
        WHERE 1=1
    """
    params = {}
    if donem:
        if len(donem) == 7:
            sql += " AND TO_CHAR(p.donem, 'YYYY-MM') = :donem"
        else:
            sql += " AND (p.donem = :donem::date OR TO_CHAR(p.donem, 'YYYY-MM') = :donem)"
        params["donem"] = donem
    else:
        sql += " AND p.donem = (SELECT MAX(donem) FROM public.bayi_donem_performans)"
        
    try:
        ozet = sorgula(sql, params)
        res = ozet[0] if ozet and ozet[0].get("toplam_bayi_sayisi", 0) > 0 else None
        if not res and donem:
            fallback_sql = """
                SELECT 
                    COUNT(DISTINCT p.bayi_kodu) AS toplam_bayi_sayisi,
                    COALESCE(SUM(p.ciro), 0) AS toplam_gerceklesen_ciro,
                    COALESCE(SUM(p.hedef), 0) AS toplam_hedef_ciro,
                    ROUND(AVG(p.hedef_tutma_orani)::numeric, 2) AS genel_hedefe_ulasma_pct,
                    ROUND(AVG(p.ortalama_kar_marji)::numeric, 2) AS genel_ortalama_marj_pct,
                    COUNT(CASE WHEN p.risk_seviyesi = 'YUKSEK' THEN 1 END) AS yuksek_riskli_bayi_sayisi
                FROM public.bayi_donem_performans p
                WHERE p.donem = (SELECT MAX(donem) FROM public.bayi_donem_performans)
            """
            fb_ozet = sorgula(fallback_sql)
            res = fb_ozet[0] if fb_ozet else {}
        elif not res:
            res = {}

        result_dict = {
            "toplamBayiSayisi": res.get("toplam_bayi_sayisi", 0),
            "toplam_bayi_sayisi": res.get("toplam_bayi_sayisi", 0),
            "toplamGerceklesenCiro": float(res.get("toplam_gerceklesen_ciro", 0)),
            "toplam_gerceklesen_ciro": float(res.get("toplam_gerceklesen_ciro", 0)),
            "toplamCiro": float(res.get("toplam_gerceklesen_ciro", 0)),
            "genelHedefeUlasmaPct": float(res.get("genel_hedefe_ulasma_pct") or 100.0),
            "genel_hedefe_ulasma_pct": float(res.get("genel_hedefe_ulasma_pct") or 100.0),
            "ortalamaHedefeUlasmaPct": float(res.get("genel_hedefe_ulasma_pct") or 100.0),
            "genelOrtalamaMarjPct": float(res.get("genel_ortalama_marj_pct") or 18.0),
            "genel_ortalama_marj_pct": float(res.get("genel_ortalama_marj_pct") or 18.0),
            "ortalamaMarjPct": float(res.get("genel_ortalama_marj_pct") or 18.0),
            "yuksekRiskliBayiSayisi": res.get("yuksek_riskli_bayi_sayisi", 0),
            "yuksek_riskli_bayi_sayisi": res.get("yuksek_riskli_bayi_sayisi", 0),
        }
        cache_set(ckey, result_dict, ttl=120)
        return result_dict
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Özet verisi çekilemedi: {e}")

@app.get("/bayiler/bolge-dagilimi")
@app.get("/api/v1/bayiler/bolge-dagilimi")
def bolge_dagilimi_getir():
    cached = cache_get("bolge_dagilimi")
    if cached is not None:
        return cached
    sql = """
        SELECT 
            b."Bolge" AS bolge,
            COUNT(b."BayiKodu") AS bayi_sayisi,
            ROUND((COUNT(b."BayiKodu") * 100.0 / NULLIF((SELECT COUNT(*) FROM bayiler), 0))::numeric, 2) AS ciro_payi_pct
        FROM bayiler b
        GROUP BY b."Bolge"
        ORDER BY bayi_sayisi DESC
    """
    try:
        res = sorgula(sql)
        cache_set("bolge_dagilimi", res, ttl=300)
        return res
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Bölge dağılımı çekilemedi: {e}")

@app.get("/bayiler/sehir-dagilimi")
@app.get("/api/v1/bayiler/sehir-dagilimi")
def sehir_dagilimi_getir():
    cached = cache_get("sehir_dagilimi")
    if cached is not None:
        return cached
    sql = """
        SELECT 
            b."Sehir" AS sehir,
            COUNT(b."BayiKodu") AS bayi_sayisi,
            ROUND((COUNT(b."BayiKodu") * 100.0 / NULLIF((SELECT COUNT(*) FROM bayiler), 0))::numeric, 2) AS ciro_payi_pct
        FROM bayiler b
        GROUP BY b."Sehir"
        ORDER BY bayi_sayisi DESC
        LIMIT 10
    """
    try:
        res = sorgula(sql)
        cache_set("sehir_dagilimi", res, ttl=300)
        return res
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Şehir dağılımı çekilemedi: {e}")

@app.get("/stok/urun-dagilimi")
@app.get("/api/v1/stok/urun-dagilimi")
def stok_urun_dagilimi_getir():
    cached = cache_get("stok_urun_dagilimi")
    if cached is not None:
        return cached
    sql = """
        SELECT 
            d."DepoKodu" AS depo_kodu,
            d."DepoAdi" AS depo_adi,
            COUNT(CASE WHEN v."StokKritikMi" = TRUE THEN 1 END) AS kritik_kalem_sayisi,
            COALESCE(SUM(v."MevcutStok"), 0) AS toplam_stok_value
        FROM depolar d
        LEFT JOIN vw_dinamik_stok_esigi v ON d."DepoKodu" = v."DepoKodu"
        GROUP BY d."DepoKodu", d."DepoAdi"
        ORDER BY d."DepoKodu"
    """
    try:
        res = sorgula(sql)
        cache_set("stok_urun_dagilimi", res, ttl=120)
        return res
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Stok ürün dağılımı çekilemedi: {e}")

@app.get("/bayi/{bayi_kodu}")
@app.get("/bayi/{bayi_kodu}/performans")
@app.get("/api/v1/bayi/{bayi_kodu}")
@app.get("/api/v1/bayi/{bayi_kodu}/performans")
@app.get("/api/v1/performance/dealers/{bayi_kodu}")
def bayi_detay_getir(bayi_kodu: str, donem: Optional[str] = Query(default=None)):
    """
    Bayi detayını ve seçilen dönem (veya en güncel dönem) performans karnesini döner.
    """
    ckey = f"bayi_detay_{bayi_kodu}_{donem}"
    cached = cache_get(ckey)
    if cached is not None:
        return cached

    bayi = sorgula('SELECT * FROM bayiler WHERE "BayiKodu" = :kodu', {"kodu": bayi_kodu})
    if not bayi:
        raise HTTPException(status_code=404, detail=f"Bayi bulunamadı: {bayi_kodu}")
    
    sql = """
        SELECT 
            bayi_kodu, donem, ciro, hedef, hedef_tutma_orani,
            ortalama_kar_marji, ortalama_iskonto_orani, ortalama_iade_orani,
            kritik_stok_kalan_urun_sayisi, risk_skoru, risk_seviyesi, gerekce_kodlari,
            ciro AS gerceklesen_ciro, hedef AS hedef_ciro,
            hedef_tutma_orani AS hedefe_ulasma_pct,
            ortalama_kar_marji AS ortalama_marj_pct,
            ortalama_iskonto_orani AS ortalama_iskonto_pct,
            ortalama_iade_orani AS iade_orani_pct
        FROM public.bayi_donem_performans 
        WHERE bayi_kodu = :kodu
    """
    params = {"kodu": bayi_kodu}
    if donem:
        if len(donem) == 7:
            sql += " AND TO_CHAR(donem, 'YYYY-MM') = :donem"
        else:
            sql += " AND (donem = :donem::date OR TO_CHAR(donem, 'YYYY-MM') = :donem)"
        params["donem"] = donem
        
    sql += " ORDER BY donem DESC LIMIT 1"
    perf = sorgula(sql, params)
    
    res = {
        "bayi": bayi[0],
        "guncel_performans": perf[0] if perf else None,
        **(perf[0] if perf else {})
    }
    cache_set(ckey, res, ttl=120)
    return res

@app.get("/bayi/{bayi_kodu}/gecmis")
@app.get("/api/v1/bayi/{bayi_kodu}/gecmis")
@app.get("/api/v1/dealers/{bayi_kodu}/history")
def bayi_gecmis_getir(bayi_kodu: str, ay_sayisi: int = Query(default=60)):
    """
    Bayinin geçmiş N dönemlik performans karnelerini kronolojik sırayla döner.
    """
    ckey = f"bayi_gecmis_{bayi_kodu}_{ay_sayisi}"
    cached = cache_get(ckey)
    if cached is not None:
        return cached

    sql = """
        SELECT 
            TO_CHAR(donem, 'YYYY-MM') AS donem,
            ciro AS gerceklesen_ciro,
            hedef AS hedef_ciro,
            hedef_tutma_orani AS hedefe_ulasma_pct,
            ortalama_kar_marji AS ortalama_marj_pct,
            ortalama_iskonto_orani AS ortalama_iskonto_pct,
            ortalama_iade_orani AS iade_orani_pct,
            risk_skoru,
            risk_seviyesi,
            gerekce_kodlari
        FROM public.bayi_donem_performans
        WHERE bayi_kodu = :bayi_kodu
        ORDER BY donem DESC
        LIMIT :limit
    """
    kayitlar = sorgula(sql, {"bayi_kodu": bayi_kodu, "limit": ay_sayisi})
    kayitlar.reverse()
    res = {
        "bayi_kodu": bayi_kodu,
        "aylik_performanslar": kayitlar,
        "items": kayitlar
    }
    cache_set(ckey, res, ttl=120)
    return res

# --- TEKLİF YÖNETİMİ & DEĞERLENDİRME ENDPOINTLERİ ---

def generate_next_teklif_id() -> str:
    today_str = datetime.now(timezone.utc).strftime("%Y%m%d")
    count_res = sorgula("SELECT COUNT(*) as cnt FROM public.teklifler WHERE teklif_id LIKE :pattern", {"pattern": f"TEK-{today_str}-%"})
    next_num = (count_res[0]["cnt"] if count_res else 0) + 1
    return f"TEK-{today_str}-{next_num:04d}"

def _save_teklif_to_db(teklif: TeklifCreate) -> str:
    """
    TEK TRANSACTION (Tek Atomik İşlem):
    Teklif başlığını, eski kalemlerin temizlenmesini ve yeni kalemlerin eklenmesini
    tek bir veritabanı transaction'ı içerisinde 'all-or-nothing' güvencesiyle kaydeder.
    """
    teklif_id = teklif.teklif_id.strip() if (teklif.teklif_id and teklif.teklif_id.strip()) else generate_next_teklif_id()
    default_depo = get_default_warehouse_for_dealer(teklif.bayi_kodu)
    toplam_net = sum([k.miktar * k.birim_fiyat * (1.0 - (k.iskonto_orani or 0.0)) for k in teklif.kalemler])

    sql_teklif = """
        INSERT INTO public.teklifler 
        (teklif_id, bayi_kodu, durum, olusturulma_tarihi, toplam_net_tutar, aciklama)
        VALUES (:id, :bayi, 'BEKLIYOR', NOW(), :tutar, :aciklama)
        ON CONFLICT (teklif_id) DO UPDATE SET
            bayi_kodu = EXCLUDED.bayi_kodu,
            toplam_net_tutar = EXCLUDED.toplam_net_tutar,
            aciklama = EXCLUDED.aciklama;
    """

    sql_delete_kalemler = "DELETE FROM public.teklif_kalemleri WHERE teklif_id = :id"

    sql_kalem = """
        INSERT INTO public.teklif_kalemleri
        (teklif_id, sira_no, urun_kodu, depo_kodu, miktar, birim_fiyat, iskonto_orani, net_tutar)
        VALUES (:id, :sira, :urun, :depo, :miktar, :fiyat, :iskonto, :net)
    """

    engine = engine_al()
    with engine.begin() as conn:
        # 1. Başlığı yaz
        conn.execute(text(sql_teklif), {
            "id": teklif_id,
            "bayi": teklif.bayi_kodu,
            "tutar": round(toplam_net, 2),
            "aciklama": teklif.aciklama or ""
        })

        # 2. Varsa eski kalemleri sil
        conn.execute(text(sql_delete_kalemler), {"id": teklif_id})

        # 3. Tüm kalemleri aynı transaction'da ekle
        for i, kalem in enumerate(teklif.kalemler, 1):
            sira_no = kalem.sira_no if kalem.sira_no else i
            depo_kodu = kalem.depo_kodu or default_depo
            iskonto = kalem.iskonto_orani if kalem.iskonto_orani is not None else 0.0
            net_tutar = round(kalem.miktar * kalem.birim_fiyat * (1.0 - iskonto), 2)
            
            conn.execute(text(sql_kalem), {
                "id": teklif_id,
                "sira": sira_no,
                "urun": kalem.urun_kodu,
                "depo": depo_kodu,
                "miktar": kalem.miktar,
                "fiyat": kalem.birim_fiyat,
                "iskonto": iskonto,
                "net": net_tutar
            })

    return teklif_id

@app.post("/api/v1/teklifler/olustur-ve-degerlendir")
@app.post("/teklifler/olustur-ve-degerlendir")
def teklif_olustur_ve_degerlendir(teklif: TeklifCreate):
    """
    TEK ADIMDA: Teklifi kaydeder, kural motoru ve LLM değerlendirmesini çalıştırıp
    audit ve AI tablolarına yazar, tam zenginleştirilmiş sonuç döner.
    """
    try:
        teklif_id = _save_teklif_to_db(teklif)
        dto = evaluate_teklif(teklif_id)
        return dto
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Teklif oluşturulup değerlendirilemedi: {e}")

@app.post("/teklifler", status_code=202)
@app.post("/teklif-olustur", status_code=202)
@app.post("/api/v1/teklifler", status_code=202)
def teklif_olustur_taslak(teklif: TeklifCreate):
    """
    Geriye dönük uyumluluk: Teklifi kaydeder.
    """
    try:
        teklif_id = _save_teklif_to_db(teklif)
        toplam_net = sum([k.miktar * k.birim_fiyat * (1.0 - (k.iskonto_orani or 0.0)) for k in teklif.kalemler])
        return {
            "status": "success",
            "message": "Teklif başarıyla kaydedildi.",
            "teklif_id": teklif_id,
            "teklifId": teklif_id,
            "bayi_kodu": teklif.bayi_kodu,
            "durum": "BEKLIYOR",
            "toplam_net_tutar": round(toplam_net, 2)
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Teklif kaydedilemedi: {e}")

@app.post("/teklifler/{teklif_id}/degerlendir")
@app.post("/api/v1/teklifler/{teklif_id}/degerlendir")
@app.post("/teklifler/{teklif_id}/yeniden-degerlendir")
@app.post("/api/v1/teklifler/{teklif_id}/yeniden-degerlendir")
def teklif_degerlendir_sync(teklif_id: str):
    """
    Teklifi anında kural motoru ve LLM ile tekrar değerlendirir.
    """
    try:
        dto = evaluate_teklif(teklif_id)
        return dto
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Teklif değerlendirilemedi: {e}")

@app.get("/teklifler")
@app.get("/api/v1/teklifler")
def teklifleri_getir(durum: Optional[str] = Query(default=None)):
    """
    Zenginleştirilmiş geçmiş teklifler listesi: Bayi bilgisi, depo, kalem sayısı,
    marj %, risk skoru ve durum rozetleri ile birlikte döner.
    """
    sql = """
        SELECT 
            t.teklif_id,
            t.bayi_kodu,
            b."BayiAdi" AS bayi_adi,
            b."Sehir" AS bayi_sehir,
            b."Bolge" AS bayi_bolge,
            t.durum,
            t.toplam_net_tutar,
            t.olusturulma_tarihi,
            t.aciklama,
            d.karar,
            d.risk_skoru,
            d.risk_seviyesi,
            d.gerekce_kodlari,
            d.bayi_marji,
            y.ai_comment,
            y.alert_message,
            (SELECT COUNT(*) FROM public.teklif_kalemleri k WHERE k.teklif_id = t.teklif_id) AS kalem_sayisi,
            (SELECT string_agg(DISTINCT k.depo_kodu, ', ') FROM public.teklif_kalemleri k WHERE k.teklif_id = t.teklif_id) AS sevk_depolari
        FROM public.teklifler t
        LEFT JOIN public.bayiler b ON t.bayi_kodu = b."BayiKodu"
        LEFT JOIN public.teklif_degerlendirme d ON t.teklif_id = d.teklif_id
        LEFT JOIN public.teklif_ai_yorumlari y ON t.teklif_id = y.teklif_id
        WHERE 1=1
    """
    params = {}
    if durum:
        sql += " AND t.durum = :durum"
        params["durum"] = durum
    sql += " ORDER BY t.olusturulma_tarihi DESC"
    try:
        kayitlar = sorgula(sql, params)
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Veri cekilemedi: {e}")
    return kayitlar

@app.get("/teklifler/{teklif_id}")
@app.get("/teklifler/{teklif_id}/detay")
@app.get("/api/v1/teklifler/{teklif_id}")
@app.get("/api/v1/teklifler/{teklif_id}/detay")
def teklif_detay_getir(teklif_id: str):
    """
    Teklif başlığı, kalemleri, audit metrikleri ve AI yorumunu içeren tam detay.
    """
    try:
        baslik = sorgula("""
            SELECT 
                t.*,
                b."BayiAdi" AS bayi_adi,
                b."Sehir" AS bayi_sehir,
                b."Bolge" AS bayi_bolge,
                d.karar,
                d.risk_skoru,
                d.risk_seviyesi,
                d.gerekce_kodlari,
                d.bayi_marji,
                d.hedef_tutturma_yuzdesi,
                d.ortalama_iskonto_orani,
                d.iade_orani,
                y.ai_comment,
                y.alert_message,
                y.is_fallback
            FROM public.teklifler t
            LEFT JOIN public.bayiler b ON t.bayi_kodu = b."BayiKodu"
            LEFT JOIN public.teklif_degerlendirme d ON t.teklif_id = d.teklif_id
            LEFT JOIN public.teklif_ai_yorumlari y ON t.teklif_id = y.teklif_id
            WHERE t.teklif_id = :id
        """, {"id": teklif_id})
        
        if not baslik:
            raise HTTPException(status_code=404, detail=f"teklif_id={teklif_id} bulunamadi")
            
        kalemler = sorgula("""
            SELECT 
                k.*,
                u."UrunAdi" AS urun_adi,
                d."DepoAdi" AS depo_adi
            FROM public.teklif_kalemleri k
            LEFT JOIN public.urunler u ON k.urun_kodu = u."UrunKodu"
            LEFT JOIN public.depolar d ON k.depo_kodu = d."DepoKodu"
            WHERE k.teklif_id = :id 
            ORDER BY k.sira_no
        """, {"id": teklif_id})
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Veri cekilemedi: {e}")
    return {**baslik[0], "kalemler": kalemler}

@app.post("/teklifler/{teklif_id}/durum")
@app.post("/api/v1/teklifler/{teklif_id}/durum")
def teklif_durum_guncelle(teklif_id: str, payload: TeklifDurumUpdate):
    """
    Yönetici onay/ret durumunu günceller.
    """
    durum_str = payload.durum.strip().upper()
    mapped_durum = "ONAYLANDI" if durum_str in ("APPROVE", "APPROVED", "ONAYLANDI") else ("REDDEDILDI" if durum_str in ("REJECT", "REJECTED", "REDDEDILDI") else durum_str)
    try:
        execute_sql("UPDATE public.teklifler SET durum = :durum WHERE teklif_id = :id", {"durum": mapped_durum, "id": teklif_id})
        execute_sql("UPDATE public.teklif_degerlendirme SET karar = :karar WHERE teklif_id = :id", {"karar": durum_str, "id": teklif_id})
        return {
            "status": "success",
            "message": f"Teklif {teklif_id} durumu '{mapped_durum}' olarak güncellendi.",
            "teklif_id": teklif_id,
            "durum": mapped_durum,
            "genel_karar": durum_str
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Teklif durumu güncellenemedi: {e}")

