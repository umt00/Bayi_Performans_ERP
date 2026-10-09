// mockData.js - Gerçekçi demo ve fallback verileri (Genişletilmiş Katalog & Dinamik Üreticiler)

export const MOCK_DEALERS = [
  { bayiKodu: 'B0001', unvan: 'Mega Bilişim Ltd. Şti.', segment: 'A', bolge: 'Marmara' },
  { bayiKodu: 'B0002', unvan: 'Ege Teknoloji Sistemleri', segment: 'B', bolge: 'Ege' },
  { bayiKodu: 'B0003', unvan: 'Başkent Dağıtım A.Ş.', segment: 'A', bolge: 'İç Anadolu' },
  { bayiKodu: 'B0004', unvan: 'Akdeniz Çözüm Ortakları', segment: 'C', bolge: 'Akdeniz' },
  { bayiKodu: 'B0005', unvan: 'Karadeniz İletişim', segment: 'B', bolge: 'Karadeniz' },
  { bayiKodu: 'B0006', unvan: 'Marmara Ağ Teknolojileri', segment: 'A', bolge: 'Marmara' },
  { bayiKodu: 'B0007', unvan: 'Çukurova Dijital Çözümler', segment: 'C', bolge: 'Akdeniz' },
  { bayiKodu: 'B0008', unvan: 'İzmir Kurumsal Donanım', segment: 'B', bolge: 'Ege' },
  { bayiKodu: 'B0009', unvan: 'Anadolu Sistem Entegratörü', segment: 'B', bolge: 'İç Anadolu' },
  { bayiKodu: 'B0010', unvan: 'Bursa İleri Bilişim', segment: 'A', bolge: 'Marmara' },
  { bayiKodu: 'B0011', unvan: 'Gaziantep Endüstriyel Yazılım', segment: 'B', bolge: 'Güneydoğu Anadolu' },
  { bayiKodu: 'B0012', unvan: 'Kayseri Otomasyon Ltd.', segment: 'C', bolge: 'İç Anadolu' },
  { bayiKodu: 'B0013', unvan: 'Antalya Veri Depolama A.Ş.', segment: 'A', bolge: 'Akdeniz' },
  { bayiKodu: 'B0014', unvan: 'Trabzon Bilişim Hizmetleri', segment: 'C', bolge: 'Karadeniz' },
  { bayiKodu: 'B0015', unvan: 'Kocaeli Ağ Çözümleri', segment: 'B', bolge: 'Marmara' },
  { bayiKodu: 'B0016', unvan: 'Denizli Güç Sistemleri', segment: 'B', bolge: 'Ege' },
  { bayiKodu: 'B0017', unvan: 'Eskişehir Siber Güvenlik', segment: 'A', bolge: 'İç Anadolu' },
  { bayiKodu: 'B0018', unvan: 'Samsun İletişim Teknolojileri', segment: 'C', bolge: 'Karadeniz' },
  { bayiKodu: 'B0019', unvan: 'Diyarbakır Yazılım Bilişim', segment: 'C', bolge: 'Güneydoğu Anadolu' },
  { bayiKodu: 'B0020', unvan: 'Erzurum Doğu Bilişim', segment: 'C', bolge: 'Doğu Anadolu' },
  { bayiKodu: 'B0021', unvan: 'Sakarya Donanım Dağıtım', segment: 'B', bolge: 'Marmara' },
  { bayiKodu: 'B0022', unvan: 'Manisa Mikrosistemler', segment: 'B', bolge: 'Ege' },
  { bayiKodu: 'B0023', unvan: 'Konya Ağ Hizmetleri', segment: 'A', bolge: 'İç Anadolu' },
  { bayiKodu: 'B0024', unvan: 'Mersin Lojistik Bilişim', segment: 'B', bolge: 'Akdeniz' },
  { bayiKodu: 'B0025', unvan: 'Balıkesir Bilgisayar A.Ş.', segment: 'C', bolge: 'Marmara' },
]

