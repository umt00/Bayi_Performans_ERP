import sys
import os

# Add the project root to the path so we can import the module
sys.path.append(os.path.join(os.path.dirname(__file__), '..'))

from app.ai.commenter_client import (
    build_prompt,
    validate_guardrails,
    fallback_output,
    validate_output_consistency,
    evaluate_offer
)
from app.ai import offer_evaluator

def test_build_prompt_contains_required_metrics():
    data = {
        "bayi_kodu": "B123",
        "metrikler": {
            "gerceklesen_ciro": 1000,
            "hedef_ciro": 1500,
            "hedefe_ulasma_pct": 66.0,
            "ortalama_marj_pct": 10.0,
            "ortalama_iskonto_pct": 20.0,
            "iade_orani_pct": 5.0,
            "kritik_stok_urun_sayisi": 1
        },
        "esikler": {
            "marj_esigi_pct": 15.0
        }
    }
    prompt = build_prompt(data)
    assert "B123" in prompt
    assert "1,000.00 TL" in prompt
    assert "1,500.00 TL" in prompt
    assert "%66.0" in prompt
    assert "%10.0" in prompt
    assert "%15.0" in prompt
    assert "%20.0" in prompt
    assert "%5.0" in prompt
    assert "1" in prompt

def test_validate_guardrails_rejects_forbidden():
    assert validate_guardrails("Güvenli bir yorum.") is True
    assert validate_guardrails("Burada risk_skoru değiştirildi.") is False
    assert validate_guardrails("GerekceKodlari HEDEF_ALTINDA.") is False
    assert validate_guardrails("AI olarak karar veriyorum.") is False
    assert validate_guardrails("This is my decision.") is False
    assert validate_guardrails("RiskSkoru 90.") is False

def test_fallback_output_returns_nonempty():
    fallback = fallback_output("B999")
    assert "B999" in fallback
    assert len(fallback) > 10

def test_output_consistency_accepts_valid():
    valid_text = "Bayi hedeflerine ulaşmıştır. Marj oranları sağlıklıdır. İskonto oranları kontrol altında."
    result = validate_output_consistency(valid_text)
    assert result["valid"] is True
    assert result["sentence_count"] == 3

def test_output_consistency_rejects_empty():
    result1 = validate_output_consistency("")
    assert result1["valid"] is False
    assert "Boş yanıt" in result1["issues"]

    result2 = validate_output_consistency("   ")
    assert result2["valid"] is False
    
def test_evaluate_offer_valid_input():
    valid_comment = "Bayi durumu çok iyi. Risk görünmüyor."
    valid_alert = "Uyarı yok."
    evaluation = evaluate_offer("B001", valid_comment, valid_alert, False)
    assert evaluation["bayi_kodu"] == "B001"
    assert evaluation["ai_comment"] == valid_comment
    assert evaluation["alert_message_draft"] == valid_alert
    assert evaluation["is_fallback"] is False

def test_evaluate_offer_inconsistent_comment_triggers_fallback():
    valid_alert = "Uyarı."
    evaluation = evaluate_offer("B001", "", valid_alert, False)
    assert evaluation["is_fallback"] is True
    assert evaluation["alert_message_draft"] != valid_alert
    assert len(evaluation["ai_comment"]) > 10

def test_evaluate_offer_is_fallback_true():
    valid_comment = "Bayi durumu çok iyi."
    evaluation = evaluate_offer("B001", valid_comment, "Uyarı", True)
    assert evaluation["is_fallback"] is True
    assert evaluation["ai_comment"] != valid_comment


def test_generate_ai_comment_without_groq_key_uses_fallback(monkeypatch):
    monkeypatch.delenv("GROQ_API_KEY", raising=False)
    monkeypatch.setattr(offer_evaluator, "load_dotenv", lambda *_args, **_kwargs: None)

    result = offer_evaluator.generate_ai_comment(
        {
            "bayi_kodu": "B001",
            "metrikler": {},
            "esikler": {},
            "sistem_karari": {
                "karar": "NEED_APPROVAL",
                "risk_skoru": 60,
                "gerekce_kodlari": ["MARJ_ESIK_ALTI"],
            },
        }
    )

    assert result["bayi_kodu"] == "B001"
    assert result["is_fallback"] is True
    assert result["ai_comment"]
