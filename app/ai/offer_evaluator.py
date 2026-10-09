import os
try:
    import psycopg2
    import psycopg2.extras
except ImportError:
    psycopg2 = None
from dotenv import load_dotenv
from openai import OpenAI
import json
import uuid
import logging

try:
    from app.ai.commenter_client import build_prompt, build_alert_prompt, validate_guardrails, evaluate_offer, strip_think_block
except ModuleNotFoundError:
    from commenter_client import build_prompt, build_alert_prompt, validate_guardrails, evaluate_offer, strip_think_block

# Ortam değişkenlerini yükle
env_path = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".env"))
load_dotenv(env_path)

def load_thresholds():
    try:
        config_path = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "config", "thresholds.json"))
        if os.path.exists(config_path):
            with open(config_path, "r", encoding="utf-8") as f:
                return json.load(f)
    except Exception:
        pass
    return {
        "marj_esigi_pct": 0.15,
        "max_iskonto_pct": 0.15,
        "hedef_risk_esigi_pct": 0.80,
        "stok_gun_esigi": 7,
        "min_marj_red_pct": 0.0,
        "iade_risk_esigi_pct": 0.10,
        "risk_weights": {
            "MARJ_ESIK_ALTI": 40,
            "HEDEF_ALTINDA": 30,
            "YUKSEK_ISKONTO": 20,
            "STOK_YETERSIZ": 20,
            "YUKSEK_IADE": 20
        },
        "risk_levels": {
            "DUSUK_max": 30,
            "ORTA_max": 60,
            "YUKSEK_max": 900
        }
    }

THRESHOLDS = load_thresholds()

# Şehir / Bölgeye Göre Otomatik Sevk Deposu Eşleme Haritası (DP01-DP06 Canonical Depolar)
CITY_TO_DEPO_MAP = {
    # DP01: Ana Depo (İstanbul / Marmara)
    "İSTANBUL": "DP01", "ISTANBUL": "DP01", "KOCAELİ": "DP01", "KOCAELI": "DP01", 
    "TEKİRDAĞ": "DP01", "TEKIRDAG": "DP01", "EDİRNE": "DP01", "EDIRNE": "DP01", 
    "KIRKLARELİ": "DP01", "KIRKLARELI": "DP01", "BURSA": "DP01", "BALIKESİR": "DP01", "BALIKESIR": "DP01",
    "ÇANAKKALE": "DP01", "CANAKKALE": "DP01", "YALOVA": "DP01", "SAKARYA": "DP01", "DÜZCE": "DP01", "DUZCE": "DP01",
    "BİLECİK": "DP01", "BILECIK": "DP01", "MARMARA": "DP01",

    # DP02: Ankara Bölge Depo (İç Anadolu & Doğu Anadolu)
    "ANKARA": "DP02", "ESKİŞEHİR": "DP02", "ESKISEHIR": "DP02", "KONYA": "DP02", 
    "KAYSERİ": "DP02", "KAYSERI": "DP02", "SİVAS": "DP02", "SIVAS": "DP02", 
    "AKSARAY": "DP02", "KARAMAN": "DP02", "KIRIKKALE": "DP02", "KIRŞEHİR": "DP02", "KIRSEHIR": "DP02",
    "NEVŞEHİR": "DP02", "NEVSEHIR": "DP02", "NİĞDE": "DP02", "NIGDE": "DP02", "YOZGAT": "DP02", "ÇANKIRI": "DP02", "CANKIRI": "DP02",
    "İÇ ANADOLU": "DP02", "IC ANADOLU": "DP02", "DOĞU ANADOLU": "DP02", "DOGU ANADOLU": "DP02",
    "ERZURUM": "DP02", "MALATYA": "DP02", "ELAZIĞ": "DP02", "ELAZIG": "DP02", "VAN": "DP02", "AĞRI": "DP02", "AGRI": "DP02",

    # DP03: İzmir Bölge Depo (Ege)
    "İZMİR": "DP03", "IZMIR": "DP03", "MANİSA": "DP03", "MANISA": "DP03", 
    "AYDIN": "DP03", "DENİZLİ": "DP03", "DENIZLI": "DP03", "MUĞLA": "DP03", 
    "MUGLA": "DP03", "UŞAK": "DP03", "USAK": "DP03", "AFYON": "DP03", "KÜTAHYA": "DP03", "KUTAHYA": "DP03", "EGE": "DP03",

    # DP04: Adana Bölge Depo (Akdeniz)
    "ADANA": "DP04", "MERSİN": "DP04", "MERSIN": "DP04", "ANTALYA": "DP04", 
    "HATAY": "DP04", "OSMANİYE": "DP04", "OSMANIYE": "DP04", "KAHRAMANMARAŞ": "DP04", "KAHRAMANMARAS": "DP04",
    "BURDUR": "DP04", "ISPARTA": "DP04", "AKDENİZ": "DP04", "AKDENIZ": "DP04",

    # DP05: Samsun Bölge Depo (Karadeniz)
    "SAMSUN": "DP05", "TRABZON": "DP05", "ORDU": "DP05", "RİZE": "DP05", "RIZE": "DP05",
    "GİRESUN": "DP05", "GIRESUN": "DP05", "AMASYA": "DP05", "ARTVİN": "DP05", "ARTVIN": "DP05",
    "BARTIN": "DP05", "BOLU": "DP05", "GIRESUN": "DP05", "KARABÜK": "DP05", "KARABUK": "DP05",
    "KASTAMONU": "DP05", "SİNOP": "DP05", "SINOP": "DP05", "TOKAT": "DP05", "ZONGULDAK": "DP05", "KARADENİZ": "DP05", "KARADENIZ": "DP05",

    # DP06: Gaziantep Bölge Depo (Güneydoğu Anadolu)
    "GAZİANTEP": "DP06", "GAZIANTEP": "DP06", "ŞANLIURFA": "DP06", "SANLIURFA": "DP06",
    "DİYARBAKIR": "DP06", "DIYARBAKIR": "DP06", "MARDİN": "DP06", "MARDIN": "DP06",
    "BATMAN": "DP06", "ADIYAMAN": "DP06", "KİLİS": "DP06", "KILIS": "DP06", "SİİRT": "DP06", "SIIRT": "DP06",
    "ŞIRNAK": "DP06", "SIRNAK": "DP06", "GÜNEYDOĞU": "DP06", "GUNEYDOGU": "DP06"
}
DEFAULT_FALLBACK_DEPO = "DP01"

