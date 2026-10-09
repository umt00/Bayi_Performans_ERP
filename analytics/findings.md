# Analiz Bulguları ve Eşik Gerekçelendirmeleri

Bu doküman, veri stajyeri tarafından yapılan Keşifsel Veri Analizi (EDA) sonucunda oluşturulan risk ve performans eşiklerinin veri odaklı gerekçelerini sunar.

## Marj Analizi
- **Bulgu:** Genel işlem geçmişine bakıldığında, şirket ortalama kâr marjının kategori bazında %15 ile %25 arasında değiştiği görülmektedir. Kar marjı %15'in altına düştüğünde, operasyonel maliyetler de hesaba katıldığında işlem başabaş noktasına (break-even) yaklaşmaktadır.
- **Karar (Eşik):** `marj_esigi_pct = 15.0`
- **Aksiyon:** Marjı %15'in altında olan teklif veya işlemler, AI modeli tarafından "Marj Erozyonu" riski olarak işaretlenmeli ve yönetici onayına (NEED_APPROVAL) düşmelidir.

## Hedef Gerçekleşme Analizi
- **Bulgu:** Aylık hedef verileri ve gerçekleşen cirolar karşılaştırıldığında, bayilerin büyük bir bölümü (%70) hedeflerinin %80'ine ulaşabilmektedir. Ancak %80 altındaki hedef tutturma oranları, ilgili bayide yapısal bir satış durgunluğuna işaret eder.
- **Karar (Eşik):** `hedef_risk_esigi_pct = 80.0`
- **Aksiyon:** Bu eşiğin altı "Geride Kaldı", %80-%100 arası "Kılpayı Kaçtı" ve %100 üstü "Hedef Aşıldı" olarak kategorize edilecektir.

## Stok Riski Analizi
- **Bulgu:** Minimum stok seviyesi varsayılan kural olarak 20 olarak alınmıştır (Bkz. `cleaning_rules.md`). Ancak, bir ürünün mevcut stoğu 5'in altına düştüğünde, ortalama satış hızı göz önüne alındığında o ürünün çok kısa sürede (1-2 gün) tükeneceği tespit edilmiştir.
- **Karar (Eşik):** `kritik_stok_esigi = 5`
- **Aksiyon:** Stok seviyesi 5'e inen bir ürüne ait sipariş geldiğinde, sistem "Tedarik Krizi" uyarısı vermeli ve siparişi riskli işleme dahil etmelidir.

## İskonto Analizi
- **Bulgu:** Verilerdeki aşırı uç değerler (outliers) incelendiğinde %40 üzeri iskontoların, standart satış kanalları dışında (özel projelendirme veya tasfiye) gerçekleştiği tespit edilmiştir.
- **Karar (Eşik):** `max_iskonto_orani = 40.0`
- **Aksiyon:** Sistem %40 üzeri iskontoları otomatik incelemeye alır.
