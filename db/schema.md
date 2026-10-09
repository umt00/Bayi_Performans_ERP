# Veritabanı Şeması

Bu doküman, Supabase (PostgreSQL) üzerinde oluşturulan tabloları ve aralarındaki ilişkileri açıklar.

## 1. Temel ERP / Operasyonel Tablolar

### 1.1 `urunler`
| Kolon | Veri Tipi | Kısıtlama | Açıklama |
|---|---|---|---|
| UrunKodu | VARCHAR(50) | PRIMARY KEY | Ürünün benzersiz kodu. |
| UrunAdi | VARCHAR(255) | NOT NULL | Ürünün açık adı. |
| Kategori | VARCHAR(100) | | Ürün kategorisi. |
| AltKategori | VARCHAR(100) | | Ürün alt kategorisi. |
| BirimMaliyet | NUMERIC(15,2)| | Ürünün birim maliyeti (Gizli veri). |

### 1.2 `bayiler`
| Kolon | Veri Tipi | Kısıtlama | Açıklama |
|---|---|---|---|
| BayiKodu | VARCHAR(50) | PRIMARY KEY | Bayinin benzersiz kodu. |
| BayiAdi | VARCHAR(255) | NOT NULL | Bayinin ticari unvanı veya adı. |
| Sehir | VARCHAR(100) | | Bayinin bulunduğu şehir. |
| Bolge | VARCHAR(100) | | Bayinin bağlı olduğu bölge. |
| Segment | VARCHAR(50) | | Bayi segmenti (KOBİ, Kurumsal, Bireysel). |

### 1.3 `depolar`
| Kolon | Veri Tipi | Kısıtlama | Açıklama |
|---|---|---|---|
| DepoKodu | VARCHAR(50) | PRIMARY KEY | Deponun benzersiz kodu (DP01-DP06). |
| DepoAdi | VARCHAR(255) | NOT NULL | Deponun adı. |
| Sehir | VARCHAR(100) | | Deponun bulunduğu şehir. |
| Bolge | VARCHAR(100) | | Deponun hizmet verdiği bölge. |

### 1.4 `stok` (Şirket Depo Stokları)
| Kolon | Veri Tipi | Kısıtlama | Açıklama |
|---|---|---|---|
| UrunKodu | VARCHAR(50) | PK, FK | Ürün kodu. |
| DepoKodu | VARCHAR(50) | PK, FK | Depo kodu. |
| MevcutStok | NUMERIC(15,2)| DEFAULT 0 | İlgili depodaki mevcut stok adedi. |
| MinStokSeviyesi | NUMERIC(15,2)| DEFAULT 20 | İlgili depodaki minimum stok kuralı. |
| SonSayimTarihi | DATE | | Stoğun en son sayıldığı tarih. |

### 1.5 `hedefler`
| Kolon | Veri Tipi | Kısıtlama | Açıklama |
|---|---|---|---|
| BayiKodu | VARCHAR(50) | PK, FK | Bayi kodu. |
| Yil | INT | PK | Hedef yılı (Örn: 2024). |
| Ay | INT | PK | Hedef ayı (1-12). |
| Ay_Adi | VARCHAR(20) | | Ayın metin adı. |
| Aciklama | TEXT | | Hedefe dair açıklama. |
| Hedef | NUMERIC(15,2)| DEFAULT 0 | Hedeflenen aylık ciro tutarı. |

### 1.6 `satis`
| Kolon | Veri Tipi | Kısıtlama | Açıklama |
|---|---|---|---|
| SatisID | VARCHAR(100)| PRIMARY KEY | İşlemin benzersiz numarası. |
| Tarih | DATE | NOT NULL | Satış tarihi. |
| BayiKodu | VARCHAR(50) | FK | Satışı yapan bayi. |
| UrunKodu | VARCHAR(50) | FK | Satılan ürün. |
| DepoKodu | VARCHAR(50) | FK | Sevkiyatın yapıldığı depo. |
| Miktar | NUMERIC(15,2)| NOT NULL | Satılan miktar. |
| BirimFiyat | NUMERIC(15,2)| NOT NULL | Satış birim fiyatı. |
| IskontoOrani | NUMERIC(5,4)| DEFAULT 0 | İskonto oranı (0.00 - 1.00). |
| SatisKanali | VARCHAR(100)| | Satışın gerçekleştiği kanal. |
| IsIade | BOOLEAN | DEFAULT FALSE | Satışın iade olup olmadığını gösterir. |

---

## 2. Analitik & Karar Destek Tabloları

