// DealerAnalysis.jsx - Kurumsal Bayi ve Ürün Analiz Modülü (Ekrana Tam Sığan Tablolar & Dinamik Stok)
import { useState, useEffect, useMemo } from 'react'
import {
  getDealerPerformance,
  getDealersList,
  getProductsList,
  getWarehousesList,
  getCriticalStock,
  getDealerHistory,
  getDefaultWarehouseForDealer,
  getFilterOptions,
} from '../api/client'
import MetricsCard from './MetricsCard'
import RiskBadge from './RiskBadge'
import ReasonCodes from './ReasonCodes'
import SearchableSelect from './SearchableSelect'

export function DealerAnalysis({ selectedDealerCode, onSelectDealer, selectedPeriod = '2026-08', onPeriodChange }) {
  const [dealers, setDealers] = useState([])
  const [products, setProducts] = useState([])
  const [warehouses, setWarehouses] = useState([])
  const [availablePeriods, setAvailablePeriods] = useState([])

  const [dealerCode, setDealerCode] = useState(selectedDealerCode || '')
  const [prevSelectedDealerCode, setPrevSelectedDealerCode] = useState(selectedDealerCode)

  if (selectedDealerCode !== prevSelectedDealerCode) {
    setPrevSelectedDealerCode(selectedDealerCode)
    if (selectedDealerCode) setDealerCode(selectedDealerCode)
  }

  const [donem, setDonem] = useState(selectedPeriod || '2026-08')
  const [prevSelectedPeriod, setPrevSelectedPeriod] = useState(selectedPeriod)

  if (selectedPeriod !== prevSelectedPeriod) {
    setPrevSelectedPeriod(selectedPeriod)
    if (selectedPeriod) setDonem(selectedPeriod)
  }

  const [perf, setPerf] = useState(null)
  const [dealerHistory, setDealerHistory] = useState([])
  const [dealerCriticalStocks, setDealerCriticalStocks] = useState([])
  const [dealerStockLoading, setDealerStockLoading] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)

  // Ürün & Depo Dinamik Stok Kontrolü
  const [selectedProductCode, setSelectedProductCode] = useState('')
  const [selectedWarehouseCode, setSelectedWarehouseCode] = useState('')
  const [productStockList, setProductStockList] = useState([])
  const [stockLoading, setStockLoading] = useState(false)

  const handlePeriodSelect = (val) => {
    setDonem(val)
    if (onPeriodChange) onPeriodChange(val)
  }

  useEffect(() => {
    async function loadCatalog() {
      const [dRes, pRes, wRes, fRes] = await Promise.allSettled([
        getDealersList(),
        getProductsList(),
        getWarehousesList(),
        getFilterOptions(),
      ])
      if (dRes.status === 'fulfilled' && dRes.value) setDealers(dRes.value)
      if (pRes.status === 'fulfilled' && pRes.value) setProducts(pRes.value)
      if (wRes.status === 'fulfilled' && wRes.value) setWarehouses(wRes.value)
      if (fRes.status === 'fulfilled' && fRes.value?.donemler) setAvailablePeriods(fRes.value.donemler)
    }
    loadCatalog()
  }, [])

  // Bayi ve Dönem Performansı Çekme
  useEffect(() => {
    async function fetchPerformance() {
      if (!dealerCode) {
        setPerf(null)
        setDealerHistory([])
        setDealerCriticalStocks([])
        setError(null)
        return
      }
      setLoading(true)
      setDealerStockLoading(true)
      setError(null)

      const matchedDealer = dealers.find((d) => d.bayiKodu === dealerCode)
      const dealerDepo = matchedDealer?.varsayilanDepoKodu || (matchedDealer ? getDefaultWarehouseForDealer(matchedDealer) : undefined)

      try {
        const [perfRes, histRes, stockRes] = await Promise.allSettled([
          getDealerPerformance(dealerCode, donem),
          getDealerHistory(dealerCode, 60),
          getCriticalStock({
            depoKodu: dealerDepo,
            bayiKodu: dealerCode,
            sadeceKritik: true,
          }),
        ])

        if (perfRes.status === 'rejected') {
          setError(perfRes.reason?.message || `${dealerCode} bayisi için ${donem} döneminde performans verisi çekilemedi.`)
        }

        const perfData = perfRes.status === 'fulfilled' ? perfRes.value : null
        const historyData = histRes.status === 'fulfilled' ? histRes.value : []
        const stockData = stockRes.status === 'fulfilled' ? stockRes.value : { urunler: [] }

        const validCriticalItems = (stockData?.urunler || []).filter(
          (u) => u.stokKritikMi === true || (u.tahminiStokGunu != null && Number(u.tahminiStokGunu) < 7)
        )

        setPerf(perfData)
        setDealerHistory(historyData || [])
        setDealerCriticalStocks(validCriticalItems)
      } catch (err) {
        setError(err.message || 'Bayi verileri çekilirken sunucu hatası oluştu.')
      } finally {
        setLoading(false)
        setDealerStockLoading(false)
      }
    }
    fetchPerformance()
  }, [dealerCode, donem, dealers])

  // Ürün & Depo Dinamik Stok Arama
  useEffect(() => {
    async function fetchProductStock() {
      if (!selectedProductCode && !selectedWarehouseCode) {
        setProductStockList([])
        setStockLoading(false)
        return
      }

      setStockLoading(true)
      try {
        const isBothSelected = Boolean(selectedProductCode && selectedWarehouseCode)
        const res = await getCriticalStock({
          urunKodu: selectedProductCode || undefined,
          depoKodu: selectedWarehouseCode || undefined,
          sadeceKritik: !isBothSelected,
        })
        const items = (res.urunler || []).filter(
          (u) => isBothSelected || u.stokKritikMi === true || (u.tahminiStokGunu != null && Number(u.tahminiStokGunu) < 7)
        )
        setProductStockList(items)
      } catch (err) {
        console.error('Ürün/Depo dinamik stok verisi alınamadı:', err)
        setProductStockList([])
      } finally {
        setStockLoading(false)
      }
    }
    fetchProductStock()
  }, [selectedProductCode, selectedWarehouseCode])

  const formatCurrency = (amount) => {
    if (amount === null || amount === undefined) return '-'
    return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', maximumFractionDigits: 0 }).format(amount)
  }

  const sortedDealers = useMemo(() => {
    return [...dealers].sort((a, b) =>
      (a.bayiKodu || '').localeCompare(b.bayiKodu || '', undefined, { numeric: true, sensitivity: 'base' })
    )
  }, [dealers])

  const dealerOptions = useMemo(() => {
    return sortedDealers.map((d) => ({
      value: d.bayiKodu,
      label: d.unvan ? `${d.bayiKodu} — ${d.unvan}` : `${d.bayiKodu} Bayisi`,
      sublabel: `Segment: ${d.segment || '-'} | Bölge: ${d.bolge || '-'}`,
    }))
  }, [sortedDealers])

  const productOptions = useMemo(() => {
    return [
      { value: '', label: 'Tüm Ürünler' },
      ...products.map((p) => ({
        value: p.urunKodu,
        label: p.urunAdi ? `${p.urunKodu} — ${p.urunAdi}` : p.urunKodu,
        sublabel: `${p.kategori || 'Genel'} | Birim Maliyet: ${p.birimMaliyet ? `${p.birimMaliyet} ₺` : 'Bilinmiyor'}`,
      })),
    ]
  }, [products])

  const warehouseOptions = useMemo(() => {
    return [
      { value: '', label: 'Tüm Depolar' },
      ...warehouses.map((w) => ({
        value: w.depoKodu,
        label: w.depoAdi && w.depoAdi !== w.depoKodu ? `${w.depoKodu} — ${w.depoAdi}` : w.depoKodu,
        sublabel: w.bolge ? `Bölge: ${w.bolge}` : '',
      })),
    ]
  }, [warehouses])

  const handleSelectDealer = (kod) => {
    setDealerCode(kod || '')
    if (onSelectDealer && kod) onSelectDealer(kod)
  }

  const currentDealerMeta = useMemo(() => {
    const found = sortedDealers.find((d) => d.bayiKodu === dealerCode)
    return {
      unvan: found?.unvan || (dealerCode ? `${dealerCode} Bayi Kaydı` : ''),
      segment: found?.segment || perf?.segment || '',
      bolge: found?.bolge || perf?.bolge || '',
      sehir: found?.sehir || '',
      varsayilanDepoKodu: found?.varsayilanDepoKodu || (found ? getDefaultWarehouseForDealer(found) : 'DP01'),
    }
  }, [sortedDealers, dealerCode, perf])

  const targetProgress = perf?.hedefeUlasmaPct != null
    ? Math.min(Math.round(perf.hedefeUlasmaPct > 5 ? perf.hedefeUlasmaPct : perf.hedefeUlasmaPct * 100), 100)
    : 0

  return (
    <div className="animate-fade-in">
      <div className="page-header">
        <div className="page-title">
          <h1>Bayi ve Ürün Analiz Merkezi</h1>
          <p>Dönemsel ciro gerçekleşmesi, iskonto/marj analizi ve depo dinamik stok takibi</p>
        </div>

        <div className="filter-toolbar">
          <div className="filter-item">
            <label>Dönem</label>
            <select className="filter-select" value={donem} onChange={(e) => handlePeriodSelect(e.target.value)}>
              {availablePeriods.length > 0 ? (
                availablePeriods.map((p) => (
                  <option key={p} value={p}>
                    {p} {p === '2026-08' ? '(Güncel Dönem)' : ''}
                  </option>
                ))
              ) : (
                <>
                  <option value="2026-08">2026-08 (Güncel Dönem)</option>
                  <option value="2025-06">2025-06 (Haziran 2025)</option>
                  <option value="2025-05">2025-05 (Mayıs 2025)</option>
                  <option value="2025-04">2025-04 (Nisan 2025)</option>
                  <option value="2024-12">2024-12 (Aralık 2024)</option>
                  <option value="2024-06">2024-06 (Haziran 2024)</option>
                  <option value="2024-01">2024-01 (Ocak 2024)</option>
                </>
              )}
            </select>
          </div>
        </div>
      </div>

      {/* ─── BAYİ SEÇİM PANELİ ─── */}
      <div className="card-section" style={{ marginBottom: '1.5rem' }}>
        <div className="card-section-header" style={{ marginBottom: '0.85rem' }}>
          <div className="card-section-title">
            <h2>Analiz Edilecek Bayi Seçimi</h2>
            {dealerCode && <span>{dealerCode}</span>}
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', width: '460px', maxWidth: '100%' }}>
            <div style={{ flex: 1 }}>
              <SearchableSelect
                value={dealerCode}
                onChange={(val) => handleSelectDealer(val)}
                options={dealerOptions}
                placeholder="Bayi Kodu veya Unvanı Yazın/Seçin (Örn: B0001)"
              />
            </div>
            {dealerCode && (
              <button
                type="button"
                className="action-btn-sm"
                style={{ padding: '0.45rem 0.65rem', fontSize: '0.74rem', whiteSpace: 'nowrap' }}
                onClick={() => handleSelectDealer('')}
                title="Bayi seçimini temizle"
              >
                Temizle ✕
              </button>
            )}
          </div>
        </div>

        {/* Hızlı Seçim Bayi Kartları */}
        <div>
          <div style={{ fontSize: '0.74rem', color: 'var(--text-muted)', fontWeight: 600, textTransform: 'uppercase', marginBottom: '0.5rem' }}>
            Hızlı Seçim Bayi Kartları ({sortedDealers.length} Kayıt):
          </div>
          <div style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fill, minmax(220px, 1fr))',
            gap: '0.5rem',
            maxHeight: '160px',
            overflowY: 'auto',
            padding: '0.2rem',
          }}>
            {sortedDealers.map((d) => {
              const isActive = d.bayiKodu === dealerCode
              const initial = (d.segment || d.bayiKodu || 'B').trim()[0].toUpperCase()
              return (
                <button
                  key={d.bayiKodu}
                  type="button"
                  onClick={() => handleSelectDealer(d.bayiKodu)}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '0.6rem',
                    padding: '0.5rem 0.75rem',
                    borderRadius: 'var(--radius-sm)',
                    border: `1px solid ${isActive ? 'var(--brand-primary)' : 'var(--border-subtle)'}`,
                    background: isActive ? 'var(--brand-primary-subtle)' : 'var(--bg-surface-elevated)',
                    textAlign: 'left',
                    cursor: 'pointer',
                    transition: 'all 0.15s ease',
                  }}
                >
                  <div style={{
                    width: '28px',
                    height: '28px',
                    borderRadius: '5px',
                    background: isActive ? 'var(--brand-primary)' : 'var(--border-medium)',
                    color: isActive ? '#fff' : 'var(--text-primary)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    fontSize: '0.78rem',
                    fontWeight: 700,
                    fontFamily: 'var(--font-mono)',
                    flexShrink: 0,
                  }}>
                    {initial}
                  </div>
                  <div style={{ overflow: 'hidden', minWidth: 0, flex: 1 }}>
                    <div style={{ fontSize: '0.82rem', fontWeight: 600, color: isActive ? 'var(--brand-primary)' : 'var(--text-primary)', fontFamily: 'var(--font-mono)' }}>
                      {d.bayiKodu}
                    </div>
                    <div style={{ fontSize: '0.72rem', color: 'var(--text-muted)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      {d.unvan || d.bolge || d.segment}
                    </div>
                  </div>
                </button>
              )
            })}
          </div>
        </div>
      </div>

      {/* ─── PERFORMANS DETAYLARI ─── */}
      {!dealerCode ? (
        <div className="card-section" style={{ textAlign: 'center', padding: '3.5rem 2rem' }}>
          <div style={{ fontSize: '2.5rem', marginBottom: '0.75rem', opacity: 0.85 }}>🏢</div>
          <h3 style={{ color: 'var(--text-primary)', fontSize: '1.15rem', marginBottom: '0.4rem', fontWeight: 700 }}>
            Bayi Analizi İçin Seçim Bekleniyor
          </h3>
          <p style={{ color: 'var(--text-secondary)', fontSize: '0.88rem', maxWidth: '520px', margin: '0 auto 1.25rem', lineHeight: 1.5 }}>
            Detaylı finansal karnesini, hedef gerçekleşme oranlarını, geçmiş dönem trendini ve bağlı depo stok durumunu incelemek istediğiniz bayiyi yukarıdaki arama kutusundan arayınız veya bayi kartlarına tıklayınız.
          </p>
        </div>
      ) : loading ? (
        <div className="card-section" style={{ textAlign: 'center', padding: '3.5rem' }}>
          <div style={{ fontSize: '1.8rem', marginBottom: '0.5rem' }}>⏳</div>
          <p style={{ color: 'var(--text-secondary)', fontWeight: 600 }}>Performans ve dinamik stok analitiği hesaplanıyor...</p>
        </div>
      ) : !perf ? (
        <div className="card-section" style={{ textAlign: 'center', padding: '3rem' }}>
          <p style={{ color: 'var(--state-danger-text)', fontWeight: 600 }}>
            {error || `${dealerCode} kodlu bayi için ${donem} döneminde performans kaydı bulunamadı.`}
          </p>
        </div>
      ) : (
        <>
          {/* Bayi Başlık Özeti */}
          <div className="dealer-header-card">
            <div className="dealer-main-info">
              <div className="dealer-avatar-large">{perf.bayiKodu}</div>
              <div className="dealer-details">
                <h2>{currentDealerMeta.unvan || `${perf.bayiKodu} Bayi Performansı`}</h2>
                <div className="dealer-meta-tags">
                  <span className="segment-badge">Segment: {perf.segment || currentDealerMeta.segment || '-'}</span>
                  <span className="segment-badge">Bölge: {perf.bolge || currentDealerMeta.bolge || '-'}</span>
                  <span className="segment-badge">Bağlı Depo: {currentDealerMeta.varsayilanDepoKodu}</span>
                  <span className="segment-badge">Dönem: {perf.donem || donem}</span>
                </div>
              </div>
            </div>

            <div style={{ textAlign: 'right' }}>
              <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginBottom: '0.25rem', fontWeight: 500 }}>
                Risk Durumu
              </div>
              <RiskBadge level={perf.riskSeviyesi} score={perf.riskSkoru} />
            </div>
          </div>

          {/* KPI İstatistikleri */}
          <div className="kpi-grid">
            <MetricsCard
              title="Gerçekleşen Ciro"
              value={formatCurrency(perf.gerceklesenCiro || perf.ciro)}
              subtext={perf.hedefCiro || perf.hedef ? `Bütçe Hedefi: ${formatCurrency(perf.hedefCiro || perf.hedef)}` : 'Bütçe Hedefi: Belirtilmemiş'}
              badgeText={perf.hedefeUlasmaPct != null ? `%${(perf.hedefeUlasmaPct > 5 ? perf.hedefeUlasmaPct : perf.hedefeUlasmaPct * 100).toFixed(1)} Hedef` : undefined}
              badgeType={(perf.hedefeUlasmaPct > 5 ? perf.hedefeUlasmaPct / 100 : perf.hedefeUlasmaPct) >= 0.8 ? 'success' : 'warning'}
            />

            <MetricsCard
              title="Ortalama Kâr Marjı"
              value={perf.ortalamaMarjPct != null ? `%${(perf.ortalamaMarjPct > 5 ? perf.ortalamaMarjPct : perf.ortalamaMarjPct * 100).toFixed(1)}` : '-'}
              subtext={`Referans Marj Eşiği: %${(perf.marjEsigiPct > 5 ? perf.marjEsigiPct : (perf.marjEsigiPct || 0.15) * 100).toFixed(1)}`}
              badgeText={(perf.ortalamaMarjPct > 5 ? perf.ortalamaMarjPct / 100 : perf.ortalamaMarjPct) >= (perf.marjEsigiPct > 5 ? perf.marjEsigiPct / 100 : (perf.marjEsigiPct || 0.15)) ? 'Marj Uygun' : 'Eşik Altı'}
              badgeType={(perf.ortalamaMarjPct > 5 ? perf.ortalamaMarjPct / 100 : perf.ortalamaMarjPct) >= (perf.marjEsigiPct > 5 ? perf.marjEsigiPct / 100 : (perf.marjEsigiPct || 0.15)) ? 'success' : 'danger'}
            />

            <MetricsCard
              title="Ortalama İskonto"
              value={perf.ortalamaIskontoPct != null ? `%${(perf.ortalamaIskontoPct > 5 ? perf.ortalamaIskontoPct : perf.ortalamaIskontoPct * 100).toFixed(1)}` : '-'}
              subtext="Maksimum Eşik: %15.0"
              badgeText={(perf.ortalamaIskontoPct > 5 ? perf.ortalamaIskontoPct / 100 : perf.ortalamaIskontoPct) <= 0.15 ? 'Normal' : 'Yüksek İskonto'}
              badgeType={(perf.ortalamaIskontoPct > 5 ? perf.ortalamaIskontoPct / 100 : perf.ortalamaIskontoPct) <= 0.15 ? 'success' : 'warning'}
            />

            <MetricsCard
              title="İade Oranı"
              value={perf.iadeOraniPct != null ? `%${(perf.iadeOraniPct > 5 ? perf.iadeOraniPct : perf.iadeOraniPct * 100).toFixed(1)}` : '%0.0'}
              subtext="Kritik Eşik: %10.0"
              badgeText={((perf.iadeOraniPct > 5 ? perf.iadeOraniPct / 100 : perf.iadeOraniPct) || 0) < 0.1 ? 'Düşük İade' : 'Kritik İade'}
              badgeType={((perf.iadeOraniPct > 5 ? perf.iadeOraniPct / 100 : perf.iadeOraniPct) || 0) < 0.1 ? 'success' : 'danger'}
            />
          </div>

          {/* Geçmiş Performans Eğilimi */}
          {(() => {
            const activeHistory = (dealerHistory || []).filter((h) => {
              const ciroVal = h.gerceklesenCiro ?? h.ciro ?? 0
              const rawMarj = h.ortalamaMarjPct ?? h.marjPct ?? 0
              return ciroVal > 0 || Math.abs(rawMarj) > 0.0001
            })

            return (
              <div className="card-section" style={{ padding: '1.15rem 1.35rem', marginBottom: '1.5rem' }}>
                <div className="card-section-title" style={{ marginBottom: '0.85rem' }}>
                  <h2>Dönemsel Satış ve Marj Gelişim Trendi</h2>
                  {activeHistory.length > 0 && (
                    <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                      ({activeHistory.length} Aktif Dönem)
                    </span>
                  )}
                </div>

                {activeHistory.length > 0 ? (
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: '0.6rem' }}>
                    {activeHistory.map((h, hIdx) => {
                      const ciroVal = h.gerceklesenCiro ?? h.ciro ?? 0
                      const rawMarj = h.ortalamaMarjPct ?? h.marjPct ?? 0
                      const marjVal = rawMarj > 5 ? rawMarj / 100 : rawMarj
                      return (
                        <div
                          key={hIdx}
                          style={{
                            background: 'var(--bg-input)',
                            padding: '0.65rem 0.5rem',
                            borderRadius: 'var(--radius-sm)',
                            border: '1px solid var(--border-subtle)',
                            textAlign: 'center',
                          }}
                        >
                          <div style={{ fontSize: '0.72rem', color: 'var(--text-muted)', fontWeight: 600 }}>{h.donem}</div>
                          <div style={{ fontSize: '0.88rem', fontWeight: 700, marginTop: '0.2rem', fontFamily: 'var(--font-mono)' }}>{formatCurrency(ciroVal)}</div>
                          <div style={{ fontSize: '0.72rem', color: marjVal >= 0.15 ? 'var(--state-success-accent)' : 'var(--state-danger-accent)', fontWeight: 600, marginTop: '0.15rem' }}>
                            Marj: %{(marjVal * 100).toFixed(1)}
                          </div>
                        </div>
                      )
                    })}
                  </div>
                ) : (
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: '0.6rem',
                      padding: '0.75rem 1rem',
                      background: 'var(--bg-surface-elevated)',
                      borderRadius: 'var(--radius-sm)',
                      border: '1px dashed var(--border-subtle)',
                      color: 'var(--text-secondary)',
                      fontSize: '0.85rem',
                    }}
                  >
                    <span style={{ fontSize: '1.1rem', opacity: 0.85 }}>ℹ️</span>
                    <span>Bu bayi için satış hareketi gerçekleşmiş geçmiş dönem kaydı bulunmamaktadır.</span>
                  </div>
                )}
              </div>
            )
          })()}

          {/* Hedef ve Risk Özeti */}
          <div className="performance-grid" style={{ marginBottom: '1.5rem' }}>
            <div className="card-section" style={{ margin: 0 }}>
              <div className="card-section-title" style={{ marginBottom: '1rem' }}>
                <h2>Hedef Gerçekleşme Analizi</h2>
              </div>

              <div style={{ marginBottom: '1.25rem' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.85rem', marginBottom: '0.35rem' }}>
                  <span style={{ color: 'var(--text-secondary)' }}>Bütçe Karşılama Oranı</span>
                  <span style={{ fontWeight: 600, color: (perf.hedefeUlasmaPct > 5 ? perf.hedefeUlasmaPct / 100 : perf.hedefeUlasmaPct) >= 0.8 ? 'var(--state-success-accent)' : 'var(--state-warning-accent)' }}>
                    {perf.hedefeUlasmaPct != null
                      ? `%${(Math.abs(perf.hedefeUlasmaPct) > 5 ? perf.hedefeUlasmaPct : perf.hedefeUlasmaPct * 100).toFixed(1)}`
                      : 'Tanımsız'}
                  </span>
                </div>
                <div className="progress-bar-container">
                  <div
                    className="progress-bar-fill"
                    style={{
                      width: `${targetProgress}%`,
                      backgroundColor: (perf.hedefeUlasmaPct > 5 ? perf.hedefeUlasmaPct / 100 : perf.hedefeUlasmaPct) >= 0.8 ? 'var(--state-success-accent)' : 'var(--state-warning-accent)',
                    }}
                  />
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                  <span>Gerçekleşen: {formatCurrency(perf.gerceklesenCiro || perf.ciro)}</span>
                  <span>Hedef: {formatCurrency(perf.hedefCiro || perf.hedef)}</span>
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                <div style={{ background: 'var(--bg-input)', padding: '0.85rem 1rem', borderRadius: 'var(--radius-sm)', border: '1px solid var(--border-subtle)' }}>
                  <div style={{ fontSize: '0.72rem', color: 'var(--text-muted)', textTransform: 'uppercase', fontWeight: 600 }}>
                    Kritik Stok Kalemi
                  </div>
                  <div style={{ fontSize: '1.35rem', fontWeight: 800, marginTop: '0.25rem', color: dealerCriticalStocks.length > 0 ? 'var(--state-danger-accent)' : 'var(--state-success-accent)' }}>
                    {dealerCriticalStocks.length} Kalem
                  </div>
                </div>

                <div style={{ background: 'var(--bg-input)', padding: '0.85rem 1rem', borderRadius: 'var(--radius-sm)', border: '1px solid var(--border-subtle)' }}>
                  <div style={{ fontSize: '0.72rem', color: 'var(--text-muted)', textTransform: 'uppercase', fontWeight: 600 }}>
                    Marj Sapması
                  </div>
                  <div style={{ fontSize: '1.35rem', fontWeight: 800, marginTop: '0.25rem', color: (perf.ortalamaMarjPct || 0) >= (perf.marjEsigiPct || 0.15) ? 'var(--state-success-accent)' : 'var(--state-danger-accent)' }}>
                    {perf.ortalamaMarjPct != null ? `%${(((perf.ortalamaMarjPct - (perf.marjEsigiPct || 0.15)) * 100)).toFixed(1)}` : '-'}
                  </div>
                </div>
              </div>
            </div>

            <div className="card-section" style={{ margin: 0 }}>
              <div className="card-section-title" style={{ marginBottom: '1rem' }}>
                <h2>Risk Değerlendirmesi</h2>
              </div>

              <div style={{ textAlign: 'center', padding: '0.5rem 0 1rem' }}>
                <div style={{ fontSize: '2.25rem', fontWeight: 800, color: 'var(--text-primary)', fontFamily: 'var(--font-heading)' }}>
                  {perf.riskSkoru}
                </div>
                <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginBottom: '0.5rem' }}>
                  Ağırlıklı Risk Puanı
                </div>
                <RiskBadge level={perf.riskSeviyesi} score={perf.riskSkoru} />
              </div>

              <div>
                <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', fontWeight: 600, marginBottom: '0.4rem' }}>
                  Risk Gerekçeleri:
                </div>
                <ReasonCodes codes={perf.gerekceKodlari} max={6} />
              </div>
            </div>
          </div>

          {/* ─── BAYİYE BAĞLI DEPO DİNAMİK KRİTİK STOK ANALİZİ (<7 GÜN) ─── */}
          <div className="card-section" style={{ marginBottom: '1.5rem', overflow: 'hidden' }}>
            <div className="card-section-header" style={{ marginBottom: '0.85rem' }}>
              <div className="card-section-title">
                <h2>Bağlı Depo Dinamik Stok Eşiği Analizi ({currentDealerMeta.varsayilanDepoKodu})</h2>
                <span>
                  {dealerCriticalStocks.length > 0
                    ? `${dealerCriticalStocks.length} Ürün Kritik Eşik Altında`
                    : 'Stok Seviyeleri Yeterli'}
                </span>
              </div>
            </div>

            {dealerStockLoading ? (
              <p style={{ color: 'var(--text-muted)', fontSize: '0.85rem' }}>Dinamik stok verileri taranıyor...</p>
            ) : dealerCriticalStocks.length === 0 ? (
              <div
                style={{
                  background: 'var(--state-success-bg)',
                  padding: '0.95rem 1.25rem',
                  borderRadius: 'var(--radius-sm)',
                  border: '1px solid var(--state-success-border)',
                  color: 'var(--state-success-text)',
                  fontSize: '0.85rem',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '0.5rem',
                }}
              >
                <span>✓</span>
                <span>
                  <strong>{perf.bayiKodu}</strong> bayisinin bağlı olduğu <strong>{currentDealerMeta.varsayilanDepoKodu}</strong> deposunda 7 günden az kalan kritik stok bulunmamaktadır.
                </span>
              </div>
            ) : (
              <div>
                <div
                  style={{
                    marginBottom: '0.85rem',
                    padding: '0.65rem 0.95rem',
                    background: 'var(--state-danger-bg)',
                    border: '1px solid var(--state-danger-border)',
                    borderRadius: 'var(--radius-sm)',
                    fontSize: '0.8rem',
                    color: 'var(--state-danger-text)',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '0.5rem',
                  }}
                >
                  <span style={{ fontSize: '1.1rem' }}>⚠</span>
                  <span>
                    <strong>{perf.bayiKodu}</strong> bayisinin bağlı olduğu <strong>{currentDealerMeta.varsayilanDepoKodu}</strong> deposunda <strong>7 günden az kalan {dealerCriticalStocks.length} kritik ürün</strong> listelenmektedir:
                  </span>
                </div>

                <div className="table-responsive" style={{ maxHeight: '360px', overflow: 'auto' }}>
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
                      {dealerCriticalStocks.map((item, idx) => {
                        const matchedProduct = products.find((p) => String(p.urunKodu) === String(item.urunKodu))
                        const productName = item.urunAdi || matchedProduct?.urunAdi || item.urunKodu
                        const kalanGun = item.tahminiStokGunu ?? item.kalanStokGunu
                        const isVeryCritical = kalanGun !== null && kalanGun <= 3

                        return (
                          <tr key={`${item.urunKodu}-${item.depoKodu}-${idx}`}>
                            <td className="dealer-code-cell" style={{ padding: '0.6rem 0.85rem' }}>
                              <span style={{ fontWeight: 700, fontFamily: 'var(--font-mono)' }}>{item.urunKodu}</span> — {productName}
                            </td>
                            <td style={{ padding: '0.6rem 0.75rem' }}>
                              <span className="segment-badge" style={{ fontSize: '0.72rem' }}>{item.depoKodu}</span>
                            </td>
                            <td style={{ padding: '0.6rem 0.75rem', fontWeight: 700, color: 'var(--state-danger-accent)', fontFamily: 'var(--font-mono)' }}>
                              {Number(item.mevcutStok).toFixed(0)} Adet
                            </td>
                            <td style={{ padding: '0.6rem 0.75rem', fontWeight: 600, color: 'var(--text-secondary)', fontFamily: 'var(--font-mono)' }}>
                              {Number(item.son90GunSatis ?? item.sonUcAySatisAdedi ?? 0).toFixed(0)} Adet
                            </td>
                            <td style={{ padding: '0.6rem 0.75rem', color: 'var(--text-muted)' }}>
                              {item.ortalamaGunlukSatis ? `${Number(item.ortalamaGunlukSatis).toFixed(2)} / gün` : '-'}
                            </td>
                            <td style={{ padding: '0.6rem 0.75rem' }}>
                              <span
                                style={{
                                  fontWeight: 700,
                                  padding: '0.2rem 0.5rem',
                                  borderRadius: 'var(--radius-sm)',
                                  background: isVeryCritical ? 'var(--state-danger-bg)' : 'var(--state-warning-bg)',
                                  color: isVeryCritical ? 'var(--state-danger-text)' : 'var(--state-warning-text)',
                                  border: `1px solid ${isVeryCritical ? 'var(--state-danger-border)' : 'var(--state-warning-border)'}`,
                                  fontSize: '0.74rem',
                                  fontFamily: 'var(--font-mono)',
                                }}
                              >
                                {kalanGun != null ? `${Number(kalanGun).toFixed(1)} Gün` : '< 7 Gün'}
                              </span>
                            </td>
                            <td style={{ padding: '0.6rem 0.85rem' }}>
                              <span
                                style={{
                                  fontSize: '0.7rem',
                                  fontWeight: 700,
                                  padding: '0.15rem 0.45rem',
                                  borderRadius: 'var(--radius-sm)',
                                  background: 'var(--state-danger-bg)',
                                  color: 'var(--state-danger-text)',
                                  border: '1px solid var(--state-danger-border)',
                                }}
                              >
                                Kritik
                              </span>
                            </td>
                          </tr>
                        )
                      })}
                    </tbody>
                  </table>
                </div>
              </div>
            )}
          </div>
        </>
      )}

      {/* ─── GENEL ÜRÜN & DEPO DİNAMİK STOK KONTROLÜ ─── */}
      <div className="card-section" id="product-stock-section" style={{ marginTop: '1.5rem', overflow: 'hidden' }}>
        <div className="card-section-header">
          <div className="card-section-title">
            <h2>Ürün Kataloğu ve Depo Dinamik Stok Denetimi</h2>
            <span>Depo veya Ürün Seçimine Göre Son 90 Günlük Satış Hızı ve Yetersiz Stok Sorgulama</span>
          </div>
        </div>

        {/* Arama & Seçim Çubukları */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '1rem', marginBottom: '1.25rem' }}>
          <div className="form-group" style={{ margin: 0 }}>
            <label style={{ fontSize: '0.78rem', fontWeight: 600 }}>Ürün Kodu veya Adı ile Filtrele:</label>
            <SearchableSelect
              value={selectedProductCode}
              onChange={(val) => setSelectedProductCode(val)}
              options={productOptions}
              placeholder="Tüm Ürünler veya Arayın (Örn: U1001)"
            />
          </div>

          <div className="form-group" style={{ margin: 0 }}>
            <label style={{ fontSize: '0.78rem', fontWeight: 600 }}>Depo Kodu veya Adı ile Filtrele:</label>
            <SearchableSelect
              value={selectedWarehouseCode}
              onChange={(val) => setSelectedWarehouseCode(val)}
              options={warehouseOptions}
              placeholder="Tüm Depolar veya Arayın (Örn: DP01)"
            />
          </div>
        </div>

        {/* Durum Açıklama Bilgisi */}
        <div style={{
          padding: '0.65rem 1rem',
          background: 'var(--bg-input)',
          borderRadius: 'var(--radius-sm)',
          border: '1px solid var(--border-subtle)',
          fontSize: '0.8rem',
          color: 'var(--text-secondary)',
          marginBottom: '1rem',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '0.5rem',
        }}>
          <div>
            🔍 Filtre Durumu:{' '}
            <strong>
              {selectedWarehouseCode && selectedProductCode
                ? `${selectedWarehouseCode} deposundaki ${selectedProductCode} stok durumu`
                : selectedWarehouseCode
                ? `${selectedWarehouseCode} deposunda stoğu 7 günlük satış hızının altındaki TÜM ürünler`
                : selectedProductCode
                ? `${selectedProductCode} ürününün TÜM depolardaki stok durumu`
                : 'Şirket genelinde 7 günlük satış hızının altındaki tüm kritik ürünler'}
            </strong>
          </div>
          {(selectedProductCode || selectedWarehouseCode) && (
            <button
              type="button"
              className="action-btn-sm"
              style={{ fontSize: '0.72rem', padding: '0.2rem 0.55rem' }}
              onClick={() => {
                setSelectedProductCode('')
                setSelectedWarehouseCode('')
              }}
            >
              Filtreleri Temizle ✕
            </button>
          )}
        </div>

        {/* Stok Sonuç Tablosu */}
        {stockLoading ? (
          <p style={{ color: 'var(--text-muted)', padding: '1.5rem', fontSize: '0.85rem' }}>Dinamik stok verileri sorgulanıyor...</p>
        ) : productStockList.length > 0 ? (
          <div className="table-responsive" style={{ maxHeight: '420px', overflow: 'auto' }}>
            <table className="data-table" style={{ minWidth: '920px' }}>
              <thead>
                <tr>
                  <th style={{ width: '30%', minWidth: '200px', padding: '0.65rem 0.75rem', textAlign: 'left' }}>Ürün Kodu ve Adı</th>
                  <th style={{ width: '9%', minWidth: '80px', padding: '0.65rem 0.65rem', textAlign: 'left' }}>Depo</th>
                  <th style={{ width: '13%', minWidth: '110px', padding: '0.65rem 0.65rem', textAlign: 'left' }}>Mevcut Stok</th>
                  <th style={{ width: '15%', minWidth: '120px', padding: '0.65rem 0.65rem', textAlign: 'left' }}>Son 90 Gün Satış</th>
                  <th style={{ width: '15%', minWidth: '120px', padding: '0.65rem 0.65rem', textAlign: 'left' }}>Günlük Hız</th>
                  <th style={{ width: '10%', minWidth: '95px', padding: '0.65rem 0.65rem', textAlign: 'left' }}>Kalan Gün</th>
                  <th style={{ width: '10%', minWidth: '95px', padding: '0.65rem 0.75rem', textAlign: 'left' }}>Durum</th>
                </tr>
              </thead>
              <tbody>
                {productStockList.map((item, idx) => {
                  const matchedProduct = products.find((p) => String(p.urunKodu) === String(item.urunKodu))
                  const productName = item.urunAdi || matchedProduct?.urunAdi || item.urunKodu
                  const kalanGun = item.tahminiStokGunu ?? item.kalanStokGunu
                  const isCritical = item.stokKritikMi || (kalanGun !== null && kalanGun < 7)

                  return (
                    <tr key={`${item.urunKodu}-${item.depoKodu}-${idx}`}>
                      <td className="dealer-code-cell" style={{ padding: '0.6rem 0.85rem' }}>
                        <span style={{ fontWeight: 700, fontFamily: 'var(--font-mono)' }}>{item.urunKodu}</span> — {productName}
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
                        {item.ortalamaGunlukSatis ? Number(item.ortalamaGunlukSatis).toFixed(2) : '-'}
                      </td>
                      <td style={{ padding: '0.6rem 0.75rem' }}>
                        {kalanGun !== null && kalanGun !== undefined ? (
                          <span
                            style={{
                              fontWeight: 700,
                              padding: '0.2rem 0.5rem',
                              borderRadius: 'var(--radius-sm)',
                              background: isCritical ? 'var(--state-danger-bg)' : 'var(--state-success-bg)',
                              color: isCritical ? 'var(--state-danger-text)' : 'var(--state-success-text)',
                              border: `1px solid ${isCritical ? 'var(--state-danger-border)' : 'var(--state-success-border)'}`,
                              fontSize: '0.74rem',
                              fontFamily: 'var(--font-mono)',
                            }}
                          >
                            {Number(kalanGun).toFixed(1)} Gün
                          </span>
                        ) : '-'}
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
                })}
              </tbody>
            </table>
          </div>
        ) : (selectedProductCode || selectedWarehouseCode) ? (
          <div style={{ background: 'var(--state-success-bg)', padding: '0.95rem 1.25rem', borderRadius: 'var(--radius-sm)', border: '1px solid var(--state-success-border)', color: 'var(--state-success-text)', fontSize: '0.85rem' }}>
            ✓ Seçilen kriterlere göre 7 günden az kalan kritik stok bulunmamaktadır.
          </div>
        ) : (
          <div style={{ background: 'var(--bg-surface-elevated)', padding: '1.5rem', borderRadius: 'var(--radius-sm)', border: '1px dashed var(--border-subtle)', textAlign: 'center', color: 'var(--text-secondary)', fontSize: '0.85rem' }}>
            🔍 Stok durumunu sorgulamak için lütfen yukarıdan bir <strong>Depo</strong> veya <strong>Ürün</strong> seçiniz.
          </div>
        )}
      </div>
    </div>
  )
}
export default DealerAnalysis
