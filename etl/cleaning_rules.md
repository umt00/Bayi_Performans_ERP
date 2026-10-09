# Veri Temizliği Raporu ve Gerekçeler

## Yapılan Temizlik İşlemleri

### Satış Verileri
- Tarih alanları tek formata dönüştürüldü.
- Birim fiyatlar metinden sayısal formata çevrildi.
- İskonto oranları %5, 5 ve 0.05 gibi farklı gösterimlerden tek bir formata normalize edildi.
- Satış kanalları standartlaştırıldı.
- Negatif miktarlar iade olarak değerlendirildi ve veri setinde korundu.
- Geçersiz bayi ve ürün kodları tespit edilerek analiz dışında bırakıldı.

### Ürün Verileri
- Mükerrer ürün kayıtları kaldırıldı.
- Ürün adlarındaki gereksiz boşluklar temizlendi.
- Birim maliyet alanı sayısal formata dönüştürüldü.
- Eksik maliyet değerleri uygun iş kuralı (Alt kategori medyanı) ile tamamlandı.

### Bayi Verileri
- Mükerrer kayıtlar kaldırıldı.
- Şehir isimleri standartlaştırıldı (Türkçe karakter düzeltmeleri vb.).
- Eksik segment bilgileri `Bilinmiyor` olarak dolduruldu.
- Segment değerleri tek formatta toplandı.

### Stok Verileri
- Tarih formatları standartlaştırıldı.
- Dinamik stok eşiği belirlendi: Minimum stok seviyesi statik bir sayı (örn. 5 veya 20) yerine, **son 90 günlük ortalama günlük satış adetinin 7 katı** (7 günlük stok) olarak hesaplanacaktır. Mevcut stok bu değerin altına düştüğünde ürün `KRITIK_STOK` (Performans) / `STOK_YETERSIZ` (Teklif) uyarısı verecektir.
- Ürün-depo kayıtlarının tekilliği kontrol edildi.

### Depo Verileri
- İsim ve lokasyon bilgileri temizlendi.
- Yazım ve boşluk hataları düzeltildi.

### Hedef Verileri
- Wide formatta bulunan aylık hedefler long formata dönüştürüldü.
- Böylece zaman bazlı analiz ve hedef karşılaştırmaları kolaylaştırıldı.

## Alınan Kararlar
| Problem | Karar |
|---------|-------|
| Boş iskonto değerleri | `%0` kabul edildi |
| Negatif miktarlar | `İade` olarak değerlendirildi |
| Eksik segmentler | `Bilinmiyor` olarak dolduruldu |
| Geçersiz ürün/bayi kodları | Analizden çıkarıldı (Inner Join) |
| Kritik/Minimum Stok Eşiği | Dinamik hesaplama: Son 90 günün ortalama satışının 7 katı (7 günlük stok). Eksik eski eşiklerin önemi kaldırıldı. |
| Hedef verisi yapısı | `Wide` → `Long` formata dönüştürüldü |

## Hazırlanan Metrikler ve API Standartları
- **Net Satış Tutarı:** `Miktar × BirimFiyat × (1 - İskontoOranı)`
- **Marj:** Oran olarak (0.15) hesaplanır, API çıktılarında `_pct` sonekiyle (örn: `marj_esigi_pct: 15.0`) 100 ile çarpılarak sunulur.
- **Hedefe Ulaşma:** `(Gerçekleşen Ciro / Hedef Ciro) × 100`, API'de `hedefe_ulasma_pct` olarak döner.
- **Standart Gerekçe Kodları:** `MARJ_ESIK_ALTI`, `YUKSEK_ISKONTO`, `HEDEF_ALTINDA`, `STOK_YETERSIZ`, `YUKSEK_IADE`

## Sonuç
Veri setleri temizlenmiş, standartlaştırılmış ve analiz yapılabilecek hale getirilmiştir. Temizlik sürecinde veri kaybını minimumda tutmak ve iş mantığını korumak öncelikli hedef olarak benimsenmiştir.
