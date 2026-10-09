// CriticalStock.jsx - Kurumsal Dinamik Kritik Stok ve Tedarik Takip Modülü (vw_dinamik_stok_esigi)
import { useState, useEffect } from 'react'
import { getCriticalStock, getWarehousesList, getProductsList } from '../api/client'
import SearchableSelect from './SearchableSelect'

export function CriticalStock({ initialDepo, initialUrun }) {
  const [items, setItems] = useState([])
  const [warehouses, setWarehouses] = useState([])
  const [products, setProducts] = useState([])
  const [depoKodu, setDepoKodu] = useState(initialDepo || '')
  const [urunKodu, setUrunKodu] = useState(initialUrun || '')
  const [onlyCritical, setOnlyCritical] = useState(true)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    async function loadMeta() {
      const [wRes, pRes] = await Promise.allSettled([getWarehousesList(), getProductsList()])
      if (wRes.status === 'fulfilled' && wRes.value) setWarehouses(wRes.value)
      if (pRes.status === 'fulfilled' && pRes.value) setProducts(pRes.value)
    }
    loadMeta()
  }, [])

  useEffect(() => {
    async function fetchStock() {
      setLoading(true)
      try {
        const res = await getCriticalStock({
          depoKodu: depoKodu || undefined,
          urunKodu: urunKodu || undefined,
          sadeceKritik: onlyCritical,
        })
        const criticalItems = (res.urunler || []).filter(
          (u) => !onlyCritical || u.stokKritikMi === true || (u.tahminiStokGunu != null && Number(u.tahminiStokGunu) < 7)
        )
        setItems(criticalItems)
      } catch (err) {
        console.error('Kritik stok yüklenemedi:', err)
      } finally {
        setLoading(false)
      }
    }
    fetchStock()
  }, [depoKodu, urunKodu, onlyCritical])

  // Depo seçenekleri
  const warehouseOptions = [
    { value: '', label: 'Tüm Depolar' },
    ...warehouses.map((w) => ({
      value: w.depoKodu,
      label: w.depoAdi && w.depoAdi !== w.depoKodu ? `${w.depoKodu} — ${w.depoAdi}` : w.depoKodu,
      sublabel: w.bolge ? `Bölge: ${w.bolge}` : '',
    })),
  ]

  // Ürün seçenekleri
  const productOptions = [
    { value: '', label: 'Tüm Ürünler' },
    ...products.map((p) => ({
      value: p.urunKodu,
      label: p.urunAdi && p.urunAdi !== p.urunKodu ? `${p.urunKodu} — ${p.urunAdi}` : p.urunKodu,
      sublabel: p.kategori ? `Kategori: ${p.kategori}` : '',
    })),
  ]

  return (
    <div className="card-section" style={{ marginTop: '1.5rem', overflow: 'hidden' }}>
      <div className="card-section-header" style={{ marginBottom: '1rem' }}>
        <div className="card-section-title">
          <h2>Depo Dinamik Stok Eşiği ve Tedarik Takibi</h2>
          <span>{items.length} Kayıt</span>
        </div>

        <div className="filter-toolbar" style={{ gap: '0.6rem', flexWrap: 'wrap' }}>
          <div className="filter-item" style={{ minWidth: '200px' }}>
            <label style={{ minWidth: '36px' }}>Depo:</label>
            <SearchableSelect
              value={depoKodu}
              onChange={(val) => setDepoKodu(val)}
              options={warehouseOptions}
              placeholder="Tüm Depolar..."
            />
          </div>

          <div className="filter-item" style={{ minWidth: '220px' }}>
            <label style={{ minWidth: '36px' }}>Ürün:</label>
            <SearchableSelect
              value={urunKodu}
              onChange={(val) => setUrunKodu(val)}
              options={productOptions}
              placeholder="Tüm Ürünler..."
            />
          </div>

          <div className="segmented-filter-bar" style={{ display: 'inline-flex' }}>
            <button
              type="button"
              className={`segmented-filter-btn warning ${onlyCritical ? 'active' : ''}`}
              style={{ padding: '0.35rem 0.75rem', fontSize: '0.74rem' }}
              onClick={() => setOnlyCritical(true)}
            >
              <span>🔴 Yalnızca Kritik (&lt;7 Gün)</span>
            </button>
            <button
              type="button"
              className={`segmented-filter-btn ${!onlyCritical ? 'active' : ''}`}
              style={{ padding: '0.35rem 0.75rem', fontSize: '0.74rem' }}
              onClick={() => setOnlyCritical(false)}
            >
              <span>📋 Tüm Stoklar</span>
            </button>
          </div>
        </div>
      </div>

      <div className="table-responsive" style={{ maxHeight: '520px', overflow: 'auto' }}>
        <table className="data-table" style={{ minWidth: '920px' }}>
          <thead>
            <tr>
              <th style={{ width: '30%', minWidth: '200px', padding: '0.65rem 0.75rem', textAlign: 'left' }}>Ürün Kodu ve Adı</th>
              <th style={{ width: '9%', minWidth: '80px', padding: '0.65rem 0.65rem', textAlign: 'left' }}>Depo</th>
              <th style={{ width: '13%', minWidth: '110px', padding: '0.65rem 0.65rem', textAlign: 'left' }}>Mevcut Stok</th>
              <th style={{ width: '15%', minWidth: '120px', padding: '0.65rem 0.65rem', textAlign: 'left' }}>Son 90 Gün Satış</th>
              <th style={{ width: '15%', minWidth: '120px', padding: '0.65rem 0.65rem', textAlign: 'left' }}>Günlük Satış Hızı</th>
              <th style={{ width: '10%', minWidth: '95px', padding: '0.65rem 0.65rem', textAlign: 'left' }}>Kalan Gün</th>
              <th style={{ width: '10%', minWidth: '95px', padding: '0.65rem 0.75rem', textAlign: 'left' }}>Durum</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan="7" style={{ textAlign: 'center', padding: '2.5rem', color: 'var(--text-muted)' }}>
                  Dinamik stok verileri taranıyor...
                </td>
              </tr>
            ) : items.length === 0 ? (
              <tr>
                <td colSpan="7" style={{ textAlign: 'center', padding: '2.5rem', color: 'var(--state-success-text)' }}>
                  ✓ Seçilen kriterlerde 7 günden az kalan kritik stoklu ürün bulunmamaktadır.
                </td>
              </tr>
            ) : (
              items.map((item, idx) => {
                const kalanGun = item.tahminiStokGunu ?? item.kalanStokGunu
                const isCritical = item.stokKritikMi || (kalanGun !== null && kalanGun < 7)
                const isVeryCritical = kalanGun !== null && kalanGun <= 3

                return (
                  <tr key={`${item.urunKodu}-${item.depoKodu}-${idx}`}>
                    <td className="dealer-code-cell" style={{ padding: '0.6rem 0.85rem' }}>
                      <span style={{ fontWeight: 700, fontFamily: 'var(--font-mono)' }}>{item.urunKodu}</span>
                      {item.urunAdi && item.urunAdi !== item.urunKodu ? ` — ${item.urunAdi}` : ''}
                    </td>
                    <td style={{ padding: '0.6rem 0.75rem' }}>
                      <span className="segment-badge" style={{ fontSize: '0.72rem' }}>{item.depoKodu}</span>
                    </td>
                    <td style={{ padding: '0.6rem 0.75rem', fontWeight: 700, color: isCritical ? 'var(--state-danger-accent)' : 'var(--text-primary)', fontFamily: 'var(--font-mono)' }}>
                      {Number(item.mevcutStok).toFixed(0)} Adet
                    </td>
                    <td style={{ padding: '0.6rem 0.75rem', fontWeight: 600, color: 'var(--text-secondary)', fontFamily: 'var(--font-mono)' }}>
                      {Number(item.son90GunSatis ?? item.sonUcAySatisAdedi ?? 0).toFixed(0)} Adet
                    </td>
                    <td style={{ padding: '0.6rem 0.75rem', color: 'var(--text-muted)' }}>
                      {item.ortalamaGunlukSatis ? `${Number(item.ortalamaGunlukSatis).toFixed(2)} / gün` : '-'}
                    </td>
                    <td style={{ padding: '0.6rem 0.75rem' }}>
                      {kalanGun !== null && kalanGun !== undefined ? (
                        <span
                          style={{
                            fontWeight: 700,
                            padding: '0.2rem 0.5rem',
                            borderRadius: 'var(--radius-sm)',
                            background: isVeryCritical ? 'var(--state-danger-bg)' : isCritical ? 'var(--state-warning-bg)' : 'var(--state-success-bg)',
                            color: isVeryCritical ? 'var(--state-danger-text)' : isCritical ? 'var(--state-warning-text)' : 'var(--state-success-text)',
                            border: `1px solid ${isVeryCritical ? 'var(--state-danger-border)' : isCritical ? 'var(--state-warning-border)' : 'var(--state-success-border)'}`,
                            fontSize: '0.74rem',
                            fontFamily: 'var(--font-mono)',
                          }}
                        >
                          {Number(kalanGun).toFixed(1)} Gün
                        </span>
                      ) : (
                        '-'
                      )}
                    </td>
                    <td style={{ padding: '0.6rem 0.85rem' }}>
                      <span
                        style={{
                          fontSize: '0.7rem',
                          fontWeight: 700,
                          padding: '0.15rem 0.45rem',
                          borderRadius: 'var(--radius-sm)',
                          background: isCritical ? 'var(--state-danger-bg)' : 'var(--state-success-bg)',
                          color: isCritical ? 'var(--state-danger-text)' : 'var(--state-success-text)',
                          border: `1px solid ${isCritical ? 'var(--state-danger-border)' : 'var(--state-success-border)'}`,
                        }}
                      >
                        {isCritical ? 'Kritik' : 'Yeterli'}
                      </span>
                    </td>
                  </tr>
                )
              })
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}
export default CriticalStock