export const MOCK_PRODUCTS = [
  { urunKodu: 'U1001', urunAdi: 'Endüstriyel Rack Sunucu (2U)', kategori: 'Sunucu', altKategori: 'Kurumsal', birimMaliyet: 84500.0 },
  { urunKodu: 'U1024', urunAdi: 'Fiber Switch 48-Port PoE+', kategori: 'Ağ', altKategori: 'Yönetilebilir', birimMaliyet: 18200.0 },
  { urunKodu: 'U1057', urunAdi: 'Kurumsal NAS Depolama (16 Bay)', kategori: 'Depolama', altKategori: 'NAS', birimMaliyet: 34500.0 },
  { urunKodu: 'U1080', urunAdi: 'NextGen Güvenlik Duvarı UTM', kategori: 'Güvenlik', altKategori: 'Firewall', birimMaliyet: 27900.0 },
  { urunKodu: 'U1102', urunAdi: 'Online UPS 10kVA Güç Kaynağı', kategori: 'Güç', altKategori: 'UPS', birimMaliyet: 41200.0 },
  { urunKodu: 'U1145', urunAdi: 'Wi-Fi 7 Kurumsal Access Point', kategori: 'Ağ', altKategori: 'Kablosuz', birimMaliyet: 7600.0 },
  { urunKodu: 'U1180', urunAdi: 'Blade Sunucu Kasası 10U', kategori: 'Sunucu', altKategori: 'Blade', birimMaliyet: 125000.0 },
  { urunKodu: 'U1210', urunAdi: 'SAN Storage Area Network Switch', kategori: 'Ağ', altKategori: 'SAN', birimMaliyet: 49000.0 },
  { urunKodu: 'U1255', urunAdi: 'NVMe All-Flash Depolama Ünitesi', kategori: 'Depolama', altKategori: 'SAN', birimMaliyet: 98000.0 },
]

export const MOCK_WAREHOUSES = [
  { depoKodu: 'DP01', depoAdi: 'Gebze Lojistik ve Dağıtım Merkezi', bolge: 'Marmara' },
  { depoKodu: 'DP02', depoAdi: 'İzmir Aliağa Bölge Deposu', bolge: 'Ege' },
  { depoKodu: 'DP03', depoAdi: 'Ankara Lojistik Üssü (Kazan)', bolge: 'İç Anadolu' },
  { depoKodu: 'DP04', depoAdi: 'Mersin Serbest Bölge Deposu', bolge: 'Akdeniz' },
  { depoKodu: 'DP05', depoAdi: 'Trabzon Liman Lojistik Merkezi', bolge: 'Karadeniz' },
  { depoKodu: 'DP06', depoAdi: 'Gaziantep Organize Sanayi Deposu', bolge: 'Güneydoğu Anadolu' },
  { depoKodu: 'DP07', depoAdi: 'Bursa Nilüfer Dağıtım Merkezi', bolge: 'Marmara' },
  { depoKodu: 'DP08', depoAdi: 'Kayseri Lojistik Deposu', bolge: 'İç Anadolu' },
  { depoKodu: 'DP09', depoAdi: 'Antalya Serbest Bölge Deposu', bolge: 'Akdeniz' },
  { depoKodu: 'DP10', depoAdi: 'Erzurum Doğu Lojistik', bolge: 'Doğu Anadolu' },
]

export const MOCK_RISK_LIST = {
  donem: '2025-06',
  toplam: 25,
  bayiler: MOCK_DEALERS.map((d, index) => ({
    bayiKodu: d.bayiKodu,
    bolge: d.bolge,
    segment: d.segment,
    hedefeUlasmaPct: index % 3 === 0 ? 0.95 : index % 3 === 1 ? 0.62 : 0.88,
    ortalamaMarjPct: index % 3 === 0 ? 0.18 : index % 3 === 1 ? 0.09 : 0.16,
    ortalamaIskontoPct: index % 3 === 0 ? 0.10 : index % 3 === 1 ? 0.22 : 0.12,
    riskSkoru: index % 3 === 1 ? 85 : index % 3 === 0 ? 15 : 40,
    riskSeviyesi: index % 3 === 1 ? 'YUKSEK' : index % 3 === 0 ? 'DUSUK' : 'ORTA',
    gerekceKodlari: index % 3 === 1 ? ['MARJ_ESIK_ALTI', 'YUKSEK_ISKONTO'] : ['MARJ_UYGUN'],
    kritikStokVarMi: index % 2 === 0,
  })),
}

