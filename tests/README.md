# Python testleri

Bu klasör FastAPI kimlik doğrulaması ile AI yorum istemcisinin birim ve hafif
entegrasyon testlerini içerir.

Proje kökünden çalıştırma:

```powershell
python -m pytest tests -q
```

`test_fastapi_auth.py` gerçek anahtar veya parola kullanmaz. Supabase Auth
yanıtlarını taklit ederek şu davranışları doğrular:

- Sağlık ve OpenAPI uçları token istemez.
- Diğer FastAPI uçları Bearer token olmadan `401` döndürür.
- Geçersiz token `401`, eksik ayar veya erişilemeyen kimlik servisi `503` olur.
- Geçerli token Supabase `/auth/v1/user` yanıtıyla doğrulanır.
