# Uygulama klasörü

Güncel ve çalıştırılabilir uygulama
[`BayiPerformansAppWeb`](./BayiPerformansAppWeb) altındadır. Solution projeleri
birbirinin kardeşidir; `Business` veya `DataAccess`, `Api` klasörünün içine
yerleştirilmemiştir.

```text
app/BayiPerformansAppWeb/
├── BayiPerformansAppWeb.Api          # Controller, DTO, Swagger, JWT, config
├── BayiPerformansAppWeb.Business     # Servis orkestrasyonu ve sonuç sözleşmeleri
├── BayiPerformansAppWeb.DataAccess   # EF Core, günlük snapshot repository'leri, PostgreSQL
├── BayiPerformansAppWeb.Entities     # Katmanlar arası modeller
├── BayiPerformansAppWeb.Api.Tests
├── BayiPerformansAppWeb.Business.Tests
├── UI                                # React/Vite yönetim portalı
├── elastic_migration                 # Elasticsearch index sözleşmesi
├── docker-compose.yml                # Redis ve isteğe bağlı search profili
└── BayiPerformansAppWeb.sln
```

Katman akışı:

```text
Data/Automation günlük işi -> Supabase hesaplanmış snapshot tabloları
React/Swagger -> API Controller -> Business Service -> Read Repository
              -> EF Core/Npgsql -> Supabase PostgreSQL
```

Controller doğrudan `DbContext` çağırmaz. DataAccess ve Business, günlük
performans/risk/stok metriklerini HTTP isteği sırasında yeniden hesaplamaz;
Data/Automation tarafından veritabanına yazılmış sonucu okur. Business API
projesine bağımlı değildir. Mevcut hesaplayıcılar geçiş tamamlanana kadar
karşılaştırma ve regresyon amacıyla tutulur. Ayrıntılı kurulum ve çalıştırma
bilgisi için
[`BayiPerformansAppWeb/README.md`](./BayiPerformansAppWeb/README.md) dosyasını
okuyun.