export const MOCK_PERFORMANCES = {
  B001: {
    bayiKodu: 'B001',
    segment: 'A',
    bolge: 'Marmara',
    donem: '2025-06',
    gerceklesenCiro: 2850000.0,
    hedefCiro: 3000000.0,
    hedefeUlasmaPct: 0.95,
    ortalamaMarjPct: 0.182,
    marjEsigiPct: 0.15,
    ortalamaIskontoPct: 0.105,
    iadeOraniPct: 0.012,
    kritikStokUrunSayisi: 0,
    riskSkoru: 15,
    riskSeviyesi: 'DUSUK',
    gerekceKodlari: ['MARJ_UYGUN', 'HEDEF_OK'],
    aiComment: 'Bayi hedeflerini büyük oranda karşılamış ve iskonto sınırına sadık kalmıştır.',
    alertMessageDraft: 'Herhangi bir müdahale gerekmemektedir.',
    commentSource: 'DETERMINISTIC_RULES',
  },
}

export const MOCK_CRITICAL_STOCK = {
  hesaplamaTarihi: new Date().toISOString(),
  toplam: 3,
  urunler: [
    { urunKodu: 'U1057', depoKodu: 'DP01', mevcutStok: 4, minStokSeviyesi: 15, sonUcAySatisAdedi: 96, ortalamaGunlukSatis: 1.07, tahminiStokGunu: 3.7, gerekceKodlari: ['STOK_MIN_ALTI', 'STOK_GUNU_KRITIK'] },
    { urunKodu: 'U1024', depoKodu: 'DP02', mevcutStok: 2, minStokSeviyesi: 10, sonUcAySatisAdedi: 54, ortalamaGunlukSatis: 0.60, tahminiStokGunu: 3.3, gerekceKodlari: ['STOK_MIN_ALTI', 'STOK_GUNU_KRITIK'] },
    { urunKodu: 'U1001', depoKodu: 'DP01', mevcutStok: 1, minStokSeviyesi: 5, sonUcAySatisAdedi: 28, ortalamaGunlukSatis: 0.31, tahminiStokGunu: 3.2, gerekceKodlari: ['STOK_MIN_ALTI', 'STOK_GUNU_KRITIK'] },
  ],
}

export function generateDynamicMockPerformance(kod, donem = '2025-06') {
  return {
    bayiKodu: kod || 'B001',
    donem: donem,
    segment: 'B',
    bolge: 'Marmara',
    gerceklesenCiro: 1250000.0,
    hedefCiro: 1500000.0,
    hedefeUlasmaPct: 0.833,
    ortalamaMarjPct: 0.165,
    marjEsigiPct: 0.15,
    ortalamaIskontoPct: 0.11,
    iadeOraniPct: 0.02,
    kritikStokUrunSayisi: 1,
    riskSkoru: 25,
    riskSeviyesi: 'DUSUK',
    gerekceKodlari: ['MARJ_UYGUN'],
    aiComment: 'Bayi performansı hedef sınırlar içerisindedir.',
    alertMessageDraft: 'Performans stabil devam etmektedir.',
    commentSource: 'DETERMINISTIC_RULES',
  }
}

export function generateMockHistory(kod, aySayisi = 12) {
  const list = []
  for (let i = aySayisi; i >= 1; i--) {
    list.push({
      donem: `2024-${String(i).padStart(2, '0')}`,
      ciro: Math.floor(800000 + Math.random() * 500000),
      marjPct: 0.15 + (Math.random() * 0.05),
      riskSkoru: Math.floor(10 + Math.random() * 30),
    })
  }
  return list
}