def get_default_warehouse(city: str = "", region: str = "") -> str:
    """
    Şehir veya bölge bilgisine göre varsayılan sevk deposunu döner (DP01-DP06).
    """
    c = (city or "").strip().upper()
    r = (region or "").strip().upper()
    
    # 1. Tam veya parçalı şehir eşleşmesi
    for key, code in CITY_TO_DEPO_MAP.items():
        if key in c:
            return code

    # 2. Bölge eşleşmesi
    for key, code in CITY_TO_DEPO_MAP.items():
        if key in r:
            return code

    return DEFAULT_FALLBACK_DEPO

def get_db_connection():
    global psycopg2
    if psycopg2 is None:
        import psycopg2
        import psycopg2.extras
    db_url = os.getenv("SUPABASE_DB_URL", "postgresql://airflow:airflow@localhost:5432/airflow")
    return psycopg2.connect(db_url, cursor_factory=psycopg2.extras.DictCursor)

def generate_ai_comment(ai_payload: dict):
    """
    LLM'i çağırır ve AI yorumunu üretir.
    ai_payload: bayi_kodu, metrikler, esikler, sistem_karari içeren veri sözlüğü.
    """
    bayi_kodu = ai_payload.get("bayi_kodu", "UNKNOWN")
    prompt = build_prompt(ai_payload)
    alert_prompt = build_alert_prompt(ai_payload)

    load_dotenv(env_path, override=True)
    api_key = (os.getenv("GROQ_API_KEY") or "").strip('"').strip("'").strip()
    base_url = (
        os.getenv("GROQ_BASE_URL") or "https://api.groq.com/openai/v1"
    ).strip('"').strip("'").strip()
    model = (
        os.getenv("GROQ_MODEL") or "openai/gpt-oss-120b"
    ).strip('"').strip("'").strip()

    ai_comment = ""
    alert_message = ""
    is_fallback = False
    
    if api_key and api_key != "your_groq_api_key_here":
        try:
            client = OpenAI(api_key=api_key, base_url=base_url)
            sys_msg = {
                "role": "system", 
                "content": "Sen kıdemli bir B2B satış yöneticisi asistanısın. SADECE nihai Türkçe yönetici özetini yaz. Kesinlikle <think> etiketi veya iç düşünme süreci kullanma. Asla İngilizce yazma. Verilen metrikleri (ciro, marj, iskonto, stok) bağlamında doğrudan analiz et."
            }
            
            resp1 = client.chat.completions.create(
                model=model,
                messages=[sys_msg, {"role": "user", "content": prompt}],
                temperature=0.3,
                max_tokens=1024
            )
            ai_comment = strip_think_block(resp1.choices[0].message.content)
            
            resp2 = client.chat.completions.create(
                model=model,
                messages=[sys_msg, {"role": "user", "content": alert_prompt}],
                temperature=0.3,
                max_tokens=1024
            )
            alert_message = strip_think_block(resp2.choices[0].message.content)
        except Exception:
            logging.exception(
                "[AI_EVALUATOR] LLM API cagrisi basarisiz oldu. "
                "Fallback mekanizmasi devreye giriyor."
            )
            is_fallback = True
    else:
        logging.warning("[AI_EVALUATOR] GROQ_API_KEY bulunamadı veya geçersiz. Fallback yorumlar kullanılacak.")
        is_fallback = True
        
    if not is_fallback:
        if not validate_guardrails(ai_comment):
            logging.warning(f"[AI_GUARDRAIL_FAILED] ai_comment yasaklı kelime içeriyor: {ai_comment}")
            is_fallback = True
        elif not validate_guardrails(alert_message):
            logging.warning(f"[AI_GUARDRAIL_FAILED] alert_message yasaklı kelime içeriyor: {alert_message}")
            is_fallback = True
            
    return evaluate_offer(bayi_kodu, ai_comment, alert_message, is_fallback)

