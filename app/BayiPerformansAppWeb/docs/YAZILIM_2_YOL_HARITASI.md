# Yazılım-2 Business katmanı yol haritası

> **Tarihsel belge / hedef mimari değil:** 21 Ağustos 2026 tarihli ekip kararıyla
> günlük performans, risk ve kritik stok hesapları Data/Automation tarafından
> üretilip PostgreSQL'e yazılacaktır. Business katmanı bu snapshot'ları okuyup
> API sonucuna dönüştürecektir. Aşağıdaki hesaplayıcı geliştirme adımları mevcut
> kodun geçmişini ve regresyon karşılaştırmasını açıklar; yeni runtime akışı
> olarak uygulanmamalıdır. Teklif değerlendirmesinin kapsamı ayrıca PO kararıdır.

Bu belge, Yazılım-2 stajyerinin `BayiPerformansAppWeb.Business` katmanını
mevcut API ve veri erişimi sözleşmelerini bozmadan tamamlaması için hazırlanmıştır.

> Durum (14 Ağustos 2026): hesaplayıcılar, karar motorları, dört Business servisi,
> threshold yapılandırması ve DI graph API ile entegre edilmiştir. Dört servisin
> davranış testleri, nullable veri kararları ve risk listesindeki kritik stok
> girdisi takip işi olarak açık kalmaktadır.

## 1. Başlangıç noktası

Yazılım-1 şu parçaları hazırlar:

- `BayiPerformansAppWeb.Api`: controller, endpoint, request/response ve HTTP hataları
- `BayiPerformansAppWeb.DataAccess`: EF Core, PostgreSQL eşlemeleri ve repository'ler
- `BayiPerformansAppWeb.Entities`: katmanlar arasında taşınan veri modelleri
- `BayiPerformansAppWeb.Business`: servis arayüzleri ve ortak sonuç modeli

Business servislerinin gerçek uygulamaları ve DI kayıtları entegrasyon branch'inde
mevcuttur. Geçici servisler `TryAddScoped` ile güvenlik ağı olarak korunur; gerçek
servis kayıtları bulunduğunda devreye girmez.

## 2. Sorumluluk sınırı

Yazılım-2'nin sorumlulukları:

- Ciro, hedef gerçekleşme, marj, iskonto ve iade metrikleri
- Risk skoru, risk seviyesi ve gerekçe kodları
- Kritik stok, son üç aylık satış hızı ve tahmini stok günü
- Teklif için `APPROVE`, `REJECT` veya `NEED_APPROVAL` kararı
- Business ayarlarının doğrulanması ve Dependency Injection kaydı
- Hesaplayıcı ve servis birim testleri

Yazılım-2'nin değiştirmemesi gerekenler:

- Controller'lar, endpoint adresleri ve JSON alanları
- EF Core `DbContext`, persistence eşlemeleri ve repository implementasyonları
- Supabase tabloları, view'ları, migration'ları veya veri yükleme kodu
- UI, Power BI ve AI ekiplerinin dosyaları

Business katmanı controller'a, `DbContext`'e veya Supabase SDK'sına doğrudan
erişmez. Veriyi yalnız Yazılım-1'in repository arayüzlerinden ister.

```text
Swagger -> Controller -> Business servisi -> Repository arayüzü
        -> DataAccess / EF Core -> Supabase PostgreSQL
```

## 3. Uygulanacak dört servis

Servis arayüzleri
`BayiPerformansAppWeb.Business/Abstractions/ServiceContracts.cs` dosyasındadır.

| Endpoint | Hazır arayüz | Önerilen uygulama sınıfı |
|---|---|---|
| `GET /bayi/{kod}/performans` | `IBayiPerformansService` | `BayiPerformansService` |
| `GET /bayiler/risk-listesi` | `IRiskListesiService` | `RiskListesiService` |
| `GET /stok/kritik` | `IKritikStokService` | `KritikStokService` |
| `POST /teklif-degerlendir` | `ITeklifDegerlendirmeService` | `TeklifDegerlendirmeService` |

`Business`, `Api` projesine referans vermemelidir. Request DTO'dan Business
komutuna dönüşüm API'de kalır; aksi durumda iki proje arasında döngü oluşur.

