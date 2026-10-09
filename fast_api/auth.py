"""Supabase access token doğrulaması için ortak yardımcılar.

FastAPI, kullanıcı/parola doğrulamasını tekrar uygulamaz. .NET API'den alınan
Supabase access token'ı Supabase Auth'ın ``/auth/v1/user`` ucu üzerinden
doğrular. Böylece imza algoritması veya anahtar rotasyonu uygulamada ayrıca
yönetilmez ve yalnızca gerçekten aktif kullanıcı token'ları kabul edilir.
"""

from __future__ import annotations

import hashlib
import os
import threading
import time
from typing import Any

import requests
from fastapi import HTTPException, status


_CACHE_TTL_SECONDS = 30
_REQUEST_TIMEOUT_SECONDS = 10
_token_cache: dict[str, tuple[dict[str, Any], float]] = {}
_cache_lock = threading.Lock()


def _first_environment_value(*names: str) -> str:
    for name in names:
        value = os.getenv(name, "").strip()
        if value:
            return value
    return ""


def authentication_is_required() -> bool:
    """Kimlik doğrulamasını yalnızca açıkça kapatıldığında devre dışı bırakır."""

    value = os.getenv("REQUIRE_AUTH", "true").strip().lower()
    return value not in {"0", "false", "no", "off"}


def get_supabase_project_url() -> str:
    return _first_environment_value(
        "SUPABASE_URL",
        "SUPABASE_PROJECT_URL",
        "SupabaseAuth__ProjectUrl",
    ).rstrip("/")


def get_supabase_publishable_key() -> str:
    return _first_environment_value(
        "SUPABASE_ANON_KEY",
        "SUPABASE_PUBLISHABLE_KEY",
        "SupabaseAuth__PublishableKey",
    )


def _auth_error(status_code: int, detail: str, error_code: str) -> HTTPException:
    headers = {"WWW-Authenticate": "Bearer"} if status_code == status.HTTP_401_UNAUTHORIZED else None
    exception = HTTPException(status_code=status_code, detail=detail, headers=headers)
    exception.error_code = error_code  # type: ignore[attr-defined]
    return exception


def extract_bearer_token(authorization: str | None) -> str:
    if not authorization:
        raise _auth_error(
            status.HTTP_401_UNAUTHORIZED,
            "Gecerli bir Supabase access token gonderilmelidir.",
            "KIMLIK_DOGRULANAMADI",
        )

    scheme, separator, token = authorization.partition(" ")
    if not separator or scheme.lower() != "bearer" or not token.strip():
        raise _auth_error(
            status.HTTP_401_UNAUTHORIZED,
            "Authorization basligi 'Bearer <access_token>' biciminde olmalidir.",
            "KIMLIK_DOGRULANAMADI",
        )

    return token.strip()


def _cache_key(token: str) -> str:
    return hashlib.sha256(token.encode("utf-8")).hexdigest()


def _get_cached_user(token: str) -> dict[str, Any] | None:
    key = _cache_key(token)
    now = time.monotonic()
    with _cache_lock:
        item = _token_cache.get(key)
        if item is None:
            return None
        user, expires_at = item
        if expires_at <= now:
            del _token_cache[key]
            return None
        return user


def _cache_user(token: str, user: dict[str, Any]) -> None:
    with _cache_lock:
        _token_cache[_cache_key(token)] = (
            user,
            time.monotonic() + _CACHE_TTL_SECONDS,
        )


def clear_auth_cache() -> None:
    """Testlerde ve gerektiğinde token doğrulama önbelleğini temizler."""

    with _cache_lock:
        _token_cache.clear()


def verify_supabase_access_token(token: str) -> dict[str, Any]:
    cached_user = _get_cached_user(token)
    if cached_user is not None:
        return cached_user

    project_url = get_supabase_project_url()
    publishable_key = get_supabase_publishable_key()
    if not project_url.startswith("https://") or not publishable_key:
        raise _auth_error(
            status.HTTP_503_SERVICE_UNAVAILABLE,
            "FastAPI kimlik dogrulama ayarlari tamamlanmamis.",
            "KIMLIK_YAPILANDIRMASI_EKSIK",
        )

    try:
        response = requests.get(
            f"{project_url}/auth/v1/user",
            headers={
                "Authorization": f"Bearer {token}",
                "apikey": publishable_key,
            },
            timeout=_REQUEST_TIMEOUT_SECONDS,
        )
    except requests.RequestException as exc:
        raise _auth_error(
            status.HTTP_503_SERVICE_UNAVAILABLE,
            "Kimlik servisine su anda ulasilamiyor.",
            "KIMLIK_SERVISI_KULLANILAMIYOR",
        ) from exc

    if response.status_code == status.HTTP_200_OK:
        try:
            user = response.json()
        except ValueError as exc:
            raise _auth_error(
                status.HTTP_503_SERVICE_UNAVAILABLE,
                "Kimlik servisi gecersiz bir yanit dondurdu.",
                "KIMLIK_SERVISI_KULLANILAMIYOR",
            ) from exc

        if not isinstance(user, dict) or not user.get("id"):
            raise _auth_error(
                status.HTTP_503_SERVICE_UNAVAILABLE,
                "Kimlik servisi kullanici bilgisini dondurmedi.",
                "KIMLIK_SERVISI_KULLANILAMIYOR",
            )

        _cache_user(token, user)
        return user

    if response.status_code in {status.HTTP_401_UNAUTHORIZED, status.HTTP_403_FORBIDDEN}:
        raise _auth_error(
            status.HTTP_401_UNAUTHORIZED,
            "Access token gecersiz, suresi dolmus veya iptal edilmis.",
            "KIMLIK_DOGRULANAMADI",
        )

    raise _auth_error(
        status.HTTP_503_SERVICE_UNAVAILABLE,
        "Kimlik servisi gecici olarak kullanilamiyor.",
        "KIMLIK_SERVISI_KULLANILAMIYOR",
    )


def authenticate_authorization_header(authorization: str | None) -> dict[str, Any]:
    return verify_supabase_access_token(extract_bearer_token(authorization))
