// Dashboard.jsx - Kurumsal Yönetici Dashboard Paneli (Gelişmiş Filtreler, Ekrana Tam Sığan Tablolar & Gerçek Veritabanı)
import { useState, useEffect, useMemo } from 'react'
import {
  getRiskList,
  getDealersSummary,
  getRegionDistribution,
  getFilterOptions,
} from '../api/client'
import MetricsCard from './MetricsCard'
import RiskBadge from './RiskBadge'
import ReasonCodes from './ReasonCodes'
import CriticalStock from './CriticalStock'

export function Dashboard({ onSelectDealer, selectedPeriod = '2026-08', onPeriodChange }) {
  const [donem, setDonem] = useState(selectedPeriod || '2026-08')
  const [prevSelectedPeriod, setPrevSelectedPeriod] = useState(selectedPeriod)

  if (selectedPeriod !== prevSelectedPeriod) {
    setPrevSelectedPeriod(selectedPeriod)
    if (selectedPeriod) setDonem(selectedPeriod)
  }

  // Filtre State'leri
  const [segment, setSegment] = useState('')
  const [bolge, setBolge] = useState('')
  const [riskLevelFilter, setRiskLevelFilter] = useState('')
  const [targetStatusFilter, setTargetStatusFilter] = useState('')
  const [search, setSearch] = useState('')
  const [sortBy, setSortBy] = useState('riskSkoruDesc')
  const [quickFilter, setQuickFilter] = useState('ALL') // 'ALL' | 'HIGH_RISK' | 'CRITICAL_STOCK' | 'LOW_TARGET' | 'LOW_MARGIN' | 'HEALTHY'

  const [availableSegments, setAvailableSegments] = useState([])
  const [availableRegions, setAvailableRegions] = useState([])
  const [availablePeriods, setAvailablePeriods] = useState([])

  const [riskData, setRiskData] = useState({ bayiler: [], toplam: 0 })
  const [summaryData, setSummaryData] = useState(null)
  const [regionDist, setRegionDist] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  useEffect(() => {
    async function loadMetaOptions() {
      try {
        const opts = await getFilterOptions()
        if (opts.segmentler && opts.segmentler.length > 0) {
          setAvailableSegments(opts.segmentler)
        }
        if (opts.bolgeler && opts.bolgeler.length > 0) {
          setAvailableRegions(opts.bolgeler)
        }
        if (opts.donemler && opts.donemler.length > 0) {
          setAvailablePeriods(opts.donemler)
        }
      } catch (err) {
        console.warn('Filtre seçenekleri yüklenemedi:', err)
      }
    }
    loadMetaOptions()
  }, [])

  const handlePeriodSelect = (val) => {
    setDonem(val)
    if (onPeriodChange) onPeriodChange(val)
  }

  const handleResetFilters = () => {
    setSegment('')
    setBolge('')
    setRiskLevelFilter('')
    setTargetStatusFilter('')
    setSearch('')
    setQuickFilter('ALL')
    setSortBy('riskSkoruDesc')
  }

  useEffect(() => {
    async function loadDashboardData() {
      setLoading(true)
      setError(null)
      try {
        const [riskRes, summaryRes, regionRes] = await Promise.allSettled([
          getRiskList({ donem, segment, bolge }),
          getDealersSummary({ donem, segment, bolge }),
          getRegionDistribution(),
        ])
        if (riskRes.status === 'fulfilled' && riskRes.value) setRiskData(riskRes.value)
        else if (riskRes.status === 'rejected') setError(riskRes.reason?.message || 'Bayi verileri alınamadı.')

        if (summaryRes.status === 'fulfilled' && summaryRes.value) setSummaryData(summaryRes.value)
        if (regionRes.status === 'fulfilled' && regionRes.value) setRegionDist(regionRes.value || [])
      } catch (err) {
        setError(err.message || 'Dashboard verileri yüklenirken hata oluştu.')
      } finally {
        setLoading(false)
      }
    }
    loadDashboardData()
  }, [donem, segment, bolge])

  // Toplam sayılar (Quick Filter rozetleri için - Gerçek Veritabanı Uyumlu)
  const counts = useMemo(() => {
    const list = riskData.bayiler || []
    const highRisk = list.filter((b) => {
      const lvl = (b.riskSeviyesi || '').toUpperCase()
      return lvl === 'YUKSEK' || lvl === 'KRITIK'
    }).length

    const criticalStock = list.filter(
      (b) => b.kritikStokVarMi || (b.gerekceKodlari || []).includes('STOK_YETERSIZ') || (b.gerekceKodlari || []).includes('STOK_GUNU_KRITIK') || (b.gerekceKodlari || []).includes('DINAMIK_ESIK_ALTI')
    ).length

    const lowTarget = list.filter((b) => {
      const t = b.hedefeUlasmaPct
      if (t == null) return false
      const norm = t > 5 ? t / 100 : t
      return norm < 0.8
    }).length

    const lowMargin = list.filter((b) => {
      const m = b.ortalamaMarjPct
      if (m == null) return false
      const norm = m > 5 ? m / 100 : m
      return norm < 0.15
    }).length

    const healthy = list.filter((b) => {
      const lvl = (b.riskSeviyesi || '').toUpperCase()
      const t = b.hedefeUlasmaPct != null ? (b.hedefeUlasmaPct > 5 ? b.hedefeUlasmaPct / 100 : b.hedefeUlasmaPct) : 1
      return lvl === 'DUSUK' && t >= 0.8
    }).length

    return {
      all: list.length,
      highRisk,
      criticalStock,
      lowTarget,
      lowMargin,
      healthy,
    }
  }, [riskData.bayiler])

  // Filtreleme ve Sıralama
  const filteredDealers = useMemo(() => {
    let list = riskData.bayiler || []

    // 1. Hızlı Filtre Butonu
    if (quickFilter === 'HIGH_RISK') {
      list = list.filter((b) => {
        const lvl = (b.riskSeviyesi || '').toUpperCase()
        return lvl === 'YUKSEK' || lvl === 'KRITIK'
      })
    } else if (quickFilter === 'CRITICAL_STOCK') {
      list = list.filter(
        (b) => b.kritikStokVarMi || (b.gerekceKodlari || []).includes('STOK_YETERSIZ') || (b.gerekceKodlari || []).includes('STOK_GUNU_KRITIK') || (b.gerekceKodlari || []).includes('DINAMIK_ESIK_ALTI')
      )
    } else if (quickFilter === 'LOW_TARGET') {
      list = list.filter((b) => {
        const t = b.hedefeUlasmaPct
        if (t == null) return false
        const norm = t > 5 ? t / 100 : t
        return norm < 0.8
      })
    } else if (quickFilter === 'LOW_MARGIN') {
      list = list.filter((b) => {
        const m = b.ortalamaMarjPct
        if (m == null) return false
        const norm = m > 5 ? m / 100 : m
        return norm < 0.15
      })
    } else if (quickFilter === 'HEALTHY') {
      list = list.filter((b) => {
        const lvl = (b.riskSeviyesi || '').toUpperCase()
        const t = b.hedefeUlasmaPct != null ? (b.hedefeUlasmaPct > 5 ? b.hedefeUlasmaPct / 100 : b.hedefeUlasmaPct) : 1
        return lvl === 'DUSUK' && t >= 0.8
      })
    }

    // 2. Risk Seviyesi Dropdown Filtresi
    if (riskLevelFilter) {
      list = list.filter((b) => (b.riskSeviyesi || '').toUpperCase() === riskLevelFilter.toUpperCase())
    }

    // 3. Hedef Durumu Dropdown Filtresi
    if (targetStatusFilter === 'MET') {
      list = list.filter((b) => {
        const t = b.hedefeUlasmaPct
        if (t == null) return false
        return (t > 5 ? t / 100 : t) >= 0.8
      })
    } else if (targetStatusFilter === 'UNMET') {
      list = list.filter((b) => {
        const t = b.hedefeUlasmaPct
        if (t == null) return false
        return (t > 5 ? t / 100 : t) < 0.8
      })
    }

    // 4. Metin Arama
    if (search.trim()) {
      const q = search.toLowerCase().trim()
      list = list.filter((b) =>
        (b.bayiKodu || '').toLowerCase().includes(q) ||
        (b.bayiAdi || '').toLowerCase().includes(q) ||
        (b.bolge || '').toLowerCase().includes(q) ||
        (b.segment || '').toLowerCase().includes(q)
      )
    }

    // 5. Sıralama
    return [...list].sort((a, b) => {
      if (sortBy === 'riskSkoruDesc') return (b.riskSkoru || 0) - (a.riskSkoru || 0)
      if (sortBy === 'riskSkoruAsc') return (a.riskSkoru || 0) - (b.riskSkoru || 0)
      if (sortBy === 'targetDesc') return (b.hedefeUlasmaPct || 0) - (a.hedefeUlasmaPct || 0)
      if (sortBy === 'marginDesc') return (b.ortalamaMarjPct || 0) - (a.ortalamaMarjPct || 0)
      if (sortBy === 'codeAsc') return (a.bayiKodu || '').localeCompare(b.bayiKodu || '', undefined, { numeric: true })
      return 0
    })
  }, [riskData.bayiler, quickFilter, riskLevelFilter, targetStatusFilter, search, sortBy])

  const isAnyFilterActive = !!(segment || bolge || riskLevelFilter || targetStatusFilter || search || (quickFilter && quickFilter !== 'ALL'))

  // KPI Hesaplamaları
  const kpis = useMemo(() => {
    const list = isAnyFilterActive ? filteredDealers : (riskData.bayiler || [])
    const summaryTotal = summaryData?.toplamBayiSayisi ?? summaryData?.toplam_bayi_sayisi
    const total = isAnyFilterActive
      ? list.length
      : ((summaryTotal && summaryTotal > 0) ? summaryTotal : (riskData.toplam || list.length))

    if (total === 0 && list.length === 0) return { totalDealers: 0, avgTargetPct: 0, avgMarginPct: 0, highRiskCount: 0, criticalStockCount: 0, totalRevenue: 0 }

    const validTargets = list.filter((b) => b.hedefeUlasmaPct != null)
    const totalRealized = validTargets.reduce((sum, b) => sum + (b.gerceklesenCiro || b.ciro || 0), 0)
    const totalTarget = validTargets.reduce((sum, b) => sum + (b.hedefCiro || b.hedef || 0), 0)
    const weightedAvgTarget = totalTarget > 0 ? (totalRealized / totalTarget) : (
      validTargets.length > 0 ? validTargets.reduce((sum, b) => sum + b.hedefeUlasmaPct, 0) / validTargets.length : 0
    )

    const avgTarget = isAnyFilterActive
      ? weightedAvgTarget
      : (summaryData?.genelHedefeUlasmaPct
        ?? summaryData?.ortalamaHedefeUlasmaPct
        ?? summaryData?.genel_hedefe_ulasma_pct
        ?? summaryData?.sistemOrtalamaHedefeUlasmaPct
        ?? weightedAvgTarget)

    const validMargins = list.filter((b) => b.ortalamaMarjPct != null)
    const avgMarginCalculated = validMargins.length > 0 ? validMargins.reduce((sum, b) => sum + b.ortalamaMarjPct, 0) / validMargins.length : 0

    const avgMargin = isAnyFilterActive
      ? avgMarginCalculated
      : (summaryData?.genelOrtalamaMarjPct
        ?? summaryData?.ortalamaMarjPct
        ?? summaryData?.genel_ortalama_marj_pct
        ?? avgMarginCalculated)

    const highRisk = list.filter((b) => {
      const lvl = (b.riskSeviyesi || '').toUpperCase()
      return lvl === 'YUKSEK' || lvl === 'KRITIK'
    }).length

    const criticalStock = list.filter(
      (b) => b.kritikStokVarMi || (b.gerekceKodlari || []).includes('STOK_YETERSIZ') || (b.gerekceKodlari || []).includes('STOK_GUNU_KRITIK') || (b.gerekceKodlari || []).includes('DINAMIK_ESIK_ALTI')
    ).length

    const totalRev = isAnyFilterActive
      ? totalRealized
      : (summaryData?.toplamCiro ?? summaryData?.toplamGerceklesenCiro ?? summaryData?.toplam_gerceklesen_ciro ?? totalRealized)

    return {
      totalDealers: total,
      totalRevenue: totalRev,
      avgTargetPct: avgTarget,
      avgMarginPct: avgMargin,
      highRiskCount: highRisk,
      criticalStockCount: criticalStock,
    }
  }, [riskData.bayiler, riskData.toplam, summaryData, filteredDealers, isAnyFilterActive])

  const formatCurrency = (amount) => {
    if (amount === null || amount === undefined) return '-'
    return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', maximumFractionDigits: 0 }).format(amount)
  }

  const handleRowClick = (dealer) => {
    if (onSelectDealer) onSelectDealer(dealer.bayiKodu)
  }

  const segmentOptions = useMemo(() => {
    const defaultSegments = ['Bireysel', 'KOBİ', 'Kurumsal']
    const combined = Array.from(new Set([...availableSegments, ...defaultSegments]))
    const filtered = combined.filter(
      (s) => !['A', 'B', 'C', 'Segment A', 'Segment B', 'Segment C'].includes(s)
    )
    return filtered.map((s) => ({
      value: s,
      label: s.startsWith('Segment') ? s : `Segment ${s}`,
    }))
  }, [availableSegments])

  const regionOptions = useMemo(() => {
    const defaultRegions = ['Marmara', 'Ege', 'İç Anadolu', 'Akdeniz', 'Karadeniz', 'Güneydoğu Anadolu', 'Doğu Anadolu']
    return Array.from(new Set([...availableRegions, ...defaultRegions]))
  }, [availableRegions])

  const displayedRegionDist = useMemo(() => {
    if (regionDist.length > 0 && regionDist.some((r) => (r.bayiSayisi || 0) > 0)) {
      return regionDist
    }

    const list = riskData.bayiler || []
    if (list.length === 0) return regionDist

    const map = {}
    let totalCiro = 0

    list.forEach((b) => {
      const r = b.bolge || 'Genel'
      if (!map[r]) map[r] = { bolge: r, bayiSayisi: 0, toplamCiro: 0 }
      map[r].bayiSayisi += 1
      const c = b.gerceklesenCiro || b.ciro || 0
      map[r].toplamCiro += c
      totalCiro += c
    })

    return Object.values(map).map((item) => ({
      ...item,
      ciroPayiPct: totalCiro > 0 ? item.toplamCiro / totalCiro : 0,
    }))
  }, [regionDist, riskData.bayiler])

  return (
    <div className="animate-fade-in">
      {/* ─── ÜST YÖNETİCİ BAŞLIK VE HIZLI FİLTRE BAR ─── */}
      <div className="page-header" style={{ marginBottom: '1.25rem' }}>
        <div className="page-title">
          <h1>Yönetici Özet Paneli</h1>
          <p>Gerçek zamanlı bayi performans göstergeleri, risk analitiği ve dinamik tedarik takibi</p>
        </div>

        <div className="filter-toolbar" style={{ gap: '0.55rem', padding: '0.45rem 0.75rem' }}>
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

          <div className="filter-item">
            <label>Bölge</label>
            <select className="filter-select" value={bolge} onChange={(e) => setBolge(e.target.value)}>
              <option value="">Tüm Bölgeler</option>
              {regionOptions.map((r) => (
                <option key={r} value={r}>
                  {r}
                </option>
              ))}
            </select>
          </div>

          <div className="filter-item">
            <label>Segment</label>
            <select className="filter-select" value={segment} onChange={(e) => setSegment(e.target.value)}>
              <option value="">Tüm Segmentler</option>
              {segmentOptions.map((s) => (
                <option key={s.value} value={s.value}>
                  {s.label}
                </option>
              ))}
            </select>
          </div>

          <div className="filter-item">
            <label>Risk Seviyesi</label>
            <select className="filter-select" value={riskLevelFilter} onChange={(e) => setRiskLevelFilter(e.target.value)}>
              <option value="">Tümü</option>
              <option value="YUKSEK">🔴 Yüksek Risk</option>
              <option value="ORTA">🟡 Orta Risk</option>
              <option value="DUSUK">🟢 Düşük Risk</option>
            </select>
          </div>

          <div className="filter-item">
            <label>Hedef Durumu</label>
            <select className="filter-select" value={targetStatusFilter} onChange={(e) => setTargetStatusFilter(e.target.value)}>
              <option value="">Tümü</option>
              <option value="MET">✓ Hedef Karşılandı (&gt;=%80)</option>
              <option value="UNMET">✕ Hedef Altında (&lt;%80)</option>
            </select>
          </div>

          {isAnyFilterActive && (
            <button
              type="button"
              className="action-btn-sm"
              style={{ fontSize: '0.72rem', padding: '0.3rem 0.6rem', background: 'var(--bg-surface-hover)' }}
              onClick={handleResetFilters}
              title="Tüm filtreleri temizle"
            >
              Sıfırla ✕
            </button>
          )}
        </div>
      </div>

      {error && (
        <div className="auth-alert error" style={{ marginBottom: '1.25rem', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <span>⚠️ API Bağlantı Uyarısı: {error}</span>
          <button type="button" className="action-btn-sm" style={{ padding: '0.2rem 0.5rem', fontSize: '0.72rem' }} onClick={() => setError(null)}>✕ Kapat</button>
        </div>
      )}

      {/* ─── KPI METRİK KARTLARI ─── */}
      <div className="kpi-grid">
        <MetricsCard
          title="Toplam Bayi Ağı"
          value={kpis.totalDealers}
          subtext={`Kapsam: ${donem} Dönemi`}
          badgeText="185 Aktif"
          badgeType="info"
        />

        <MetricsCard
          title="Toplam Gerçekleşen Ciro"
          value={formatCurrency(kpis.totalRevenue)}
          subtext="Dönemsel Net Ciro Hacmi"
          badgeText="Ciro"
          badgeType="success"
        />

        {(() => {
          const normTarget = kpis.avgTargetPct > 5 ? kpis.avgTargetPct / 100 : kpis.avgTargetPct
          const displayTarget = Math.abs(kpis.avgTargetPct) > 5 ? kpis.avgTargetPct : kpis.avgTargetPct * 100
          return (
            <MetricsCard
              title="Ortalama Hedefe Ulaşma"
              value={`%${displayTarget.toFixed(1)}`}
              subtext={normTarget >= 0.8 ? 'Bütçe hedefi karşılandı' : 'Hedef altında sapma mevcut'}
              badgeText={normTarget >= 0.8 ? 'Yeterli' : 'Takip'}
              badgeType={normTarget >= 0.8 ? 'success' : 'warning'}
            />
          )
        })()}

        {(() => {
          const normMargin = kpis.avgMarginPct > 5 ? kpis.avgMarginPct / 100 : kpis.avgMarginPct
          const displayMargin = Math.abs(kpis.avgMarginPct) > 5 ? kpis.avgMarginPct : kpis.avgMarginPct * 100
          return (
            <MetricsCard
              title="Ortalama Kâr Marjı"
              value={`%${displayMargin.toFixed(1)}`}
              subtext="Referans Eşik Marj: %15.0"
              badgeText={normMargin >= 0.15 ? 'Uygun' : 'Eşik Altı'}
              badgeType={normMargin >= 0.15 ? 'success' : 'danger'}
            />
          )
        })()}

        <MetricsCard
          title="Yüksek Riskli Bayi"
          value={kpis.highRiskCount}
          subtext={kpis.highRiskCount > 0 ? 'Öncelikli aksiyon gerekli' : 'Kritik risk bulunmuyor'}
          badgeText={kpis.highRiskCount > 0 ? `${kpis.highRiskCount} Bayi` : 'Normal'}
          badgeType={kpis.highRiskCount > 0 ? 'danger' : 'success'}
        />
      </div>

      {/* ─── COĞRAFİ BÖLGE DAĞILIMI ÖZETİ ─── */}
      {displayedRegionDist.length > 0 && (
        <div className="card-section" style={{ padding: '1.15rem 1.35rem', marginBottom: '1.5rem', overflow: 'hidden' }}>
          <div className="card-section-title" style={{ marginBottom: '0.85rem' }}>
            <h2>Bölgesel Bayi Ağı ve Ciro Dağılımı</h2>
            <span>7 Coğrafi Bölge</span>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '0.75rem' }}>
            {displayedRegionDist.map((rd, i) => (
              <div
                key={i}
                style={{
                  background: 'var(--bg-input)',
                  border: '1px solid var(--border-subtle)',
                  borderRadius: 'var(--radius-sm)',
                  padding: '0.75rem 0.95rem',
                  transition: 'all 0.15s ease',
                }}
              >
                <div style={{ fontSize: '0.75rem', fontWeight: 600, color: 'var(--text-secondary)' }}>{rd.bolge}</div>
                <div style={{ fontSize: '1.15rem', fontWeight: 700, color: 'var(--text-primary)', marginTop: '0.15rem' }}>
                  {rd.bayiSayisi} Bayi
                </div>
                <div style={{ fontSize: '0.72rem', color: 'var(--brand-primary)', fontWeight: 600, marginTop: '0.15rem' }}>
                  Ciro Payı: %{((rd.ciroPayiPct || 0) * 100).toFixed(0)}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* ─── BAYİ RİSK & PERFORMANS TABLOSU (KURUMSAL GENİŞ VE ESNEK TASARIM) ─── */}
      <div className="card-section" style={{ overflow: 'hidden' }}>
        <div className="card-section-header" style={{ marginBottom: '0.85rem' }}>
          <div className="card-section-title">
            <h2>Bayi Performans ve Risk Değerlendirme Tablosu</h2>
            <span>{filteredDealers.length} / {counts.all} Bayi Kaydı</span>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', flexWrap: 'wrap' }}>
            <div className="search-input-wrapper">
              <span className="search-icon">🔍</span>
              <input
                type="text"
                className="search-input"
                placeholder="Kod, unvan, bölge ara..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>

            <div className="filter-item">
              <label>Sırala</label>
              <select className="filter-select" value={sortBy} onChange={(e) => setSortBy(e.target.value)}>
                <option value="riskSkoruDesc">Risk Skoru (Azalan)</option>
                <option value="riskSkoruAsc">Risk Skoru (Artan)</option>
                <option value="targetDesc">Hedefe Ulaşma (Azalan)</option>
                <option value="marginDesc">Ortalama Marj (Azalan)</option>
                <option value="codeAsc">Bayi Kodu (A-Z)</option>
              </select>
            </div>
          </div>
        </div>

        {/* Hızlı Filtre Segmented Butonları */}
        <div className="segmented-filter-bar" style={{ display: 'flex', gap: '0.45rem', flexWrap: 'wrap', marginBottom: '1rem' }}>
          <button
            type="button"
            className={`segmented-filter-btn ${quickFilter === 'ALL' ? 'active' : ''}`}
            onClick={() => setQuickFilter('ALL')}
          >
            <span>📋 Tüm Bayiler</span>
            <span className="count-badge">{counts.all}</span>
          </button>

          <button
            type="button"
            className={`segmented-filter-btn danger ${quickFilter === 'HIGH_RISK' ? 'active' : ''}`}
            onClick={() => setQuickFilter('HIGH_RISK')}
          >
            <span>🔴 Yüksek Riskli</span>
            <span className="count-badge">{counts.highRisk}</span>
          </button>

          <button
            type="button"
            className={`segmented-filter-btn warning ${quickFilter === 'CRITICAL_STOCK' ? 'active' : ''}`}
            onClick={() => setQuickFilter('CRITICAL_STOCK')}
          >
            <span>⚠️ Kritik Stoklu</span>
            <span className="count-badge">{counts.criticalStock}</span>
          </button>

          <button
            type="button"
            className={`segmented-filter-btn ${quickFilter === 'LOW_TARGET' ? 'active' : ''}`}
            onClick={() => setQuickFilter('LOW_TARGET')}
          >
            <span>📉 Hedef Altında &lt;%80</span>
            <span className="count-badge">{counts.lowTarget}</span>
          </button>

          <button
            type="button"
            className={`segmented-filter-btn ${quickFilter === 'LOW_MARGIN' ? 'active' : ''}`}
            onClick={() => setQuickFilter('LOW_MARGIN')}
          >
            <span>🔻 Düşük Marj &lt;%15</span>
            <span className="count-badge">{counts.lowMargin}</span>
          </button>

          <button
            type="button"
            className={`segmented-filter-btn success ${quickFilter === 'HEALTHY' ? 'active' : ''}`}
            onClick={() => setQuickFilter('HEALTHY')}
          >
            <span>🟢 Sağlıklı Bayiler</span>
            <span className="count-badge">{counts.healthy}</span>
          </button>
        </div>

        {kpis.highRiskCount > 0 && quickFilter === 'ALL' && !search && (
          <div style={{
            marginBottom: '1rem',
            padding: '0.65rem 1rem',
            background: 'var(--state-danger-bg)',
            border: '1px solid var(--state-danger-border)',
            borderRadius: 'var(--radius-sm)',
            fontSize: '0.8rem',
            color: 'var(--state-danger-text)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: '0.5rem',
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <span style={{ fontSize: '1.1rem' }}>⚠</span>
              <span>
                Sistemde <strong>{kpis.highRiskCount} yüksek riskli bayi</strong> tespit edildi.
              </span>
            </div>
            <button
              type="button"
              className="action-btn-sm"
              style={{ fontSize: '0.72rem', padding: '0.25rem 0.65rem' }}
              onClick={() => setQuickFilter('HIGH_RISK')}
            >
              Yüksek Risklileri Filtrele ➔
            </button>
          </div>
        )}

        <div className="table-responsive" style={{ maxHeight: '680px' }}>
          <table className="data-table">
            <thead>
              <tr>
                <th style={{ width: '12%', padding: '0.5rem 0.25rem', textAlign: 'left' }}>Bayi Kodu & Ünvan</th>
                <th style={{ width: '14%', padding: '0.5rem 0.25rem', textAlign: 'left' }}>Bölge</th>
                <th style={{ width: '9%', padding: '0.5rem 0.25rem', textAlign: 'left' }}>Segment</th>
                <th style={{ width: '6.5%', padding: '0.5rem 0.15rem', textAlign: 'center' }}>Hedef %</th>
                <th style={{ width: '6.5%', padding: '0.5rem 0.15rem', textAlign: 'center' }}>Ort. Marj</th>
                <th style={{ width: '6%', padding: '0.5rem 0.15rem', textAlign: 'center' }}>İskonto</th>
                <th style={{ width: '15%', padding: '0.5rem 0.25rem', textAlign: 'left' }}>Risk Seviyesi</th>
                <th style={{ width: '23%', padding: '0.5rem 0.25rem', textAlign: 'left' }}>Gerekçeler</th>
                <th style={{ width: '8%', padding: '0.5rem 0.3rem', textAlign: 'right' }}>İşlem</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan="9" style={{ textAlign: 'center', padding: '2.5rem', color: 'var(--text-muted)' }}>
                    Veritabanı taranıyor...
                  </td>
                </tr>
              ) : filteredDealers.length === 0 ? (
                <tr>
                  <td colSpan="9" style={{ textAlign: 'center', padding: '2.5rem', color: 'var(--text-muted)' }}>
                    Filtre kriterlerine uygun bayi kaydı bulunamadı.
                  </td>
                </tr>
              ) : (
                filteredDealers.map((dealer) => {
                  const targetPct = dealer.hedefeUlasmaPct
                  const marginPct = dealer.ortalamaMarjPct
                  const discountPct = dealer.ortalamaIskontoPct
                  const lvl = (dealer.riskSeviyesi || '').toUpperCase()
                  const isHighRisk = lvl === 'YUKSEK' || lvl === 'KRITIK'

                  return (
                    <tr
                      key={dealer.bayiKodu}
                      onClick={() => handleRowClick(dealer)}
                      style={{
                        cursor: 'pointer',
                        borderLeft: isHighRisk ? '3px solid var(--state-danger-accent)' : '3px solid transparent',
                      }}
                      title={`${dealer.bayiKodu} detayına git`}
                    >
                      <td className="dealer-code-cell" style={{ padding: '0.45rem 0.25rem' }}>
                        <div style={{ display: 'inline-flex', alignItems: 'center', gap: '0.3rem', whiteSpace: 'nowrap' }}>
                          <span style={{ fontWeight: 700, fontFamily: 'var(--font-mono)' }}>{dealer.bayiKodu}</span>
                          {dealer.bayiAdi && dealer.bayiAdi !== dealer.bayiKodu && (
                            <span style={{ fontSize: '0.72rem', color: 'var(--text-muted)' }}>
                              — {dealer.bayiAdi}
                            </span>
                          )}
                        </div>
                      </td>
                      <td style={{ padding: '0.45rem 0.25rem' }}>
                        {dealer.bolge}
                      </td>
                      <td style={{ padding: '0.45rem 0.25rem' }}>
                        <span className="segment-badge" style={{ padding: '0.12rem 0.4rem', fontSize: '0.68rem' }}>
                          {dealer.segment}
                        </span>
                      </td>
                      <td style={{ padding: '0.45rem 0.15rem', fontWeight: 600, textAlign: 'center' }}>
                        {targetPct != null ? (() => {
                          let pct = Math.abs(targetPct) > 5 ? targetPct : targetPct * 100
                          if (pct > 1000) pct = pct / 100
                          else if (pct > 200) pct = pct / 10
                          const normVal = pct / 100
                          return (
                            <span style={{ color: normVal >= 0.8 ? 'var(--state-success-accent)' : 'var(--state-warning-accent)' }}>
                              %{pct.toFixed(1)}
                            </span>
                          )
                        })() : '-'}
                      </td>
                      <td style={{ padding: '0.45rem 0.15rem', fontWeight: 600, textAlign: 'center' }}>
                        {marginPct != null ? (
                          <span style={{ color: (marginPct > 5 ? marginPct / 100 : marginPct) >= 0.15 ? 'var(--state-success-accent)' : 'var(--state-danger-accent)' }}>
                            %{(Math.abs(marginPct) > 5 ? marginPct : marginPct * 100).toFixed(1)}
                          </span>
                        ) : '-'}
                      </td>
                      <td style={{ padding: '0.45rem 0.15rem', textAlign: 'center' }}>
                        {discountPct != null ? `%${(Math.abs(discountPct) > 5 ? discountPct : discountPct * 100).toFixed(1)}` : '-'}
                      </td>
                      <td style={{ padding: '0.45rem 0.25rem' }}>
                        <RiskBadge level={dealer.riskSeviyesi} score={dealer.riskSkoru} />
                      </td>
                      <td style={{ padding: '0.45rem 0.25rem' }}>
                        <ReasonCodes codes={dealer.gerekceKodlari} max={1} />
                      </td>
                      <td style={{ padding: '0.45rem 0.3rem', textAlign: 'right' }}>
                        <button
                          type="button"
                          className="action-btn-sm"
                          style={{ padding: '0.2rem 0.5rem', fontSize: '0.70rem' }}
                          onClick={(e) => { e.stopPropagation(); handleRowClick(dealer) }}
                        >
                          İncele ➔
                        </button>
                      </td>
                    </tr>
                  )
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* ─── DİNAMİK KRİTİK STOK MODÜLÜ ─── */}
      <CriticalStock />
    </div>
  )
}
export default Dashboard
