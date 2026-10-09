# API katmanı

Bu proje dış dünyaya açılan HTTP katmanıdır. Swagger ekranı, controller'lar,
request/response modelleri, giriş doğrulaması ve standart hata cevapları burada
bulunur.

## İstek nasıl ilerler?

```text
Swagger isteği
-> Controller
-> Business servis arayüzü
-> ServiceResult
-> HTTP cevabı
```

Controller'lar yalnız HTTP girdisini doğrular, Business komutuna dönüştürür ve
servisi çağırır. Ciro, marj, risk, kritik stok veya teklif kararı hesaplamaz;
`DbContext` ya da repository kullanmaz.

25 Ağustos 2026 tarihli kesin veri sözleşmesine göre performans endpointleri,
Data/Automation tarafından aylık tek kayıt olarak yazılan
`bayi_donem_performans` satırlarını döndürür. API bu metrikleri istek sırasında
yeniden hesaplamaz ve eksik veriyi mock sonuçla gizlemez.

## Önemli klasörler

- `Controllers`: kimlik, temel iş, katalog ve yönetim endpointleri
- `Contracts/Requests`: dışarıdan alınan JSON modelleri
- `Contracts/Responses`: dışarı dönen JSON modelleri
- `Configuration`: Swagger, CORS, JSON ve hata ayarları
- `Integration`: gerçek servis kaydı eksikse kontrollü `503` sağlayan `TryAdd` fallback'leri
- `Validation`: tarih/kod gibi ortak HTTP girdi kontrolleri

## Şu an beklenen davranış

Önce API projesinde geçerli `Database:ConnectionString`,
`SupabaseAuth:ProjectUrl` ve `SupabaseAuth:PublishableKey` User Secret'ları
tanımlanmalıdır. Ayarlardan biri boş veya geçersizse uygulama başlangıç
doğrulamasında durur ve Swagger açılmaz.

Veritabanı bağlantısı `SSL Mode=VerifyFull` ve mevcut bir `Root Certificate`
dosyası kullanmalıdır. Sertifika yolu ve bağlantı dizesi yalnız yerel User
Secrets'ta tutulur; izlenen `appsettings` dosyalarına yazılmaz.

Token olmayan iş isteği önce `401` döner. Geçersiz bir istek controller veya
`[ApiController]` doğrulamasında `400` döner. Geçerli JWT ve geçerli iş isteği
Business sorgu/orkestrasyon servisine veya aylık performans repository'sine
ilerler. Fallback
kayıtları `TryAddScoped` kullandığı için gerçek servisleri ezmez; yalnız gerçek
entegrasyon kaydı bulunmadığında kontrollü
`503 BUSINESS_KATMANI_HAZIR_DEGIL` döndürür.

Çalıştırma ve User Secrets adımları için kök [README](../README.md) dosyasına bak.

## Kayıt, giriş ve JWT

Kullanıcı kaydı ve giriş e-posta ile şifre üzerinden Supabase Auth tarafından
yönetilir. API parola hash'i veya JWT üretmez. Supabase parolayı güvenli hash
olarak saklar ve girişte access/refresh token döndürür.

Login iki adımlıdır. İlk `POST /auth/login` isteği yalnız e-posta ve şifreyi
gönderir. API parolayı Supabase Auth ile doğruladıktan sonra OTP'yi e-postaya
gönderir ve `202` cevabında `islem_id` döndürür. Yanlış parolada OTP
gönderilmez. `POST /auth/otp/dogrula` sonucundaki tek kullanımlık
`dogrulama_token`, aynı e-posta ve şifreyle yapılan ikinci login isteğine eklenir;
başarılı cevap access/refresh token çiftidir.

Kayıt, login, token yenileme ve onay mailini yeniden gönderme endpointleri
anonimdir. `auth/me`, `auth/cikis` ve korumalı iş endpointleri geçerli Supabase
access token ister. Swagger'daki Authorize düğmesine login cevabındaki
`access_token` yazılarak korumalı endpointler denenebilir. Yenileme cevabındaki
yeni `refresh_token` eskisinin yerine kaydedilmeli; çıkıştan sonra istemci iki
tokenı da kendi tarafında silmelidir.

Akış, User Secrets alanları, güvenlik sınırları ve test adımları için
[kimlik doğrulama rehberine](../docs/KIMLIK_DOGRULAMA.md) bak.

## Uygulama OTP'si

Login OTP'si ayrı `POST /auth/otp/gonder` isteğiyle başlatılamaz; böylece
parolası bilinmeyen bir hesaba login OTP'si gönderilemez. Login için kodu ilk
`POST /auth/login` isteği üretir. `POST /auth/otp/dogrula`, işlem ID ve
e-postadaki kodu doğrulayarak kısa ömürlü, tek kullanımlık bir kanıt verir.
Kanıtın yalnız hash'i Redis'te TTL ile tutulur ve başarılı login denemesinde
atomik olarak tüketilir. `/auth/otp/gonder` diğer OTP amaçları için kullanılabilir.
Brevo API anahtarı yalnız User Secrets'taki `BrevoEmail` bölümünde tutulur.
Ayar kapalı veya Brevo kullanılamıyorsa gönder endpointi sahte başarı
yerine `503 OTP_EPOSTA_GONDERILEMEDI` döndürür.

