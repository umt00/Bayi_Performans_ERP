// ReasonCodes.jsx - Risk ve Karar Gerekçe Kodları Rozetleri (thresholds.json Kurallarıyla %100 Uyumlu)

const REASON_LABELS = {
  // --- Eşik Altı / Risk Kriterleri (Danger / Warning) ---
  MARJ_ESIK_ALTI: { label: 'Marj Eşik Altı', detail: 'Marj Eşik Altı (<%15)', type: 'danger' },
  HEDEF_ALTINDA: { label: 'Hedef Altında', detail: 'Hedef Altında (<%80)', type: 'danger' },
  YUKSEK_ISKONTO: { label: 'Yüksek İskonto', detail: 'Yüksek İskonto (>%15)', type: 'danger' },
  STOK_YETERSIZ: { label: 'Kritik Stok', detail: 'Kritik Stok (<7 Gün)', type: 'danger' },
  YUKSEK_IADE: { label: 'Yüksek İade', detail: 'Yüksek İade (>=%10)', type: 'danger' },
  ZARARINA_SATIS: { label: 'Zararına Satış', detail: 'Zararına Satış (<=%0)', type: 'danger' },
  NEGATIF_MARJ: { label: 'Negatif Marj', detail: 'Negatif Marj (<=%0)', type: 'danger' },
  KRITIK_DUSUK_MARJ: { label: 'Kritik Düşük Marj', detail: 'Kritik Düşük Marj (<%5)', type: 'danger' },
  KRITIK_DEPO_STOK: { label: 'Kritik Depo Stoğu', detail: 'Kritik Depo Stoğu (<7 Gün)', type: 'danger' },
  STOK_GUNU_KRITIK: { label: 'Kritik Kalan Gün', detail: 'Kritik Kalan Gün (<7 Gün)', type: 'danger' },
  DINAMIK_ESIK_ALTI: { label: 'Dinamik Eşik Altı', detail: 'Dinamik Eşik Altı (7 Gün)', type: 'danger' },

  // --- Veri Doğrulama Hataları ---
  VERI_EKSIK_MALIYET: { label: 'Eksik Maliyet', detail: 'Eksik Maliyet Verisi', type: 'danger' },
  MALIYET_BILGISI_YOK: { label: 'Maliyet Yok', detail: 'Maliyet Bilgisi Yok', type: 'danger' },
  GECERSIZ_MIKTAR: { label: 'Geçersiz Miktar', detail: 'Geçersiz Miktar (<=0)', type: 'danger' },
  GECERSIZ_BIRIM_FIYAT: { label: 'Geçersiz Fiyat', detail: 'Geçersiz Fiyat (<=0)', type: 'danger' },
  GECERSIZ_ISKONTO: { label: 'Geçersiz İskonto', detail: 'Geçersiz İskonto', type: 'danger' },

  // --- Başarılı / Standartlara Uygun Kriterler (Success) ---
  MARJ_UYGUN: { label: 'Kârlılık Uygun', detail: 'Kârlılık Uygun (>=%15)', type: 'success' },
  STOK_YETERLI: { label: 'Stok Yeterli', detail: 'Stok Yeterli (>=7 Gün)', type: 'success' },
  HEDEF_BASARILI: { label: 'Hedef Karşılandı', detail: 'Hedef Karşılandı (>=%80)', type: 'success' },
}

export function ReasonCodes({ codes = [], max = 1 }) {
  if (!codes || codes.length === 0) {
    return (
      <span className="reason-chip success" title="Tüm kriterler thresholds.json standartları dahilindedir" style={{ fontSize: '0.66rem', padding: '0.12rem 0.35rem', whiteSpace: 'nowrap' }}>
        ✓ Uygun
      </span>
    )
  }

  const visibleCodes = codes.slice(0, max)
  const remainingCount = codes.length - max

  return (
    <div className="reasons-container" style={{ display: 'flex', flexWrap: 'nowrap', gap: '0.2rem', alignItems: 'center' }}>
      {visibleCodes.map((code) => {
        const item = REASON_LABELS[code] || { label: code, detail: code, type: 'info' }
        return (
          <span
            key={code}
            className={`reason-chip ${item.type}`}
            title={`Gerekçe Kodu: ${code} — ${item.detail || item.label}`}
            style={{ fontSize: '0.66rem', padding: '0.12rem 0.35rem', whiteSpace: 'nowrap' }}
          >
            {item.type === 'success' ? '✓ ' : item.type === 'danger' ? '✕ ' : 'ℹ '}
            {item.label}
          </span>
        )
      })}
      {remainingCount > 0 && (
        <span
          className="reason-chip info"
          title={codes.slice(max).map(c => REASON_LABELS[c]?.detail || c).join(', ')}
          style={{ fontSize: '0.66rem', padding: '0.12rem 0.35rem', whiteSpace: 'nowrap' }}
        >
          +{remainingCount} ek
        </span>
      )}
    </div>
  )
}
export default ReasonCodes