## 4. Kodlamadan önce PO ile kararlaştırılacaklar

Aşağıdaki maddeler yazılı olarak kesinleşmeden iş kuralı tahmin edilmemelidir.

| Konu | Kesinleştirilmesi gereken karar |
|---|---|
| Hedefsiz bayi | Hedef alanları `null` kalacak. Eksik hedef risk puanı veya gerekçe kodu üretecek mi? |
| İade | Negatif miktar ve `IsIade=true` birlikteyse cirodan iki kez düşme nasıl önlenecek? İade oranının payı ve paydası nedir? |
| Marj | Kesin formül nedir? Ürün maliyeti eksikse marj `null` mı, karar `NEED_APPROVAL` mı olacak? |
| Risk | Her kuralın puanı ile `DUSUK`, `ORTA`, `YUKSEK` sınırları nedir? Eşik eşitliğinde ne olur? |
| Kritik stok | Kritik koşul `mevcut_stok < min_stok` mu, yoksa `<=` mi? Satış yoksa stok günü ne olur? |
| Teklif | Stok, marj, iskonto ve bayi riski hangi öncelikle değerlendirilir? |
| Varsayılan dönem | `donem` yoksa son tamamlanmış ay mı, verideki son satış ayı mı seçilir? |
| Oranlar | Yüzde alanları `18` mi, `0.18` mi döner ve kaç basamak yuvarlanır? |
| Gerekçe kodları | İzin verilen sabit kodlar ve sıraları nelerdir? |
| Eksik zorunlu veri | Kaynakta `null` olabilen segment, bölge, mevcut stok veya minimum stok dış sözleşmede zorunludur. Eksik kayıt kontrollü hata mı olacak, listeden mi çıkarılacak, yoksa sözleşme PO onayıyla nullable mı yapılacak? |

Eşikler sınıfların içine sayı olarak serpiştirilmemelidir. PO onaylı değerler
`BusinessRulesOptions` gibi tek bir ayar modelinden okunmalı ve uygulama başlarken
doğrulanmalıdır.

## 5. Önerilen Business yapısı

```text
BayiPerformansAppWeb.Business
├── Abstractions
├── Calculations
│   ├── SalesMetricsCalculator.cs
│   ├── RiskEngine.cs
│   ├── StockMetricsCalculator.cs
│   └── OfferDecisionEngine.cs
├── Configuration
│   └── BusinessRulesOptions.cs
├── Services
│   ├── BayiPerformansService.cs
│   ├── RiskListesiService.cs
│   ├── KritikStokService.cs
│   └── TeklifDegerlendirmeService.cs
└── DependencyInjection.cs
```

Hesaplayıcılar mümkün olduğunca saf ve deterministik olmalıdır: aynı veri ve
aynı ayarlar her zaman aynı sonucu vermelidir.

## 6. Servis bazında uygulama sırası

### 6.1 BayiPerformansService

Kullanılabilecek repository'ler:

- `IBayiReadRepository.GetByCodeAsync`
- Varsayılan dönem gerekiyorsa `ISatisReadRepository.GetLatestSaleDateAsync`
- `ISatisReadRepository.ListByDealerAndPeriodAsync`
- `IAylikHedefReadRepository.GetAsync`
- `IUrunReadRepository.ListByCodesAsync`
- Gerektiğinde `IStokReadRepository`

Akış:

1. Bayinin varlığını kontrol et; yoksa `ServiceResult.Fail` ile `NotFound` döndür.
2. İstenen veya PO'nun onayladığı varsayılan dönemi belirle.
3. Satış, hedef, ürün maliyeti ve gerekli stok verilerini getir.
4. Ortak hesaplayıcılarla metrikleri üret.
5. Aynı metrikleri tek bir `RiskEngine`e ver.
6. `BayiPerformansOutput` döndür.
7. Hedef yoksa `0` uydurma; nullable alanları koru.

### 6.2 RiskListesiService

Kullanılabilecek repository'ler:

- `IBayiReadRepository.ListAsync`
- Varsayılan dönem gerekiyorsa `ISatisReadRepository.GetLatestSaleDateAsync`
- `ISatisReadRepository.ListByPeriodAsync`
- `IAylikHedefReadRepository.ListByPeriodAsync`
- `IUrunReadRepository.ListByCodesAsync`

