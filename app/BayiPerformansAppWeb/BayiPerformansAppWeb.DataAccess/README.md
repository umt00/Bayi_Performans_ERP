# DataAccess katmanı

Bu proje Supabase PostgreSQL'e EF Core/Npgsql ile erişir. Business servislerinin
ihtiyaç duyduğu veriyi read repository arayüzleri üzerinden sağlar; bayi, ürün
stok ve teklif yönetim endpointleri için sınırlı `INSERT/UPDATE`
repository'leri de içerir. Metrik, risk skoru veya teklif kararı hesaplamaz.

Performans read repository'si ham satış/hedef tablolarından hesap yapmak yerine
Data/Automation tarafından yazılan aylık `bayi_donem_performans` tablosunu okur.

## Akış

```text
Business sorgu/orkestrasyon servisi
-> IGunlukSnapshotReadRepository / IWriteRepository
-> EF Core sorgusu veya SaveChanges
-> PentaDbContext
-> Supabase PostgreSQL
```

## Eşlenen ham tablolar

- `bayiler`
- `depolar`
- `hedefler`
- `satis`
- `stok`
- `urunler`
- `bayi_donem_performans`
- `teklifler`
- `teklif_kalemleri`
- `teklif_degerlendirme`
- `teklif_ai_yorumlari`

`Persistence/PersistenceConfigurations.cs` içindeki kod, canlı Supabase
şemasında belirtilen tablo/kolon adlarını ve PostgreSQL tiplerini bekler. Data
ekibi şemayı değiştirirse mapping'ler canlı metadata ile tekrar doğrulanmalıdır.

17 Ağustos 2026 tarihli salt-okunur metadata doğrulamasına göre satış ve stok
tarihleri PostgreSQL `date`; hedef yıl/ay alanları `integer`; miktar, fiyat,
iskonto, hedef, stok ve maliyet alanları `numeric`; `SatisID` ise
`character varying` tipindedir. EF eşlemeleri bu canlı tipleri kullanır.

Satış repository'si ürün/depo bazlı 90 günlük toplamları PostgreSQL tarafında
`GroupBy + Sum` ile hesaplayan iki toplu sorgu sağlar. İade grupları normal
satışlardan ayrı toplanır ve Business katmanının mevcut signed-miktar davranışı
korunur. Böylece her ürün/depo için ayrı satış sorgusu çalıştırılmaz.

Bayi/depo/ürün kodlarının, hedefte bayi-yıl-ay ve stokta ürün-depo birleşiminin
tekil olması beklenir. Duplicate kayıtlar rastgele `First()` seçilerek gizlenmez;
veri kalitesi Data ekibi tarafından düzeltilmeli veya davranış PO ile açıkça
kararlaştırılmalıdır.

Eşlemeler mevcut tablo anahtarlarını EF modelinde tanımlar ve migration dışıdır.
Read repository'ler `NoTracking` kullanır. Bayi, ürün, stok ve teklif write
repository'leri yalnız mevcut tablolarda `Add`/`SaveChanges` ile kayıt ekler veya
günceller; hata olduğunda başarı taklidi yapmaz. Uygulama `EnsureCreated`,
`Migrate`, tablo oluşturma veya şema değiştirme işlemi çalıştırmaz.

Kesin performans ve teklif şeması repository kökündeki
`supabase/migrations/20260825000001_refactor_schema_and_tables.sql`
migration'ındadır. Uygulama başlangıcında otomatik çalıştırılmaz.
Data/Supabase sorumlusu migration'ı hedef projeye uygulamalı, API rolüne gereken
en az `SELECT/INSERT/UPDATE` izinlerini vermelidir.

Data ekibinin view'ları performans için daha sonra değerlendirilebilir; fakat bir
view içindeki hedef, iade veya risk formülü PO onayı olmadan uygulamanın kesin iş
kuralı kabul edilmez. Ham tablo repository'leri güvenli başlangıç noktasıdır.

## Kesin aylık performans sözleşmesi

`bayi_donem_performans` anahtarı `(bayi_kodu, donem)` birleşimidir. Mapping şu
kolonları bekler: `ciro`, `hedef`, `hedef_tutma_orani`,
`ortalama_kar_marji`, `ortalama_iskonto_orani`, `ortalama_iade_orani`,
`kritik_stok_kalan_urun_sayisi`, `risk_skoru`, `risk_seviyesi` ve
`gerekce_kodlari`. Aylık tabloda snapshot/audit, karar veya AI yorumu kolonu
yoktur. Bayi adı, segment ve bölge `bayiler` tablosundan join ile alınır.

Teklif başlığı `teklifler`, satırlar `teklif_kalemleri`, hesaplanmış sonuç
`teklif_degerlendirme`, metin çıktısı `teklif_ai_yorumlari` tablosundadır.
Repository bu dört tabloyu birlikte okur; teklif tablosunda master açıklamalarını
veya JSON değerlendirme kopyasını beklemez.

Bağlantı dizesi yalnız API projesinin VS Code **Manage User Secrets**
ekranında tutulur. Gerçek bağlantı bilgisi bu projeye veya Git'e eklenmez.
PostgreSQL rolü, gerekli tablolarda `SELECT` ile birlikte yalnız yönetim
endpointlerinin ve teklif akışının gerektirdiği `INSERT/UPDATE` izinlerine sahip
olmalıdır.
`SSL Mode=VerifyFull` ile birlikte Supabase Root CA dosyasının yerel yolu da
User Secrets içindeki `Root Certificate` alanında bulunmalıdır.
