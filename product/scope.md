Kapsam Dokümanı — Bayi Performans ve Karar Destek Platformu
Problem Tanımı

Şirketin satış yönetimi, 204 bayinin aylık performansını bugün elle takip ediyor: satış hareketleri, ürün maliyetleri, bayi listesi, stok durumu ve ciro hedefleri altı ayrı Excel dosyasında dağınık ve kirli durumda (karışık tarih formatları, farklı yazılmış iskonto oranları, mükerrer kayıtlar). Bu dosyaları elle birleştirip değerlendirmek yavaş, hataya açık ve kişiden kişiye farklı sonuç veriyor.

Kullanıcı

Satış yönetimi / satış yöneticisi. Platformu iki amaçla kullanacak:

Bir bayinin aylık performansını (ciro, hedefe ulaşma, marj, risk skoru) tek ekranda görmek
Yeni gelen bir bayi teklifini (fiyat/iskonto/miktar) hızlıca değerlendirip APPROVE/REJECT/NEED_APPROVAL kararı almak
Kapsam İçi
Altı Excel dosyasının temizlenip tek veri setinde birleştirilmesi, Supabase'e (PostgreSQL) yüklenmesi
Bayi bazında aylık metrikler: gerçekleşen ciro, hedefe ulaşma oranı, ortalama marj, iskonto eğilimi, iade oranı
Deterministik (kod tabanlı, rastgele olmayan) bayi risk skoru ve gerekçe kodları (ör. HEDEF_ALTINDA, MARJ_EROZYONU)
Kritik stok tespiti (MinStokSeviyesi altındaki ürün-depo kayıtları, satış hızıyla birlikte)
Teklif değerlendirme modülü (APPROVE/REJECT/NEED_APPROVAL)
API (ASP.NET Core) + basit React arayüzü
AI katmanı: sadece hesaplanmış skoru/metrikleri insan diline çeviren yorum ve uyarı taslağı üretir — skoru veya kararı asla hesaplamaz
Aylık otomasyon akışı: tetikleme → hesaplama → rapor + uyarı üretimi (mock e-posta/dosya çıktısı kabul)
Power BI raporu (en az 3 sayfa: yönetici özeti, bayi kırılımı, stok)
Kapsam Dışı (bilinçli olarak yapılmayacak)
Production-grade altyapı, ileri seviye mimari (dokümanda açıkça istenmiyor)
Gerçek e-posta/Teams gönderimi (mock/dosya çıktısı yeterli)
Gerçek LLM anahtarı zorunlu değil — mock LLM/şablon kabul edilir
Kullanıcı girişi / yetkilendirme sistemi (login, roller vb.)
Gerçek zamanlı veri entegrasyonu — veri statik, tek seferlik yüklenir
Tahminleme / forecasting (gelecek ay projeksiyonu) — sadece geçmiş veri analizi
Mobil uygulama, çoklu dil desteği
Süslü/özel tasarım arayüz — okunur ve çalışır olması yeterli
Docker zorunlu değil (alternatif: tek başlatma script'i)
Başarı Kriteri

Doğrudan dokümandaki "Minimum Kabul Kriterleri" listesine dayanıyor — proje şu sağlandığında tamamlanmış sayılır:

Uygulama tek komutla ayağa kalkıyor, üç ana endpoint (performans, risk listesi, kritik stok) + teklif değerlendirme endpoint'i veritabanından okuyarak çalışıyor
Risk skoru deterministik, eşikler thresholds.json'dan okunuyor (koda gömülü değil)
AI çıktısı skoru/kararı değiştiremiyor (guardrail ile garanti altında), LLM hata verirse fallback devreye giriyor
Power BI'daki rakamlar API'nin döndüğü rakamlarla birebir eşleşiyor (en az 3 bayide doğrulanmış)
README ile herhangi biri projeyi sıfırdan çalıştırabiliyor
20 dakikalık takım sunumu ve demo senaryosu hazır