### 2.1 `bayi_donem_performans` (Aylık Karne)
Aylık Airflow ETL DAG tarafından hesaplanan bayi karne tablosudur:
| Kolon | Veri Tipi | Kısıtlama | Açıklama |
|---|---|---|---|
| bayi_kodu | TEXT | PK, FK | Bayi kodu. |
| donem | DATE | PK | Performansın ait olduğu ayın ilk günü. |
| ciro | NUMERIC | | Gerçekleşen net ciro. |
| hedef | NUMERIC | | O aya ait ciro hedefi. |
| hedef_tutma_orani | NUMERIC | | `(ciro / hedef) * 100` |
| ortalama_kar_marji | NUMERIC | | Bayinin ortalama kâr marjı (%). |
| ortalama_iskonto_orani | NUMERIC | | Bayinin ortalama iskonto oranı (%). |
| ortalama_iade_orani | NUMERIC | | İade oranı (%). |
| kritik_stok_kalan_urun_sayisi | INTEGER | | Depoda kritik seviyedeki ürün sayısı. |
| risk_skoru | INTEGER | | 0-100 ağırlıklı ceza puanı. |
| risk_seviyesi | TEXT | | `DUSUK`, `ORTA`, `YUKSEK`. |
| gerekce_kodlari | TEXT[] | | Ceza gerekçeleri dizisi. |

### 2.2 `teklifler` (Header)
| Kolon | Veri Tipi | Kısıtlama | Açıklama |
|---|---|---|---|
| teklif_id | VARCHAR(40) | PRIMARY KEY | `TEK-YYYYMMDD-XXXX` formatında teklif kodu. |
| bayi_kodu | VARCHAR(100)| FK | Teklif verilen bayi kodu. |
| durum | VARCHAR(30) | | `TASLAK`, `BEKLIYOR`, `DEGERLENDIRILDI`, `ONAYLANDI`, `REDDEDILDI`. |
| olusturulma_tarihi | TIMESTAMPTZ | DEFAULT NOW() | Oluşturulma zamanı. |
| toplam_net_tutar | NUMERIC(18,2)| | Kalemlerin net toplam tutarı. |
| aciklama | TEXT | | Teklif notu. |

### 2.3 `teklif_kalemleri` (Lines)
| Kolon | Veri Tipi | Kısıtlama | Açıklama |
|---|---|---|---|
| teklif_id | VARCHAR(40) | PK, FK | Bağlı teklif kodu. |
| sira_no | INTEGER | PK | Kalem sıra numarası (1, 2, 3..). |
| urun_kodu | VARCHAR(100)| FK | Ürün kodu. |
| depo_kodu | VARCHAR(100)| FK | Sevk deposu (Şehir/bölgeye göre otomatik atanır). |
| miktar | NUMERIC(18,4)| > 0 | Sipariş miktarı. |
| birim_fiyat | NUMERIC(18,4)| >= 0 | Liste birim satış fiyatı. |
| iskonto_orani | NUMERIC(9,6)| [0, 1] | İskonto oranı. |
| net_tutar | NUMERIC(18,2)| | `miktar * birim_fiyat * (1 - iskonto_orani)`. |

### 2.4 `teklif_degerlendirme` (Audit Snapshot Log)
| Kolon | Veri Tipi | Kısıtlama | Açıklama |
|---|---|---|---|
| teklif_id | VARCHAR(40) | PK, FK | Teklif kodu. |
| bayi_kodu | VARCHAR(100)| | Bayi kodu. |
| karar | VARCHAR(30) | | `APPROVED`, `NEED_APPROVAL`, `REJECTED`. |
| risk_skoru | INTEGER | | Teklif anındaki bayi risk skoru. |
| risk_seviyesi | VARCHAR(20) | | `DUSUK`, `ORTA`, `YUKSEK`. |
| gerekce_kodlari | TEXT[] | | Karara yol açan gerekçeler. |
| bayi_marji | NUMERIC | | Bayi güncel ortalama marjı (%). |
| hedef_tutturma_yuzdesi | NUMERIC | | Hedef tutturma oranı (%). |
| ortalama_iskonto_orani | NUMERIC | | Ortalama iskonto oranı (%). |
| iade_orani | NUMERIC | | İade oranı (%). |
| degerlendirme_tarihi | TIMESTAMPTZ | DEFAULT NOW() | Kararın verildiği zaman damgası. |

### 2.5 `teklif_ai_yorumlari` (LLM Yönetici Özeti)
| Kolon | Veri Tipi | Kısıtlama | Açıklama |
|---|---|---|---|
| teklif_id | VARCHAR(40) | PK, FK | Teklif kodu (1:1 ilişki). |
| ai_comment | TEXT | | Türkçe yönetici karar özeti. |
| alert_message | TEXT | | Satış yöneticisi uyarı taslağı. |
| is_fallback | BOOLEAN | | Fallback kural motoru şablonu bayrağı. |
| created_time | TIMESTAMPTZ | DEFAULT NOW() | Üretilme zamanı. |
