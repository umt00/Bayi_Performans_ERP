# Bayi Performans React UI

Vite + React yönetim portalı iki ayrı backend kullanır:

- `.NET API`: kayıt, login, OTP, kullanıcı, bayi, stok ve performans uçları.
- `FastAPI`: teklif oluşturma/değerlendirme, geçmiş ve durum uçları.

## Ortam ayarları

`.env.example` dosyasını `.env.local` olarak kopyalayın ve yalnız kendi ortam
adreslerinizi girin:

```dotenv
VITE_API_BASE_URL=https://bayi-performans-api.onrender.com
VITE_FASTAPI_BASE_URL=https://bayi-performans-fastapi.onrender.com
```

Anahtar, parola veya access token Vite ortam değişkenine yazılmaz. Login sonrası
alınan Supabase access token her iki backend isteğine de `Authorization: Bearer`
başlığıyla eklenir. UI, canlı API hatalarını mock sonuçla gizlemez.

## Yerel çalışma ve doğrulama

```powershell
npm ci
npm run dev
npm run lint
npm run build
```

Yerel adres varsayılan olarak `http://localhost:5173` olur. Backend CORS
ayarlarında bu origin bulunmalıdır.

## Vercel

- Root Directory: `app/BayiPerformansAppWeb/UI`
- Framework Preset: Vite
- Production ortam değişkenleri: `VITE_API_BASE_URL` ve
  `VITE_FASTAPI_BASE_URL`
- `vercel.json`, SPA sayfa yenilemelerini `index.html` dosyasına yönlendirir.

Canlı yayın sonrası ağ sekmesinde kimlik/katalog çağrılarının .NET adresine,
teklif çağrılarının FastAPI adresine gittiğini kontrol edin.