Akış:

1. Dönemi belirle ve segment/bölge filtreli bayileri toplu getir.
2. Dönemin satış, hedef ve ürün verilerini mümkün olduğunca toplu oku.
3. Her bayi için başka endpoint'e HTTP isteği atma; ortak hesaplayıcıları kullan.
4. Tek bir `RiskEngine` ile skor, seviye ve gerekçe kodlarını üret.
5. Sıralamayı deterministik yap; örneğin skor azalan, eşitlikte bayi kodu artan.
6. `toplam` alanını dönen bayi sayısıyla eşleştir.

Her bayi için ayrı sorgu çalıştıran N+1 yaklaşımından kaçınılmalıdır.

### 6.3 KritikStokService

Kullanılabilecek repository'ler:

- `IStokReadRepository.ListAsync`
- `ISatisReadRepository.ListByProductWarehouseAndPeriodAsync`
- Çoklu stok için `ISatisReadRepository.ListByPeriodAsync` ile dönemi tek seferde
  okuyup ürün/depo anahtarına göre gruplama
- Gerektiğinde `IUrunReadRepository` ve `IDepoReadRepository`

Akış:

1. Ürün/depo filtresiyle stokları getir.
2. Hesaplama zamanını bir kez al; test edilebilirlik için `TimeProvider` kullan.
3. PO'nun belirlediği son üç aylık aralığı hesapla.
4. Satış adedi, ortalama günlük satış ve tahmini stok gününü hesapla.
5. Satış yoksa sıfıra bölme yapma; PO kararına göre nullable sonuç döndür.
6. Kritik olma kuralını onaylanan `<` veya `<=` sınırıyla uygula.
7. Sonucu ürün/depo koduna göre deterministik sırala.

Veri hacmi tek dönem satışlarını toplu okumak için fazla büyürse veya view kullanımı
zorunlu olursa `DbContext`e kaçmak yerine Yazılım-1'den ürün/depo anahtarlarını
toplu alan yeni bir repository sözleşmesi istenmelidir.

### 6.4 TeklifDegerlendirmeService

Kullanılabilecek repository'ler:

- `IBayiReadRepository.GetByCodeAsync`
- `IUrunReadRepository.GetByCodeAsync`
- `IDepoReadRepository.GetByCodeAsync`
- `IStokReadRepository.GetAsync`
- Risk gerekiyorsa satış ve hedef repository'leri

Akış:

1. Bayi, ürün ve deponun varlığını kontrol et.
2. Mevcut stoku getir.
3. PO onaylı formülle net fiyat ve marjı hesapla.
4. Stok, marj, iskonto ve gerekiyorsa bayi riskini onaylı sırayla değerlendir.
5. Yalnız `APPROVE`, `REJECT` veya `NEED_APPROVAL` döndür.
6. Gerekçe kodlarını sabit listeden seç.
7. Teklif veya stok verisi yazma; bu endpoint yalnız değerlendirme yapar.

## 7. Repository kullanım kuralları

- Repository arayüzlerini constructor ile al.
- Business içinde `new PentaDbContext`, raw SQL veya Supabase SDK kullanma.
- Bütün async çağrılarda `CancellationToken`ı repository'ye aktar.
- Para ve oran hesabında `decimal` kullan.
- Nullable veriyi sessizce `0` yapma.
- Beklenen bulunamadı durumlarını kontrollü `ServiceResult` ile döndür.
- Eksik bir okuma işlemi varsa Yazılım-1'e küçük ve açık bir arayüz talebi ilet.
- Business katmanından başka bir endpoint'i HTTP ile çağırma.

## 8. Dependency Injection entegrasyonu

Business projesi tek bir kayıt uzantısı sunmalıdır:

```csharp
services.AddBayiPerformansBusiness(configuration);
```

Bu metot:

- PO onaylı ayarları bind ve validate etmeli.
- Dört servis arayüzünü gerçek sınıflara `AddScoped` ile bağlamalı.
- Scoped repository kullanan servisleri `Singleton` yapmamalı.

`Program.cs` kayıt sırası şu şekilde olmalıdır:

