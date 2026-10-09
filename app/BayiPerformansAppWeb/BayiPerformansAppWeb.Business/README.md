# Business katmanı

Bu proje servis sözleşmelerini, sonuç modellerini ve API ile veri erişimi
arasındaki orkestrasyonu içerir.

## Aylık performans veri sınırı

25 Ağustos 2026 tarihli kesin sözleşmeye göre performans, hedef, marj, iskonto,
iade, risk ve kritik stok metrikleri yeni performans endpointinde HTTP isteği
sırasında hesaplanmaz. Data/Automation bu değerleri aylık
`bayi_donem_performans` tablosuna yazar; sorgu yolu hazır satırı API sonucuna
dönüştürür.

Kod tabanındaki mevcut hesaplayıcılar ve karar motorları geçiş süresince:

- Data/Automation çıktısıyla karşılaştırma,
- formül regresyon testleri,
- eski ve yeni sonuçlar arasındaki farkları açıklama

amacıyla korunur. Aylık repository bağlandıktan sonra yeni performans
ve stok endpointlerinin runtime yolunda kullanılmamalıdır.

Business katmanı:

- API projesine referans vermez.
- Controller, `DbContext`, raw SQL veya Supabase SDK kullanmaz.
- Veriyi DataAccess aylık performans repository arayüzünden alır.
- Para ve oran alanlarında `decimal` sözleşmesini korur.
- AI'ya skor veya karar hesabı yaptırmaz.
- Aylık kayıt eksikliğini eski hesaplayıcıya veya mock veriye düşerek gizlemez.

Eski hesaplama motorunun geliştirme geçmişi için
[Yazılım-2 yol haritasına](../docs/YAZILIM_2_YOL_HARITASI.md) bakılabilir; bu
belge artık hedef runtime mimarisi değildir.