def evaluate_teklif(teklif_id: str) -> dict:
    """
    Teklifi veritabanından ROW LOCK (SELECT FOR UPDATE) ile okur.
    - Şirket depolarındaki dinamik stok çıkış hızını ve kalan gün sayısını (vw_dinamik_stok_esigi) kontrol eder.
    - Gizli birim maliyetleri (BirimMaliyet) çekerek zararına satış (Net Fiyat < Maliyet) kontrolü yapar.
    - Değerlendirme sonucunu `teklif_degerlendirme` audit tablosuna kaydeder.
    - LLM yönetici özetini `teklif_ai_yorumlari` tablosuna yazar.
    - Teklif durumunu günceller: REJECTED -> REDDEDILDI, NEED_APPROVAL -> BEKLIYOR, APPROVED -> ONAYLANDI.
    """
    conn = get_db_connection()
    cursor = conn.cursor()
    
    try:
        # 1. Teklif Başlığını Oku (Row Lock)
        cursor.execute("SELECT * FROM public.teklifler WHERE teklif_id = %s FOR UPDATE", (teklif_id,))
        teklif = cursor.fetchone()
        
        if not teklif:
            raise ValueError(f"Teklif ({teklif_id}) bulunamadı.")

        bayi_kodu = teklif["bayi_kodu"]

        # Bayi Bilgisi
        cursor.execute('SELECT "BayiKodu", "BayiAdi", "Sehir", "Bolge", "Segment" FROM bayiler WHERE "BayiKodu" = %s', (bayi_kodu,))
        bayi_bilgi = cursor.fetchone()
        bayi_adi = bayi_bilgi["BayiAdi"] if bayi_bilgi else bayi_kodu
        bayi_sehir = bayi_bilgi["Sehir"] if bayi_bilgi else ""
        bayi_bolge = bayi_bilgi["Bolge"] if bayi_bilgi else ""

        # 2. Kalemleri Oku
        cursor.execute("SELECT * FROM public.teklif_kalemleri WHERE teklif_id = %s ORDER BY sira_no", (teklif_id,))
        kalemler = cursor.fetchall()

        if not kalemler:
            raise ValueError(f"Teklif ({teklif_id}) icerisinde hicbir urun kalemi bulunamadi.")

        # 3. İlgili Ürünlerin Gerçek Birim Maliyetlerini Çek
        urun_kodlari = list(set([k["urun_kodu"] for k in kalemler]))
        urun_maliyetleri = {}
        urun_adlari = {}
        if urun_kodlari:
            format_strings = ','.join(['%s'] * len(urun_kodlari))
            cursor.execute(f'SELECT "UrunKodu", "UrunAdi", "BirimMaliyet" FROM urunler WHERE "UrunKodu" IN ({format_strings})', tuple(urun_kodlari))
            for u in cursor.fetchall():
                urun_maliyetleri[u["UrunKodu"]] = float(u["BirimMaliyet"]) if u["BirimMaliyet"] is not None else 0.0
                urun_adlari[u["UrunKodu"]] = u["UrunAdi"]

        # 4. İlgili Depo Stoklarını ve Dinamik Stok Eşiklerini Çek (vw_dinamik_stok_esigi)
        depo_stok_haritasi = {}
        depo_kalan_gun_haritasi = {}
        depo_adlari = {}
        depo_kodlari = list(set([k["depo_kodu"] for k in kalemler]))

        # Depo İsimleri
        if depo_kodlari:
            format_d = ','.join(['%s'] * len(depo_kodlari))
            cursor.execute(f'SELECT "DepoKodu", "DepoAdi" FROM depolar WHERE "DepoKodu" IN ({format_d})', tuple(depo_kodlari))
            for d in cursor.fetchall():
                depo_adlari[d["DepoKodu"]] = d["DepoAdi"]

        # Dinamik Stok Bilgisi
        if urun_kodlari and depo_kodlari:
            format_u = ','.join(['%s'] * len(urun_kodlari))
            format_d = ','.join(['%s'] * len(depo_kodlari))
            try:
                cursor.execute(f'''
                    SELECT "UrunKodu", "DepoKodu", "MevcutStok", "KalanStokGunu" 
                    FROM vw_dinamik_stok_esigi 
                    WHERE "UrunKodu" IN ({format_u}) AND "DepoKodu" IN ({format_d})
                ''', tuple(urun_kodlari + depo_kodlari))
                for s in cursor.fetchall():
                    depo_stok_haritasi[(s["UrunKodu"], s["DepoKodu"])] = float(s["MevcutStok"] or 0)
                    depo_kalan_gun_haritasi[(s["UrunKodu"], s["DepoKodu"])] = float(s["KalanStokGunu"] or 999.0)
            except Exception:
                # Fallback: doğrudan stok tablosu
                cursor.execute(f'''
                    SELECT "UrunKodu", "DepoKodu", "MevcutStok" 
                    FROM stok 
                    WHERE "UrunKodu" IN ({format_u}) AND "DepoKodu" IN ({format_d})
                ''', tuple(urun_kodlari + depo_kodlari))
                for s in cursor.fetchall():
                    depo_stok_haritasi[(s["UrunKodu"], s["DepoKodu"])] = float(s["MevcutStok"] or 0)
                    depo_kalan_gun_haritasi[(s["UrunKodu"], s["DepoKodu"])] = 999.0

        # 5. Bayinin En Güncel Performans Karnesini Çek (bayi_donem_performans)
        cursor.execute("""
            SELECT * FROM public.bayi_donem_performans 
            WHERE bayi_kodu = %s 
            ORDER BY donem DESC LIMIT 1
        """, (bayi_kodu,))
        perf = cursor.fetchone()

        # 6. Kalem Bazlı Gerçek Kural Motoru Denetimi
        kalem_sonuclari = []
        has_reject = False
        has_need_approval = False
        toplam_net_hesaplanan = 0.0
        toplam_maliyet_hesaplanan = 0.0

        for k in kalemler:
            urun_kodu = k["urun_kodu"]
            depo_kodu = k["depo_kodu"]
            miktar = float(k["miktar"])
            birim_fiyat = float(k["birim_fiyat"])
            iskonto_orani = float(k["iskonto_orani"])
            
            # Gerçek maliyet
            birim_maliyet = float(urun_maliyetleri.get(urun_kodu, 0.0))
            
            net_birim_fiyat = round(birim_fiyat * (1.0 - iskonto_orani), 2)
            kalem_net_tutar = round(miktar * net_birim_fiyat, 2)
            kalem_maliyet = round(miktar * birim_maliyet, 2)
            
            toplam_net_hesaplanan += kalem_net_tutar
            toplam_maliyet_hesaplanan += kalem_maliyet
            
            # Kalem Marjı (%)
            if net_birim_fiyat > 0:
                hesaplanan_marj_pct = round(((net_birim_fiyat - birim_maliyet) / net_birim_fiyat) * 100.0, 2)
            else:
                hesaplanan_marj_pct = -100.0

            # Kural Tabanlı Karar Hiyerarşisi (thresholds.json kurallarına tam uyumlu)
            gerekce_kodlari = []
            marj_esigi = THRESHOLDS.get("marj_esigi_pct", 0.15) * 100.0
            max_iskonto = THRESHOLDS.get("max_iskonto_pct", 0.15)
            stok_gun_esigi = float(THRESHOLDS.get("stok_gun_esigi", 7))
            min_marj_red = THRESHOLDS.get("min_marj_red_pct", 0.0) * 100.0
            
            # 1. ZARARINA SATIŞ KONTROLÜ (KRİTİK GÜVENLİK)
            if net_birim_fiyat < birim_maliyet or hesaplanan_marj_pct <= min_marj_red:
                karar = "REJECTED"
                gerekce_kodlari.append("ZARARINA_SATIS")
                has_reject = True
            # 2. MARJ EŞİK ALTI (< %15)
            elif hesaplanan_marj_pct < marj_esigi:
                karar = "NEED_APPROVAL"
                gerekce_kodlari.append("MARJ_ESIK_ALTI")
                has_need_approval = True
            # 3. MARJ UYGUN (>= %15)
            else:
                karar = "APPROVED"
                gerekce_kodlari.append("MARJ_UYGUN")

            # 4. YÜKSEK İSKONTO KONTROLÜ (> %15)
            if iskonto_orani > max_iskonto:
                gerekce_kodlari.append("YUKSEK_ISKONTO")
                if karar == "APPROVED":
                    karar = "NEED_APPROVAL"
                    has_need_approval = True

            # 5. DEPO STOK YETERLİLİK KONTROLÜ
            mevcut_depo_stok = depo_stok_haritasi.get((urun_kodu, depo_kodu), 0.0)
            if mevcut_depo_stok < miktar:
                gerekce_kodlari.append("STOK_YETERSIZ")
                if karar == "APPROVED":
                    karar = "NEED_APPROVAL"
                    has_need_approval = True

            # 6. DEPO KALAN GÜN SAYISI KONTROLÜ (< 7 gün kritik depo stoku)
            kalan_stok_gunu = depo_kalan_gun_haritasi.get((urun_kodu, depo_kodu), 999.0)
            if kalan_stok_gunu < stok_gun_esigi:
                gerekce_kodlari.append("KRITIK_DEPO_STOK")
                gerekce_kodlari.append("STOK_GUNU_KRITIK")
                if karar == "APPROVED":
                    karar = "NEED_APPROVAL"
                    has_need_approval = True

            kalem_sonuclari.append({
                "sira_no": k["sira_no"],
                "siraNo": k["sira_no"],
                "urun_kodu": urun_kodu,
                "urunKodu": urun_kodu,
                "urun_adi": urun_adlari.get(urun_kodu, urun_kodu),
                "urunAdi": urun_adlari.get(urun_kodu, urun_kodu),
                "depo_kodu": depo_kodu,
                "depoKodu": depo_kodu,
                "depo_adi": depo_adlari.get(depo_kodu, depo_kodu),
                "depoAdi": depo_adlari.get(depo_kodu, depo_kodu),
                "miktar": miktar,
                "birim_fiyat": birim_fiyat,
                "birimFiyat": birim_fiyat,
                "iskonto_orani": iskonto_orani,
                "iskontoOrani": iskonto_orani,
                "net_birim_fiyat": net_birim_fiyat,
                "netBirimFiyat": net_birim_fiyat,
                "net_tutar": kalem_net_tutar,
                "netTutar": kalem_net_tutar,
                "birim_maliyet": birim_maliyet,
                "mevcut_depo_stok": mevcut_depo_stok,
                "mevcutDepoStok": mevcut_depo_stok,
                "kalan_stok_gunu": kalan_stok_gunu,
                "kalanStokGunu": kalan_stok_gunu,
                "hesaplanan_marj_pct": hesaplanan_marj_pct,
                "hesaplananMarjPct": hesaplanan_marj_pct,
                "karar": karar,
                "gerekce_kodlari": gerekce_kodlari,
                "gerekceKodlari": gerekce_kodlari
            })

        # 7. Genel Teklif Marjı ve Nihai Karar Hiyerarşisi
        if toplam_net_hesaplanan > 0:
            genel_marj_pct = round(((toplam_net_hesaplanan - toplam_maliyet_hesaplanan) / toplam_net_hesaplanan) * 100.0, 2)
        else:
            genel_marj_pct = 0.0

        if has_reject:
            genel_karar = "REJECTED"
            teklif_durum = "REDDEDILDI"
        elif has_need_approval:
            genel_karar = "NEED_APPROVAL"
            teklif_durum = "BEKLIYOR"
        else:
            genel_karar = "APPROVED"
            teklif_durum = "ONAYLANDI"

        tum_gerekceler = list(set([g for k in kalem_sonuclari for g in k["gerekce_kodlari"]]))

        # 8. Bayi Performans Metrikleri
        bayi_marji = float(perf["ortalama_kar_marji"]) if perf and perf["ortalama_kar_marji"] is not None else genel_marj_pct
        hedef_tutma = float(perf["hedef_tutma_orani"]) if perf and perf["hedef_tutma_orani"] is not None else 100.0
        iskonto_ort = float(perf["ortalama_iskonto_orani"]) if perf and perf["ortalama_iskonto_orani"] is not None else 0.0
        iade_ort = float(perf["ortalama_iade_orani"]) if perf and perf["ortalama_iade_orani"] is not None else 0.0
        risk_skoru = int(perf["risk_skoru"]) if perf and perf["risk_skoru"] is not None else (70 if has_reject else (40 if has_need_approval else 10))
        risk_seviyesi = perf["risk_seviyesi"] if perf and perf["risk_seviyesi"] else ("YUKSEK" if has_reject else ("ORTA" if has_need_approval else "DUSUK"))

        # 9. `teklif_degerlendirme` AUDIT SNAPSHOT LOG TABLOSUNA YAZ
        cursor.execute("""
            INSERT INTO public.teklif_degerlendirme (
                teklif_id, bayi_kodu, karar, risk_skoru, risk_seviyesi,
                gerekce_kodlari, bayi_marji, hedef_tutturma_yuzdesi,
                ortalama_iskonto_orani, iade_orani, degerlendirme_tarihi
            ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, NOW())
            ON CONFLICT (teklif_id) DO UPDATE SET
                karar = EXCLUDED.karar,
                risk_skoru = EXCLUDED.risk_skoru,
                risk_seviyesi = EXCLUDED.risk_seviyesi,
                gerekce_kodlari = EXCLUDED.gerekce_kodlari,
                bayi_marji = EXCLUDED.bayi_marji,
                hedef_tutturma_yuzdesi = EXCLUDED.hedef_tutturma_yuzdesi,
                ortalama_iskonto_orani = EXCLUDED.ortalama_iskonto_orani,
                iade_orani = EXCLUDED.iade_orani,
                degerlendirme_tarihi = NOW();
        """, (
            teklif_id, bayi_kodu, genel_karar, risk_skoru, risk_seviyesi,
            tum_gerekceler, bayi_marji, hedef_tutma, iskonto_ort, iade_ort
        ))

        # 10. `teklifler` BAŞLIĞINI GÜNCELLE
        cursor.execute("""
            UPDATE public.teklifler 
            SET durum = %s, toplam_net_tutar = %s 
            WHERE teklif_id = %s
        """, (teklif_durum, round(toplam_net_hesaplanan, 2), teklif_id))

        # 11. Yapay Zeka İçin Payload Hazırla
        ai_payload = {
            "bayi_kodu": bayi_kodu,
            "metrikler": {
                "gerceklesen_ciro": float(perf["ciro"] or 0) if perf else 0,
                "hedef_ciro": float(perf["hedef"] or 1) if perf else 1,
                "hedefe_ulasma_pct": hedef_tutma,
                "ortalama_marj_pct": bayi_marji,
                "ortalama_iskonto_pct": iskonto_ort,
                "iade_orani_pct": iade_ort,
                "kritik_stok_urun_sayisi": int(perf["kritik_stok_kalan_urun_sayisi"] or 0) if perf else 0
            },
            "esikler": {"marj_esigi_pct": 15.0},
            "sistem_karari": {
                "risk_skoru": risk_skoru,
                "gerekce_kodlari": tum_gerekceler,
                "risk_seviyesi": risk_seviyesi,
                "hesaplanan_karar": genel_karar
            },
            "teklif_ozeti": {
                "teklif_id": teklif_id,
                "toplam_net_tutar": round(toplam_net_hesaplanan, 2),
                "teklif_marji_pct": genel_marj_pct,
                "kalem_sayisi": len(kalem_sonuclari)
            }
        }

        ai_result = generate_ai_comment(ai_payload)

        # 12. `teklif_ai_yorumlari` TABLOSUNA YAZ (1:1 TEKLIF_ID PK)
        cursor.execute("""
            INSERT INTO public.teklif_ai_yorumlari (teklif_id, ai_comment, alert_message, is_fallback, created_time)
            VALUES (%s, %s, %s, %s, NOW())
            ON CONFLICT (teklif_id) DO UPDATE SET
                ai_comment = EXCLUDED.ai_comment,
                alert_message = EXCLUDED.alert_message,
                is_fallback = EXCLUDED.is_fallback,
                created_time = NOW();
        """, (teklif_id, ai_result["ai_comment"], ai_result["alert_message_draft"], ai_result["is_fallback"]))

        conn.commit()

        # 13. UI & API Response DTO (camelCase + snake_case destekli)
        dto = {
            "teklif_id": teklif_id,
            "teklifId": teklif_id,
            "bayi_kodu": bayi_kodu,
            "bayiKodu": bayi_kodu,
            "bayi_adi": bayi_adi,
            "bayiAdi": bayi_adi,
            "bayi_sehir": bayi_sehir,
            "bayi_bolge": bayi_bolge,
            "durum": teklif_durum,
            "genel_karar": genel_karar,
            "genelKarar": genel_karar,
            "genel_marj_pct": genel_marj_pct,
            "genelMarjPct": genel_marj_pct,
            "toplam_net_tutar": round(toplam_net_hesaplanan, 2),
            "toplamNetTutar": round(toplam_net_hesaplanan, 2),
            "kalem_sonuclari": kalem_sonuclari,
            "kalemSonuclari": kalem_sonuclari,
            "kalemler": kalem_sonuclari,
            "gerekce_kodlari": tum_gerekceler,
            "gerekceKodlari": tum_gerekceler,
            "risk_skoru": risk_skoru,
            "riskSkoru": risk_skoru,
            "risk_seviyesi": risk_seviyesi,
            "riskSeviyesi": risk_seviyesi,
            "bayi_marji": bayi_marji,
            "hedef_tutturma_yuzdesi": hedef_tutma,
            "ortalama_iskonto_orani": iskonto_ort,
            "iade_orani": iade_ort,
            "ai_comment": ai_result["ai_comment"],
            "aiComment": ai_result["ai_comment"],
            "alert_message": ai_result["alert_message_draft"],
            "alertMessage": ai_result["alert_message_draft"],
            "is_fallback": ai_result["is_fallback"],
            "isFallback": ai_result["is_fallback"]
        }

        return dto

    except Exception as e:
        conn.rollback()
        raise e
    finally:
        cursor.close()
        conn.close()
