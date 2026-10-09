# Entities katmanı

Bu proje diğer katmanların birlikte kullandığı sade veri modellerini içerir.

- `ReadModels`: repository'lerin ham veya aylık hesaplanmış tablolardan okuduğu modeller
- `ServiceOutputs`: Business sorgu/orkestrasyon servislerinin API'ye verdiği sonuç modelleri

Entities hiçbir projeye referans vermez ve HTTP, EF Core, Supabase veya iş kuralı
bilmez. Böylece katmanlar arasında döngü oluşmadan ortak modeller taşınabilir.

Buradaki modeller persistence satırlarının birebir kopyası olmak zorunda değildir;
DataAccess, veritabanı satırını read modele dönüştürür. API de servis çıktısını
response DTO'ya dönüştürür.

Kesin aylık performans şemasının persistence satırları DataAccess'te tanımlanır.
Entities, `bayi_donem_performans` metriklerini ve normalize teklif değerlendirme
ile AI yorum modellerini katmanlar arasında kayıpsız taşır.
