import re
import os
from typing import Dict

def load_prompt(prompt_name: str) -> str:
    """
    ai/prompts klasöründeki txt dosyasından prompt şablonunu okur.
    """
    prompt_path = os.path.join(os.path.dirname(__file__), "prompts", f"{prompt_name}.txt")
    try:
        with open(prompt_path, "r", encoding="utf-8") as f:
            return f.read()
    except Exception as e:
        print(f"Error loading prompt {prompt_name}: {e}")
        return ""

def format_pct(val, default=0.0) -> float:
    if val is None:
        return default
    try:
        f = float(val)
        return round(f, 1)
    except Exception:
        return default

def format_currency(val) -> str:
    if val is None:
        return "0.00 TL"
    try:
        f = float(val)
        return f"{f:,.2f} TL"
    except Exception:
        return f"{val} TL"

def build_prompt(data: Dict) -> str:
    """
    JSON verisinden LLM'ye gönderilecek yapılandırılmış prompt oluşturur.
    performance_comment.txt dosyasını kullanır.
    """
    template = load_prompt("performance_comment")
    
    metrikler = data.get("metrikler", {})
    esikler = data.get("esikler", {})

    # Güvenlik için bayi adı kullanılmaz, yalnızca kod
    prompt = template.format(
        bayi_kodu=data.get("bayi_kodu", "UNKNOWN"),
        gerceklesen_ciro=format_currency(metrikler.get("gerceklesen_ciro", 0)),
        hedef_ciro=format_currency(metrikler.get("hedef_ciro", 0)),
        hedefe_ulasma_pct=format_pct(metrikler.get("hedefe_ulasma_pct", 0)),
        ortalama_marj_pct=format_pct(metrikler.get("ortalama_marj_pct", 0)),
        marj_esigi_pct=format_pct(esikler.get("marj_esigi_pct", 15.0)),
        ortalama_iskonto_pct=format_pct(metrikler.get("ortalama_iskonto_pct", 0)),
        iade_orani_pct=format_pct(metrikler.get("iade_orani_pct", 0)),
        kritik_stok_urun_sayisi=metrikler.get("kritik_stok_urun_sayisi", 0)
    )
    return prompt

def build_alert_prompt(data: Dict) -> str:
    """
    JSON verisinden LLM'ye gönderilecek uyarı mesajı prompt'u oluşturur.
    alert_message.txt dosyasını kullanır.
    """
    template = load_prompt("alert_message")
    
    metrikler = data.get("metrikler", {})
    esikler = data.get("esikler", {})
    sistem_karari = data.get("sistem_karari", {})

    prompt = template.format(
        bayi_kodu=data.get("bayi_kodu", "UNKNOWN"),
        risk_skoru=sistem_karari.get("risk_skoru", 0),
        gerekce_kodlari=", ".join(sistem_karari.get("gerekce_kodlari", [])) if sistem_karari.get("gerekce_kodlari") else "YOK",
        ortalama_marj_pct=format_pct(metrikler.get("ortalama_marj_pct", 0)),
        marj_esigi_pct=format_pct(esikler.get("marj_esigi_pct", 15.0)),
        ortalama_iskonto_pct=format_pct(metrikler.get("ortalama_iskonto_pct", 0))
    )
    return prompt

def strip_think_block(text: str) -> str:
    """
    Eğer LLM bir 'reasoning' modeli (Qwen/Deepseek R1) ise, 
    çıktısındaki <think>...</think> bloklarını temizler ve unicode boşlukları normalize eder.
    """
    if not text:
        return ""
    # <think> ile başlayıp </think> ile biten kısımları siler
    cleaned = re.sub(r'<think>.*?</think>', '', text, flags=re.DOTALL)
    # Unicode narrow spaces (\u202f, \xa0 vb.) normal boşluğa çevir
    cleaned = re.sub(r'[\u202f\u00a0\u2000-\u200b]', ' ', cleaned)
    # Unicode hyphens (\u2010-\u2015) normal tireye çevir
    cleaned = re.sub(r'[\u2010-\u2015]', '-', cleaned)
    return cleaned.strip()

def validate_guardrails(response: str) -> bool:
    """
    AI çıktısının yasaklı pattern'leri (risk_skoru vb.) içerip içermediğini kontrol eder.
    True: Güvenli (yasaklı kelime yok)
    False: İhlal (yasaklı kelime var)
    """
    forbidden_patterns = [
        r"RiskSkoru", r"risk_skoru", r"risk\s*skoru",
        r"GerekceKodlari", r"gerekce_kodlari", r"gerekçe\s*kodları", r"gerekce\s*kodlari",
        r"\bkarar\b", r"\bdecision\b"
    ]
    for pattern in forbidden_patterns:
        if re.search(pattern, response, re.IGNORECASE):
            return False
    return True

def fallback_output(bayi_kodu: str) -> str:
    """
    Hata veya güvenlik ihlali durumunda dönülecek güvenli, şablon tabanlı metin.
    """
    return f"{bayi_kodu} kodlu bayi için mevcut performans verileri incelenmiştir; hedefe ulaşım ve marj baskısı göz önünde bulundurulmalıdır."

def validate_output_consistency(response: str) -> Dict:
    """
    AI çıktısının tutarlılığını 4 kritere göre kontrol eder:
    - Boş mu?
    - 3 cümleden uzun mu?
    - Türkçe karakter içeriyor mu?
    - 500 karakterden uzun mu?
    """
    if not response or len(response.strip()) == 0:
        return {"valid": False, "issues": ["Boş yanıt"], "sentence_count": 0, "length": 0}
        
    length = len(response)
    if length > 500:
        return {"valid": False, "issues": ["Çok uzun (>500 karakter)"], "sentence_count": 0, "length": length}
        
    sentences = re.split(r'[.!?]+(?=\s|$)', response)
    sentence_count = len([s for s in sentences if len(s.strip()) > 0])
    
    if sentence_count > 3:
        return {"valid": False, "issues": ["3 cümleden uzun"], "sentence_count": sentence_count, "length": length}
        
    return {"valid": True, "issues": [], "sentence_count": sentence_count, "length": length}

def evaluate_offer(bayi_kodu: str, ai_comment: str, alert_message: str, is_fallback: bool = False) -> Dict:
    """
    İstenilen JSON sözleşmesine (DTO) uygun çıktıyı üretir.
    Eğer yorum tutarsız ise fallback moduna geçer.
    """
    consistency = validate_output_consistency(ai_comment)
    
    if not consistency["valid"] or is_fallback:
        import logging
        if not consistency["valid"]:
            logging.warning(f"[AI_VALIDATION_FAILED] LLM Yanıtı kuralları ihlal etti: {consistency['issues']} | Uzunluk: {consistency['length']} | Cümle: {consistency['sentence_count']}")
            logging.warning(f"[AI_VALIDATION_FAILED] Orijinal Yanıt: {ai_comment}")
        return {
            "bayi_kodu": bayi_kodu,
            "ai_comment": fallback_output(bayi_kodu),
            "alert_message_draft": f"{bayi_kodu} için risk durumu incelenmelidir.",
            "is_fallback": True
        }
        
    return {
        "bayi_kodu": bayi_kodu,
        "ai_comment": ai_comment,
        "alert_message_draft": alert_message,
        "is_fallback": False
    }
