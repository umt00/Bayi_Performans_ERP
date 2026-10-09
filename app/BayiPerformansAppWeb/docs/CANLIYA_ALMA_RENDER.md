# Backend servislerini Render üzerinde çalıştırma

Bu belge ASP.NET Core API, OTP Redis ve FastAPI teklif servisini kapsar.
React/Vercel yayını aynı repository'nin UI klasöründen yapılır. Veritabanı
Supabase'de kalır; Render yeni bir PostgreSQL veritabanı oluşturmaz.

## Hazır altyapı

- `Dockerfile.api`, .NET 8 API'yi çok aşamalı Docker build ile yayınlar.
- API Render'ın verdiği `PORT` değerine ve `0.0.0.0` adresine bağlanır.
- `render.yaml`, iki web service ile dış erişime kapalı bir Render Key Value
  (Redis/Valkey) servisi tanımlar.
- Swagger production'da yalnız demo için konfigürasyonla açılır.
- API içindeki eski skor background job'u kapalıdır. Aylık performans
  sonuçlarını Data/Automation üretir; API hazır kayıtları okur.
- Production'da Redis kullanılamazsa OTP bellek deposuna sessizce düşmez.

## Render Blueprint oluşturma

1. Bu branch ortak entegrasyon branch'ine review edilerek alındıktan sonra
   Render Dashboard'da **New > Blueprint** seçin.
2. GitHub repository'yi seçin. Render kökteki `render.yaml` dosyasını okur.
3. İlk oluşturma ekranında `sync: false` olan değerleri girin:

| Değişken | Değer |
| --- | --- |
| `Database__ConnectionString` | Supabase **Transaction Pooler** Npgsql bağlantı dizesi (`Port=6543`). `SSL Mode=VerifyFull` kullanın; Windows sertifika yolu eklemeyin. |
| `SupabaseAuth__ProjectUrl` | `https://<project-ref>.supabase.co` |
| `SupabaseAuth__PublishableKey` | Supabase publishable key; secret/service_role anahtarı değil |
| `Cors__AllowedOrigins__0` | Vercel'in tam HTTPS origin'i; sonunda `/` olmadan |
| `BrevoEmail__ApiKey` | Brevo HTTP API key |
| `BrevoEmail__FromEmail` | Brevo'da doğrulanmış gönderici e-postası |

FastAPI servisi için ayrıca:

| Değişken | Değer |
| --- | --- |
| `SUPABASE_DB_URL` | SQLAlchemy uyumlu Supabase pooler URL'si; Git'e yazılmaz |
| `REQUIRE_AUTH` | Production'da kesinlikle `true` |
| `SUPABASE_URL` | `https://<project-ref>.supabase.co` |
| `SUPABASE_ANON_KEY` | Publishable/anon anahtar; secret veya service_role değil |
| `ALLOWED_ORIGINS` | Virgülle ayrılmış kesin UI origin'leri; `*` kullanmayın |
| `GROQ_API_KEY` | AI yorum sağlayıcısının gizli anahtarı |

Bu değerleri `appsettings.json`, Dockerfile veya GitHub'a yazmayın.

API ayrıca `Database:MaximumPoolSize=5`, `MinimumPoolSize=0` ve
`ConnectionIdleLifetimeSeconds=60` varsayılanlarını uygular. Böylece .NET API,
Supabase bağlantı kotasının tamamını tek başına tüketmez. FastAPI veya Airflow
gibi diğer servisler de kendi havuzlarını ayrıca sınırlamalıdır.

## Supabase CA sertifikası

API `VerifyFull` kullanır. Supabase Dashboard'da **Database Settings > SSL
Configuration** bölümünden `prod-ca-2021.crt` dosyasını indirin. Render web
service'in **Environment > Secret Files** bölümünde:

- Filename: `prod-ca-2021.crt`
- Contents: indirilen sertifika dosyasının içeriği

olarak ekleyin. Render dosyayı `/etc/secrets/prod-ca-2021.crt` yolunda sunar;
`render.yaml` içindeki `PGSSLROOTCERT` bu yolu kullanır.

## İlk doğrulama

Deploy tamamlandıktan sonra sırasıyla:

1. `https://<service>.onrender.com/health` çağrısı `200` dönmeli.
2. `https://<service>.onrender.com/swagger` açılmalı.
3. Kayıt veya ilk login isteği e-postaya OTP göndermeli.
4. OTP doğrulama tokenı yalnız bir kez kullanılabilmeli.
5. JWT olmadan iş endpoint'i `401`, geçerli JWT ile performans endpoint'i canlı
   Supabase verisini dönmeli.
6. FastAPI `https://<fastapi-service>.onrender.com/health` çağrısı proses
   liveness sonucu olarak `200` dönmeli.
7. FastAPI `https://<fastapi-service>.onrender.com/health/ready` çağrısı
   PostgreSQL bağlantısıyla birlikte `200` dönmeli. Buradaki `503`, deploy
   hatası değil eksik/yanlış `SUPABASE_DB_URL` yapılandırmasıdır.
8. FastAPI `/docs` sayfasında **Authorize** ile aynı access token girildikten
   sonra teklif uçları çalışmalı; tokensız çağrı `401` dönmeli.

`/health` başarısızsa önce Render logunda seçenek doğrulama hatasına bakın.
En sık nedenler eksik CA secret file, yanlış Supabase connection string veya
Brevo göndericisinin doğrulanmamış olmasıdır.

## UI teslimatı gelmeden önce

API tek başına yayınlanabilir ve Swagger'dan test edilebilir. Tam kullanıcı
akışı için UI tarafında şunlar tamamlanmalıdır:

- Vite istekleri `VITE_API_BASE_URL` üzerinden Render API'ye gitmeli.
- Teklif istekleri `VITE_FASTAPI_BASE_URL` üzerinden FastAPI'ye gitmeli.
- Login önce e-posta+şifreyle `/auth/login`, sonra OTP doğrulama, son olarak
  `dogrulama_token` ile ikinci `/auth/login` çağrısını yapmalı.
- Vercel production build ve SPA route ayarı doğrulanmalı.

UI URL'si kesinleşince Render'daki .NET `Cors__AllowedOrigins__0` ve FastAPI
`ALLOWED_ORIGINS` değerleri aynı HTTPS origin ile güncellenir; iki servis de
yeniden deploy edilir.
