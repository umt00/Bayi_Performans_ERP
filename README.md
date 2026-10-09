# Penta Bayi Performans ve Karar Destek Platformu

Bu repository; veri hazırlama, zamanlanmış aylık hesaplama, PostgreSQL/Supabase,
.NET Web API, FastAPI teklif servisi, React yönetim portalı ve raporlama
çalışmalarının ortak entegrasyon alanıdır.

## 🚀 Tek Komutla Tüm Projeyi Başlatma (Docker Compose)

Projenin tüm servislerini (React 19 UI, .NET 8 Web API, FastAPI, Redis, Elasticsearch ve Kibana) tek bir komutla ayağa kaldırmak için:

```bash
# 1. Ortam değişkenlerini hazırlayın (.env dosyasını oluşturun)
cp .env.example .env

# 2. Tüm mikroservisleri ve altyapıyı başlatın
docker compose up -d --build
```

### 🌐 Erişim Adresleri

| Servis | Port / Adres | Açıklama |
|---|---|---|
| **React UI Dashboard** | [http://localhost:3000](http://localhost:3000) | Ana kullanıcı ve yönetici arayüzü |
| **FastAPI Servisi** | [http://localhost:8000](http://localhost:8000) | Teklif motoru & AI servisleri |
| **FastAPI Swagger UI** | [http://localhost:8000/docs](http://localhost:8000/docs) | FastAPI interaktif API dokümantasyonu |
| **.NET Web API** | [http://localhost:10000](http://localhost:10000) / [http://localhost:7000](http://localhost:7000) | Performans, Auth & Arama API |
| **.NET Swagger UI** | [http://localhost:10000/swagger](http://localhost:10000/swagger) | .NET API dokümantasyonu |
| **Elasticsearch** | [http://localhost:9200](http://localhost:9200) | Satış ve bayi arama motoru |
| **Kibana (Opsiyonel)** | [http://localhost:5601](http://localhost:5601) | Elasticsearch dashboard (`--profile tools`) |
| **Redis Commander (Opsiyonel)** | [http://localhost:8081](http://localhost:8081) | Redis GUI yönetim paneli (`--profile tools`) |

---

## Güncel uygulama

Güncel .NET 8 N-tier solution ve React uygulaması
[`app/BayiPerformansAppWeb`](./app/BayiPerformansAppWeb) klasöründedir.

Bu snapshot şunları içerir:

- ASP.NET Core Web API, Swagger ve standart `ProblemDetails` cevapları
- Supabase Auth kayıt/login, JWT kontrolü, refresh/logout ve e-posta onayı akışı
- Redis + SMTP tabanlı, parola doğrulamasından sonra başlayan iki adımlı login akışı
- EF Core/Npgsql ile Supabase PostgreSQL veri erişimi
- Aylık hesaplanmış bayi performansı, risk ve kritik stok sonuçlarını sunan servisler
- Çok ürünlü teklif oluşturma, PostgreSQL'de saklama ve sonradan değerlendirme
- Supabase access token doğrulamalı FastAPI teklif uçları
- Bayi, ürün ve stok yönetim endpointleri
- Harici günlük Data/Automation çalışmasının durumunu göstermeye hazırlanan job endpointleri
- React admin portalı
- Yerel Redis, Elasticsearch ve Kibana için Docker Compose geliştirme altyapısı
- API ve Business otomatik test projeleri

Ayrıntılı mimari, User Secrets, sertifika, Redis/SMTP, Elasticsearch ve endpoint
rehberi için [uygulama README'sine](./app/BayiPerformansAppWeb/README.md) bakın.

## Kesin performans veri kararı

Yeni ekip kararına göre performans, hedef gerçekleşme, marj, iskonto, iade,
risk ve kritik stok metrikleri HTTP isteği sırasında API/Business tarafından
yeniden hesaplanmayacaktır.

Performans tablosunda bayi ve dönem başına tek aylık kayıt tutulur. Hedef veri
akışı şöyledir:

```text
Ham tablolar
-> Data/Automation tarafından zamanlanmış aylık hesaplama
-> Supabase'te bayi + dönem anahtarlı aylık performans tablosu
-> .NET DataAccess read repository
-> API endpointleri
-> React / Power BI / diğer tüketiciler
```

- Aylık hesaplamanın sahibi Data/Automation ekibidir.
- API'nin görevi hazır performans verisini doğrulamak, filtrelemek ve DTO
  sözleşmesiyle sunmaktır; eksik kaydı anlık hesaplama veya mock veriyle gizlemez.
- AI yalnız önceden `NULL` bırakılan açıklama/yorum alanlarını doldurur;
  deterministik sayısal alanları değiştirmez.
- Mevcut Business hesaplayıcıları geçiş süresince sonuç karşılaştırması ve
  regresyon testleri için tutulur. Günlük tablolar bağlandıktan sonra endpoint
  çalışma yolundan çıkarılacaktır.
- Kesin anahtar `(bayi_kodu, donem)` şeklindedir; günlük snapshot/run ID alanı
  performans API sözleşmesinin parçası değildir.
- Teklif oluşturma ve değerlendirme aynı kullanıcı eylemi içinde sırayla
  FastAPI tarafından yürütülür; geçmiş teklifler normalize teklif tablolarında
  korunur.

## Çalışan servis sınırları

- **.NET API:** kayıt, login, OTP, Supabase JWT üretimi; katalog, stok ve aylık
  performans okuma uçları.
- **FastAPI:** çok kalemli teklif oluşturma/değerlendirme ve teklif geçmişi.
  Sağlık/OpenAPI dışında bütün uçlar .NET API'den alınan aynı Supabase access
  token'ını doğrular.
- **React UI:** iki backend'e yalnız ortam değişkenleriyle bağlanır; risk, marj,
  karar veya AI yorumu üretmez ve canlı hatayı mock başarıya çevirmez.

## Repository yapısı

| Klasör | Amaç / sahiplik |
|---|---|
| `app/BayiPerformansAppWeb` | Güncel .NET API, Business, DataAccess, Entities, React UI ve uygulama testleri |
| `data` | Ham ve temizlenmiş veri dosyaları |
| `etl`, `pipeline` | Veri temizleme/yükleme ve Airflow akışları |
| `db`, `supabase` | PostgreSQL şeması, metadata ve view migration'ları |
| `analytics`, `config` | Günlük hesap formülleri, analiz ve kural/eşik sözleşmeleri |
| `automation`, `logs` | Günlük hesaplama, AI yorum zenginleştirme, uyarı akışları ve çalışma kayıtları |
| `powerbi`, `reports`, `Raporlar` | Dashboard ve ekip raporları |
| `product` | PO kapsamı, backlog ve kabul kararları |

Bir ekip üyesi başka bir alanı değiştirirken ilgili alan sahibinden review
istemelidir. Ortak entegrasyon değişiklikleri doğrudan `main`e gönderilmez;
feature branch + pull request üzerinden incelenir.

## Hızlı başlangıç

1. VS Code ile repository kökünü açın.
2. `app/BayiPerformansAppWeb/BayiPerformansAppWeb.sln` solution'ını seçin.
3. Gerçek bağlantı bilgilerini yalnız API projesinin **.NET User Secrets**
   dosyasına girin. Parola, token, SMTP uygulama parolası ve yerel sertifika yolu
   Git'e eklenmez.
4. `app/BayiPerformansAppWeb` altındaki `.vscode` görevleriyle gerekli yerel
   Docker servislerini başlatın.
5. API'yi .NET 8 profiliyle, UI'ı Vite geliştirme profiliyle çalıştırın.

## Entegrasyon için kalan dış adımlar

- Data/Supabase sorumlusu
  [`20260821_teklifler.sql`](./app/BayiPerformansAppWeb/BayiPerformansAppWeb.DataAccess/Sql/20260821_teklifler.sql)
  dosyasını inceleyip hedef Supabase projesinde uygulamalıdır. API otomatik DDL
  veya migration çalıştırmaz.
- Her geliştirici Supabase bağlantısını ve Root CA yolunu kendi User Secrets
  dosyasında tanımlar.
- UI canlı modda API hatasını mock başarıya çevirmemeli; endpoint/JSON sözleşmesi
  ortak testte doğrulanmalıdır.
- Elasticsearch indexine Supabase verisini taşıyan kalıcı ETL/Airflow senkronu
  Data/Automation ekibinin entegrasyonudur.
- Data/Automation ekibi aylık performans kaydını ve zamanlanmış yüklemeyi
  sağlamalıdır. Yazılım-1 salt-okunur EF mapping ve endpoint sözleşmesini hazır
  tutar; dış yükleme başarısızsa sahte sonuç üretmez.

## Doğrulanan durum

Güncel doğrulama sonuçları her entegrasyon push'undan önce yeniden üretilir.
.NET Business/API testleri, Python FastAPI/Auth testleri, React lint ve production
build birlikte yeşil olmadan ortak `main` yayını tamamlanmış kabul edilmez.

-----trying for vercel
