from __future__ import annotations

from unittest.mock import Mock

import pytest
import requests
from fastapi import HTTPException
from fastapi.testclient import TestClient

import fast_api.auth as auth
import fast_api.main as fastapi_main


@pytest.fixture(autouse=True)
def clean_auth_cache():
    auth.clear_auth_cache()
    yield
    auth.clear_auth_cache()


def test_extract_bearer_token_rejects_missing_or_wrong_scheme():
    with pytest.raises(HTTPException) as missing:
        auth.extract_bearer_token(None)
    assert missing.value.status_code == 401

    with pytest.raises(HTTPException) as wrong_scheme:
        auth.extract_bearer_token("Basic abc")
    assert wrong_scheme.value.status_code == 401


def test_verify_token_fails_closed_when_configuration_is_missing(monkeypatch):
    for name in (
        "SUPABASE_URL",
        "SUPABASE_PROJECT_URL",
        "SupabaseAuth__ProjectUrl",
        "SUPABASE_ANON_KEY",
        "SUPABASE_PUBLISHABLE_KEY",
        "SupabaseAuth__PublishableKey",
    ):
        monkeypatch.delenv(name, raising=False)

    with pytest.raises(HTTPException) as result:
        auth.verify_supabase_access_token("token")

    assert result.value.status_code == 503
    assert result.value.error_code == "KIMLIK_YAPILANDIRMASI_EKSIK"


def test_verify_token_uses_supabase_user_endpoint_and_caches_user(monkeypatch):
    monkeypatch.setenv("SUPABASE_URL", "https://project.supabase.co")
    monkeypatch.setenv("SUPABASE_ANON_KEY", "publishable-key")

    response = Mock(status_code=200)
    response.json.return_value = {"id": "user-1", "email": "user@example.com"}
    get = Mock(return_value=response)
    monkeypatch.setattr(auth.requests, "get", get)

    first = auth.verify_supabase_access_token("valid-token")
    second = auth.verify_supabase_access_token("valid-token")

    assert first["id"] == "user-1"
    assert second == first
    get.assert_called_once_with(
        "https://project.supabase.co/auth/v1/user",
        headers={
            "Authorization": "Bearer valid-token",
            "apikey": "publishable-key",
        },
        timeout=10,
    )


@pytest.mark.parametrize("status_code", [401, 403])
def test_verify_token_rejects_invalid_supabase_response(monkeypatch, status_code):
    monkeypatch.setenv("SUPABASE_URL", "https://project.supabase.co")
    monkeypatch.setenv("SUPABASE_ANON_KEY", "publishable-key")
    monkeypatch.setattr(auth.requests, "get", Mock(return_value=Mock(status_code=status_code)))

    with pytest.raises(HTTPException) as result:
        auth.verify_supabase_access_token("invalid-token")

    assert result.value.status_code == 401
    assert result.value.error_code == "KIMLIK_DOGRULANAMADI"


def test_verify_token_maps_network_failure_to_service_unavailable(monkeypatch):
    monkeypatch.setenv("SUPABASE_URL", "https://project.supabase.co")
    monkeypatch.setenv("SUPABASE_ANON_KEY", "publishable-key")
    monkeypatch.setattr(
        auth.requests,
        "get",
        Mock(side_effect=requests.ConnectionError("offline")),
    )

    with pytest.raises(HTTPException) as result:
        auth.verify_supabase_access_token("token")

    assert result.value.status_code == 503
    assert result.value.error_code == "KIMLIK_SERVISI_KULLANILAMIYOR"


def test_fastapi_health_and_documentation_are_public(monkeypatch):
    monkeypatch.setenv("REQUIRE_AUTH", "true")
    monkeypatch.setattr(fastapi_main, "sorgula", lambda *_args, **_kwargs: [{"value": 1}])
    client = TestClient(fastapi_main.app)

    assert client.get("/health").status_code == 200
    readiness = client.get("/health/ready")
    assert readiness.status_code == 200
    assert readiness.json()["veritabani"] == "baglandi"
    openapi = client.get("/openapi.json")
    assert openapi.status_code == 200
    assert "BearerAuth" in openapi.json()["components"]["securitySchemes"]


def test_fastapi_readiness_reports_database_failure(monkeypatch):
    monkeypatch.setenv("REQUIRE_AUTH", "true")
    monkeypatch.setattr(
        fastapi_main,
        "sorgula",
        lambda *_args, **_kwargs: (_ for _ in ()).throw(RuntimeError("database offline")),
    )
    client = TestClient(fastapi_main.app)

    assert client.get("/health").status_code == 200
    response = client.get("/health/ready")
    assert response.status_code == 503
    assert response.json()["detail"] == "Veritabani baglantisi hazir degil."
    assert "database offline" not in response.text


def test_fastapi_protected_endpoint_requires_token(monkeypatch):
    monkeypatch.setenv("REQUIRE_AUTH", "true")
    client = TestClient(fastapi_main.app)

    response = client.get(
        "/api/v1/teklifler",
        headers={"Origin": "http://localhost:5173"},
    )

    assert response.status_code == 401
    assert response.json()["hata_kodu"] == "KIMLIK_DOGRULANAMADI"
    assert response.headers["www-authenticate"] == "Bearer"
    assert response.headers["access-control-allow-origin"] == "http://localhost:5173"


def test_fastapi_protected_endpoint_accepts_verified_token(monkeypatch):
    monkeypatch.setenv("REQUIRE_AUTH", "true")
    monkeypatch.setattr(
        fastapi_main,
        "authenticate_authorization_header",
        lambda header: {"id": "user-1"} if header == "Bearer valid-token" else None,
    )
    monkeypatch.setattr(fastapi_main, "sorgula", lambda *_args, **_kwargs: [])
    client = TestClient(fastapi_main.app)

    response = client.get(
        "/api/v1/teklifler",
        headers={"Authorization": "Bearer valid-token"},
    )

    assert response.status_code == 200
    assert response.json() == []
