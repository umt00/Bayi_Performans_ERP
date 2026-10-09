# 🗄️ Veritabanı Sanal Tablo (View) Mimari Dokümantasyonu

Bu doküman, projedeki sanal tabloların (View) veri mimarisinde ne işe yaradığını ve hangi operasyonel amaçlarla kullanıldığını özetlemektedir.

## 1. Satış ve Kârlılık Analizi (`vw_satis_analiz`)
* **Ne İşe Yarar?** Ham satış verilerini, ürün maliyetleri ve bayi bilgileriyle birleştirerek tek bir satırda net tutar, toplam maliyet, TL bazlı kâr (marj) ve kârlılık yüzdesini otomatik olarak hesaplar.
* **Ne İçin Kullanılır?** Power BI üzerindeki ana satış raporlarını beslemek, şirketin genel kârlılığını anlık olarak ölçmek ve backend (API) üzerinden "Tüm Satışları Getir" tarzı veri isteklerine hazır, formüllenmiş veri sunmak için kullanılır.

## 2. Hedef ve Performans Takibi (`vw_bayi_hedef_performans`)
* **Ne İşe Yarar?** Bayilerin aydan aya yaptıkları toplam ciroyu, sistemdeki aylık hedefleriyle kıyaslar. Yüzde kaç hedefe ulaşıldığını hesaplar ve "Hedef Aşıldı", "Geride Kaldı" şeklinde performans durumunu kategorize eder.
* **Ne İçin Kullanılır?** Yönetici panellerinde (Dashboard) en başarılı ve hedefin gerisinde kalan bayileri ayırt etmek, performans değerlendirmeleri yapmak ve satış ekipleri için prim/ödül sistemlerine veri sağlamak için kullanılır.

## 3. Stok Kontrol ve Alarm (`vw_stok_durum_analiz`)
* **Ne İşe Yarar?** Depolardaki anlık stok miktarlarını kontrol eder ve önceden belirlenen minimum stok seviyelerinin altına düşen ürünleri "Kritik Seviye" veya "Tükendi" olarak etiketleyip stok açığını hesaplar.
* **Ne İçin Kullanılır?** Tedarik zinciri ve satın alma departmanı için otomatik stok siparişi alarmları (erken uyarı sistemleri) kurgulamak ve hangi depoda hangi ürünün eksildiğini anında görebilmek için kullanılır.

## 4. Kategori ve Bölge Analizi (`vw_kategori_bolge`)
* **Ne İşe Yarar?** Tek tek satış satırlarını (mikro veriyi) gizler ve veriyi şehir, bayi segmenti (Kurumsal/KOBİ vb.) ve ürün kategorisi bazında toplulaştırır.
* **Ne İçin Kullanılır?** Şirketin hangi coğrafi bölgede veya hangi ürün gruplarında daha güçlü/zayıf olduğunu analiz etmek, bölgesel pazarlama stratejileri belirlemek ve raporlama araçlarında makro özet grafikleri çizmek için kullanılır.

## 5. Riskli İşlem Tespit (`vw_riskli_islem`)
* **Ne İşe Yarar?** Şirketi zarara uğratan (kâr marjı eksiye düşen) veya alışılmışın dışında, çok yüksek oranlarda iskonto (indirim) uygulanmış şüpheli satış işlemlerini süzerek ayrı bir listede toplar.
* **Ne İçin Kullanılır?** Operasyon ekiplerine veya yöneticilere usulsüz ya da hatalı işlemleri anında yakalamaları için otomatik uyarılar göndermek ve güvenilir bir finansal denetim (audit) mekanizması kurmak için kullanılır.

## 6. Yapay Zeka (AI) Maskelenmiş Özet (`vw_ai_ozet`)
* **Ne İşe Yarar?** Şirketin gerçek bayi isimleri, net maliyetler veya bölgesel sırlar gibi hassas verilerini tamamen gizleyerek (maskeleyerek), veriyi sadece AI'ın okuyabileceği genel işlem sayısı ve ciro formatına indirger.
* **Ne İçin Kullanılır?** Sisteme entegre edilecek olan Büyük Dil Modellerinin (LLM) şirket sırlarını dışarı sızdırmasını engellemek ve yapay zekanın sadece izin verilen güvenli alanlar üzerinden mantıklı yorumlar (insight) üretebilmesini sağlamak için kullanılır.