```text
AddPentaDataAccess(...)
AddBayiPerformansBusiness(...)
AddBusinessIntegrationFallbacks()
```

API fallback kayıtları `TryAddScoped` kullandığı için gerçek servisler önce
kaydedildiğinde `503` yerine gerçek servisler çalışır.

## 9. AI sınırı

İlk Business tesliminde AI entegrasyonu gerekmez; opsiyonel metin alanları `null`
kalabilir. AI yalnız açıklama taslağı üretebilir. Şunları hesaplayamaz veya
değiştiremez:

- Performans metrikleri
- `risk_skoru` ve `risk_seviyesi`
- `karar`
- `gerekce_kodlari`

AI çalışmasa bile deterministik temel API cevabı dönmelidir. AI açık veya kapalı
olduğunda sayısal sonuç, risk ve karar aynı kalmalıdır.

## 10. Test ve kabul listesi

Önce hesaplayıcılar, sonra servis orkestrasyonu test edilmelidir.

- Eşiğin bir altı, eşit ve bir üstü
- Boş satış listesi ve sıfır payda
- `null` hedef, maliyet veya stok
- İadeli ve iadesiz satış
- Yuvarlama sınırları
- Aynı girdide aynı sonuç
- `CancellationToken` aktarımı
- Hedefli, hedefsiz, satışsız ve bulunmayan bayi
- Segment/bölge filtresi ve deterministik risk sıralaması
- Minimum stok altı/eşiti/üstü ve satışsız ürün
- Teklifte üç kararın tamamı

Tamamlanma ölçütleri:

- Solution .NET 8 ile derlenir ve bütün Business testleri geçer.
- Dört servis repository arayüzlerini kullanır.
- Geçerli Swagger istekleri gerçek ve deterministik sonuç üretir.
- `400`, `404`, `409`, `503` ve snake_case JSON sözleşmesi değişmez.
- Controller'da iş hesabı veya `DbContext` bulunmaz.
- Supabase'e yazma veya migration yapılmaz.
- Secret, parola veya gerçek bağlantı dizesi Git'e girmez.

## 11. Test projesini Visual Studio'dan oluşturma

1. Solution'a sağ tıkla ve **Add > New Project** seç.
2. **xUnit Test Project** şablonunu seç.
3. Proje adını `BayiPerformansAppWeb.Business.Tests` yap ve framework olarak
   `.NET 8.0` seç.
4. Test projesinde **Dependencies > Add Project Reference** yoluyla `Business`,
   `DataAccess` ve `Entities` projelerini seç. Repository arayüzleri DataAccess'te
   olduğu için servis test doubles/fake'leri bu referansa ihtiyaç duyabilir.
5. Solution dosyasına test projesi eklenmesi bu görev için gerekli ve izinli bir
   değişikliktir.
6. **Test > Test Explorer** ekranından tüm testleri çalıştır.

Test projesi gerçek Supabase'e bağlanmamalıdır. Repository arayüzleri fake veya
mock edilerek deterministik Business kuralları test edilmelidir.

## 12. Branch ve birleştirme akışı

1. Visual Studio'da **Git > Manage Branches** ekranını aç ve **Fetch** yap.
2. `Remotes/origin/yusuf/software-1-ntier-api-data-access` üzerine sağ tıklayıp
   **New Local Branch From** seç. `master`a geçme ve ortak branch bekleme.
3. Kişisel branch adını şu biçimde oluştur:
   `ad-soyad/software-2-business`
4. Değişiklikleri ağırlıklı olarak `Business` ve kendi test projesinde tut.
5. `Program.cs` ve proje dosyalarına gereksiz değişiklik yapma; test projesini
   solution'a eklemek gerekli bir değişikliktir.
6. Küçük commitler oluştur: ayarlar/hesaplayıcılar, dört servis, testler, DI.
7. **Build > Build Solution** ve **Test > Test Explorer** ile doğrula.
8. Swagger'da dört endpoint'i kontrol et.
9. Ekipçe belirlenen hedef branch'e PR aç; doğrudan `master`a push yapma.
10. PR açıklamasına PO kararlarını ve test sonuçlarını yaz; hassas bağlantı
   bilgisini ekleme.