Brevo ayarı kayıt olan kullanıcıya göre değişmez. User Secrets'taki doğrulanmış
gönderici uygulamanın göndericisidir; kullanıcının formda girdiği e-posta alıcıdır.

Redis ve Brevo API kurulumu için kök README'deki
**Redis ve e-posta OTP kurulumu** bölümüne bak.

## Aylık bayi performans endpointleri

Data/Automation tarafından `bayi_donem_performans` tablosuna yazılmış aylık
sonuçlar aşağıdaki salt-okunur endpointlerden sunulur:

- `GET /api/v1/performance/dealers`: dönem, risk seviyesi, bölge ve segment filtreli,
  sayfalı bayi listesi. `donem` verilmezse tablodaki en son dönem kullanılır.
- `GET /api/v1/performance/dealers/{bayi_kodu}`: seçilen dönemdeki tek bayi
  detayı, hazır metrikler ve gerekçe kodları.
- `GET /api/v1/performance/dashboard-stats`: toplam bayi/ciro/hedef, yüksek
  riskli ve kritik stoklu bayi sayıları ile ortalama metrikler.

Bu controller metrik veya karar hesaplamaz; doğrudan
`IBayiDonemPerformansReadRepository` üzerinden `(bayi_kodu, donem)` anahtarlı
aylık tabloyu okur. `segment`, `bolge` ve `bayi_adi` alanları tekrar performans
tablosuna yazılmaz; `bayiler` master tablosundan salt-okunur join ile alınır.
Yüzde alanları `0-100` aralığındadır. Liste sayfa boyutu en fazla 100'dür ve
bütün route'lar geçerli Supabase JWT ister.

## Sağlık kontrolleri

`GET /health` yalnız uygulama sürecinin ayakta olduğunu bildiren liveness
endpointidir. Render bu endpointi kullanır; dış servislerdeki geçici bir kesinti
uygulamanın sürekli yeniden başlatılmasına yol açmaz.

`GET /health/ready` PostgreSQL ve Redis bağlantılarını kontrol eder. Ayrıca
`Elasticsearch:Enabled=true` olduğunda Elasticsearch cluster'ı ile
`satislar_index` varlığını da doğrular. Readiness endpointi JWT istemez ve
bağlantı bilgisi veya belge içeriği döndürmez. Zorunlu bir bağımlılık hazır
değilse `503` döner. Geliştirme ortamında Redis yerine açıkça izin verilen
bellek deposu kullanılıyorsa durum `Degraded` olarak raporlanır.

Satış araması `GET /api/v1/satis/arama` üzerinden yapılır ve geçerli Supabase JWT
ister. Serbest metin `q` parametresi bayi, şehir, ürün, kategori, marka ve depo
adlarında Türkçe N-Gram araması yapar. Kesin kod/bölge/segment filtreleri, tarih
aralığı ve sayfalama birlikte kullanılabilir. Elasticsearch erişilemezse veya
index yoksa endpoint hatayı gizlemeden `503 ProblemDetails` döndürür.

Docker/Kibana kurulumu, index mapping'i ve Data/ETL kapsam sınırı için kök
README'deki **Elasticsearch ve Kibana yerel kurulumu** bölümüne bak.

## Teklif akışı

`POST /api/v1/teklifler` yalnız teklifi ve çoklu ürün kalemlerini kaydeder.
UI'daki tek tuş davranışı için `POST /api/v1/teklifler/olustur-ve-degerlendir`
önce kaydı oluşturur, ardından aynı `teklif_id` ile değerlendirme adımını
çalıştırır. Ayrı kullanım için `POST /api/v1/teklifler/{id}/degerlendir` ve
`POST /api/v1/teklifler/{id}/yeniden-degerlendir` route'ları bulunur. Detay
`GET /api/v1/teklifler/{id}/detay` üzerinden okunabilir. `/teklifler` kökleri
geriye dönük uyumluluk için aynı controller'da korunur. Listeleme endpointi
`bayi_kodu`, `sehir`, `bolge`, `durum`, `baslangic_tarihi` ve `bitis_tarihi`
filtrelerini destekler.

Kalıcı veri dört normalize tablodadır: `teklifler`, `teklif_kalemleri`,
`teklif_degerlendirme` ve `teklif_ai_yorumlari`. Bayi/ürün/depo adları teklif
tablolarında çoğaltılmaz; ilgili master tablolardan okunur. Değerlendirme ve AI
yorumu birbirinden ayrı 1:1 kayıtlardır.

Eski `POST /teklif-degerlendir` route'u geriye dönük uyumluluk için kodda kalır
fakat Swagger'da gizlidir. Yeni istemciler bu route'u kullanmamalıdır.

Hedef mimaride günlük hesaplama API içindeki bir background service tarafından
yürütülmez. Data/Automation zamanlayıcısı hesaplar ve job durumunu veritabanına
yazar; `GET /jobs/gece-batch` ve `GET /jobs/skor-hesapla` yalnız bu harici
çalışmanın durumunu okur. Mevcut background service geçiş kodudur.

Kesin şema kök `supabase/migrations/20260825000001_refactor_schema_and_tables.sql`
migration'ındadır. API DDL/migration çalıştırmaz; migration'ı Data/Supabase
sorumlusu uygular. Dört teklif tablosu kurulmadan canlı teklif akışı çalışmaz.
