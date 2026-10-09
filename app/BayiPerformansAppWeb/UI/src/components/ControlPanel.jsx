// ControlPanel.jsx - API Denetim, Admin CRUD & Sistem Kontrol Paneli
import { useState, useMemo } from 'react'
import {
  getToken,
  isDemoMode,
  getApiUrl,
  getApiBaseUrl,
  createDealer,
  createProduct,
  upsertStock,
} from '../api/client'

const ENDPOINT_DEFINITIONS = [
  // ─── KİMLİK & DOĞRULAMA (AUTH & OTP) ───
  {
    id: 'auth_me',
    category: 'Kimlik (Auth & OTP)',
    method: 'GET',
    path: '/auth/me',
    description: 'Geçerli JWT token ile giriş yapmış kullanıcının profil detaylarını döndürür.',
    authRequired: true,
    defaultParams: {},
  },
  {
    id: 'auth_login',
    category: 'Kimlik (Auth & OTP)',
    method: 'POST',
    path: '/auth/login',
    description: 'E-posta ve şifre ile Supabase üzerinden access & refresh JWT token üretir.',
    authRequired: false,
    defaultBody: { email: 'yonetici@bayiperformans.com', sifre: 'Demo123!' },
  },
  {
    id: 'otp_send',
    category: 'Kimlik (Auth & OTP)',
    method: 'POST',
    path: '/auth/otp/gonder',
    description: '2FA Redis OTP doğrulama kodu üretir ve e-posta olarak gönderir.',
    authRequired: true,
    defaultBody: { email: 'yonetici@bayiperformans.com', islem_tipi: 'LOGIN_2FA' },
  },
  {
    id: 'otp_verify',
    category: 'Kimlik (Auth & OTP)',
    method: 'POST',
    path: '/auth/otp/dogrula',
    description: 'Gönderilen OTP doğrulama kodunu kontrol eder.',
    authRequired: true,
    defaultBody: { islem_id: 'OTP-1001', kod: '123456' },
  },

  // ─── ADMIN & CRUD EKLEME / GÜNCELLEME ───
  {
    id: 'create_dealer',
    category: 'Admin / CRUD Yönetimi',
    method: 'POST',
    path: '/bayiler',
    description: 'Sisteme yeni bayi ekler.',
    authRequired: true,
    defaultBody: { bayiKodu: 'B026', unvan: 'Yeni Doğu Bilişim A.Ş.', segment: 'B', bolge: 'Doğu Anadolu' },
  },
  {
    id: 'update_dealer',
    category: 'Admin / CRUD Yönetimi',
    method: 'PUT',
    path: '/bayiler/{kod}',
    description: 'Mevcut bayi bilgilerini günceller.',
    authRequired: true,
    defaultBody: { unvan: 'Güncellenmiş Bilişim A.Ş.', segment: 'A', bolge: 'Marmara' },
  },
  {
    id: 'create_product',
    category: 'Admin / CRUD Yönetimi',
    method: 'POST',
    path: '/urunler',
    description: 'Yeni ürün kataloğu ve birim maliyeti ekler.',
    authRequired: true,
    defaultBody: { urunKodu: 'U1500', urunAdi: 'AI Hızlandırıcı Kart GPU', kategori: 'Sunucu', birimMaliyet: 145000.0 },
  },
  {
    id: 'upsert_stock',
    category: 'Admin / CRUD Yönetimi',
    method: 'POST',
    path: '/stok',
    description: 'Ürün ve depo stok seviyelerini ekler veya günceller.',
    authRequired: true,
    defaultBody: { urunKodu: 'U1001', depoKodu: 'DP01', mevcutStok: 50 },
  },

  // ─── BAYİ & PERFORMANS & RİSK ───
  {
    id: 'dealer_perf',
    category: 'Bayi & Risk',
    method: 'GET',
    path: '/bayi/{kod}/performans',
    description: 'Belirtilen bayinin dönemlik gerçekleşen ciro, hedef, marj ve risk skoru analizini üretir.',
    authRequired: true,
    defaultParams: { donem: '2026-08' },
  },
  {
    id: 'risk_list',
    category: 'Bayi & Risk',
    method: 'GET',
    path: '/bayiler/risk-listesi',
    description: 'Dönem, segment ve bölgeye göre filtrelenmiş bayi risk listesini getirir.',
    authRequired: true,
    defaultParams: { donem: '2026-08', segment: '', bolge: '' },
    optionalParams: ['segment', 'bolge'],
  },
  {
    id: 'dealers_summary',
    category: 'Bayi & Risk',
    method: 'GET',
    path: '/bayiler/ozet',
    description: 'Tüm bayi ağının toplam ciro, ortalama marj ve risk özet istatistiklerini getirir.',
    authRequired: true,
    defaultParams: {},
  },
  {
    id: 'dealers_city_dist',
    category: 'Bayi & Risk',
    method: 'GET',
    path: '/bayiler/sehir-dagilimi',
    description: 'Bayilerin Türkiye illerine göre coğrafi dağılımı ve sayılarını döndürür.',
    authRequired: true,
    defaultParams: {},
  },
  {
    id: 'dealers_region_dist',
    category: 'Bayi & Risk',
    method: 'GET',
    path: '/bayiler/bolge-dagilimi',
    description: 'Bölgelere göre bayi sayıları ve ciro paylarını getirir.',
    authRequired: true,
    defaultParams: {},
  },

  // ─── STOK & TEDARİK ───
  {
    id: 'critical_stock',
    category: 'Stok & Tedarik',
    method: 'GET',
    path: '/stok/kritik',
    description: 'Kritik stok seviyesine inen ürünleri, satış hızını ve stok gününü getirir.',
    authRequired: true,
    defaultParams: { depo_kodu: '', urun_kodu: '' },
  },
  {
    id: 'stock_matrix',
    category: 'Stok & Tedarik',
    method: 'GET',
    path: '/stok/urun-dagilimi',
    description: 'Ürün ve depo bazlı stok dağılım matrisini getirir.',
    authRequired: true,
    defaultParams: {},
  },

  // ─── TEKLİF & KARAR ENGINE ───
  {
    id: 'offers_list',
    category: 'Teklif & Karar Engine',
    method: 'GET',
    path: '/teklifler',
    description: 'Geçmiş değerlendirilen tekliflerin listesini getirir.',
    authRequired: true,
    defaultParams: {},
  },
  {
    id: 'offer_eval_draft',
    category: 'Teklif & Karar Engine',
    method: 'POST',
    path: '/teklifler/{kod}/degerlendir',
    description: 'Belirtilen taslak teklifi kural motoruyla değerlendirir.',
    authRequired: true,
    defaultBody: {},
  },

  // ─── ARKA PLAN İŞLERİ (JOBS) ───
  {
    id: 'jobs_status',
    category: 'Arka Plan İşleri (Jobs)',
    method: 'GET',
    path: '/jobs/durum',
    description: 'Skor hesaplama zamanlanmış görevinin güncel durumunu getirir.',
    authRequired: true,
    defaultParams: {},
  },
]

export function ControlPanel() {
  const [activeTab, setActiveTab] = useState('explorer') // explorer | crud | jobs
  const [selectedEndpoint, setSelectedEndpoint] = useState(ENDPOINT_DEFINITIONS[0])
  const [queryParams, setQueryParams] = useState(selectedEndpoint.defaultParams || {})
  const [requestBody, setRequestBody] = useState(
    selectedEndpoint.defaultBody ? JSON.stringify(selectedEndpoint.defaultBody, null, 2) : ''
  )
  const [pathKod, setPathKod] = useState('B001')

  const [responseStatus, setResponseStatus] = useState(null)
  const [responseLatency, setResponseLatency] = useState(null)
  const [responseData, setResponseData] = useState(null)
  const [isExecuting, setIsExecuting] = useState(false)
  const [endpointHealth, setEndpointHealth] = useState({})

  // Quick CRUD State
  const [crudType, setCrudType] = useState('dealer')
  const [crudNotice, setCrudNotice] = useState(null)

  // Dealer CRUD Form
  const [cBayiKodu, setCBayiKodu] = useState('')
  const [cUnvan, setCUnvan] = useState('')
  const [cSegment, setCSegment] = useState('A')
  const [cBolge, setCBolge] = useState('Marmara')

  // Product CRUD Form
  const [cUrunKodu, setCUrunKodu] = useState('')
  const [cUrunAdi, setCUrunAdi] = useState('')
  const [cKategori, setCKategori] = useState('Sunucu')
  const [cBirimMaliyet, setCBirimMaliyet] = useState('')

  // Stock CRUD Form
  const [sUrunKodu, setSUrunKodu] = useState('')
  const [sDepoKodu, setSDepoKodu] = useState('DP01')
  const [sMevcutStok, setSMevcutStok] = useState('')

  const token = getToken()
  const demo = isDemoMode()

  const handleSelectEndpoint = (ep) => {
    setSelectedEndpoint(ep)
    setQueryParams(ep.defaultParams || {})
    setRequestBody(ep.defaultBody ? JSON.stringify(ep.defaultBody, null, 2) : '')
    setResponseStatus(null)
    setResponseLatency(null)
    setResponseData(null)
  }

  const handleExecuteRequest = async () => {
    setIsExecuting(true)
    setResponseData(null)
    const startTime = performance.now()

    let url = selectedEndpoint.path
    if (url.includes('{kod}')) {
      url = url.replace('{kod}', encodeURIComponent(pathKod || 'B001'))
    }

    const searchParams = new URLSearchParams()
    Object.entries(queryParams).forEach(([k, v]) => {
      if (v !== '' && v !== undefined) searchParams.append(k, v)
    })
    const queryString = searchParams.toString() ? `?${searchParams.toString()}` : ''

    const headers = {
      'Content-Type': 'application/json',
      ...(token && selectedEndpoint.authRequired ? { Authorization: `Bearer ${token}` } : {}),
    }

    const options = {
      method: selectedEndpoint.method,
      headers,
    }

    if ((selectedEndpoint.method === 'POST' || selectedEndpoint.method === 'PUT') && requestBody) {
      try {
        options.body = JSON.stringify(JSON.parse(requestBody))
      } catch (err) {
        setResponseStatus(400)
        setResponseLatency(0)
        setResponseData({ error: 'Geçersiz JSON Request Body biçimi.', detail: err.message })
        setIsExecuting(false)
        return
      }
    }

    try {
      const fullUrl = getApiUrl(url + queryString)
      const res = await fetch(fullUrl, options)
      const duration = Math.round(performance.now() - startTime)
      setResponseLatency(duration)
      setResponseStatus(res.status)

      const json = await res.json().catch(() => null)
      setResponseData(json || { statusText: res.statusText, status: res.status })

      setEndpointHealth((prev) => ({
        ...prev,
        [selectedEndpoint.id]: { status: res.status, latency: duration },
      }))
    } catch (err) {
      const duration = Math.round(performance.now() - startTime)
      setResponseLatency(duration)
      setResponseStatus(0)
      setResponseData({
        error: 'Ağ Bağlantı Hatası veya Sunucu Çevrimdışı',
        detail: err.message,
      })
      setEndpointHealth((prev) => ({
        ...prev,
        [selectedEndpoint.id]: { status: 0, latency: duration },
      }))
    } finally {
      setIsExecuting(false)
    }
  }

  // Quick CRUD Handlers
  const handleSaveDealer = async (e) => {
    e.preventDefault()
    setCrudNotice(null)
    try {
      const res = await createDealer({ bayiKodu: cBayiKodu, unvan: cUnvan, segment: cSegment, bolge: cBolge })
      setCrudNotice({ type: 'success', text: `Bayi (${res.bayiKodu}) başarıyla sisteme eklendi (POST /bayiler).` })
    } catch (err) {
      setCrudNotice({ type: 'error', text: 'Bayi eklenemedi: ' + err.message })
    }
  }

  const handleSaveProduct = async (e) => {
    e.preventDefault()
    setCrudNotice(null)
    try {
      const res = await createProduct({ urunKodu: cUrunKodu, urunAdi: cUrunAdi, kategori: cKategori, birimMaliyet: Number(cBirimMaliyet) })
      setCrudNotice({ type: 'success', text: `Ürün (${res.urunKodu}) başarıyla eklendi (POST /urunler).` })
    } catch (err) {
      setCrudNotice({ type: 'error', text: 'Ürün eklenemedi: ' + err.message })
    }
  }

  const handleSaveStock = async (e) => {
    e.preventDefault()
    setCrudNotice(null)
    try {
      await upsertStock({ urunKodu: sUrunKodu, depoKodu: sDepoKodu, mevcutStok: Number(sMevcutStok) })
      setCrudNotice({ type: 'success', text: `Stok seviyesi güncellendi (POST /stok).` })
    } catch (err) {
      setCrudNotice({ type: 'error', text: 'Stok güncellenemedi: ' + err.message })
    }
  }

  const categories = useMemo(() => {
    const map = {}
    ENDPOINT_DEFINITIONS.forEach((ep) => {
      if (!map[ep.category]) map[ep.category] = []
      map[ep.category].push(ep)
    })
    return map
  }, [])

  return (
    <div className="animate-fade-in">
      <div className="page-header">
        <div className="page-title">
          <h1>API Kontrol & Admin Veri Yönetimi</h1>
          <p>REST endpoint test simülatörü, Admin CRUD işlemleri ve zamanlanmış görev denetimi</p>
        </div>

        <div className="auth-tabs" style={{ margin: 0 }}>
          <button
            type="button"
            className={`auth-tab ${activeTab === 'explorer' ? 'active' : ''}`}
            onClick={() => setActiveTab('explorer')}
          >
            🔍 API Explorer & Test
          </button>
          <button
            type="button"
            className={`auth-tab ${activeTab === 'crud' ? 'active' : ''}`}
            onClick={() => setActiveTab('crud')}
          >
            🛠️ Admin Veri Girişi (CRUD)
          </button>
        </div>
      </div>

      {/* KPI Özeti */}
      <div className="kpi-grid" style={{ marginBottom: '1.5rem' }}>
        <div className="metric-card">
          <div className="metric-card-top">
            <span className="metric-label">Bağlantı Modu</span>
            <span className={`metric-badge ${demo ? 'warning' : 'success'}`}>
              {demo ? 'Simüle Demo' : 'Canlı Supabase API'}
            </span>
          </div>
          <div className="metric-card-body">
            <div className="metric-value" style={{ fontSize: '1.2rem' }}>
              {demo ? 'Çevrimdışı / Demo' : getApiBaseUrl() || 'https://localhost:7000'}
            </div>
          </div>
          <div className="metric-card-footer">
            <span>ASP.NET Core & Supabase PostgreSQL</span>
          </div>
        </div>

        <div className="metric-card">
          <div className="metric-card-top">
            <span className="metric-label">JWT Token Durumu</span>
            <span className={`metric-badge ${token ? 'success' : 'danger'}`}>
              {token ? 'Aktif Oturum' : 'Token Yok (401)'}
            </span>
          </div>
          <div className="metric-card-body">
            <div className="metric-value" style={{ fontSize: '0.9rem', fontFamily: 'var(--font-mono)' }}>
              {token ? `${token.substring(0, 24)}...` : 'Yetkilendirilmemiş'}
            </div>
          </div>
          <div className="metric-card-footer">
            <span>Bearer JWT JWKS Doğrulaması</span>
          </div>
        </div>

        <div className="metric-card">
          <div className="metric-card-top">
            <span className="metric-label">Zamanlanmış Görev (Job)</span>
            <span className="metric-badge info">Skor Job</span>
          </div>
          <div className="metric-card-body">
            <div className="metric-value" style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>
              Zamanlanmış Arka Plan Servisi
            </div>
          </div>
          <div className="metric-card-footer">
            <span>GET /jobs/durum (Otomatik Çalışır)</span>
          </div>
        </div>
      </div>

      {crudNotice && (
        <div className={`auth-alert ${crudNotice.type}`} style={{ marginBottom: '1.25rem' }}>
          <span>{crudNotice.text}</span>
        </div>
      )}

      {/* TAB 1: API Explorer */}
      {activeTab === 'explorer' && (
        <div style={{ display: 'grid', gridTemplateColumns: '340px 1fr', gap: '1.5rem', alignItems: 'start' }}>
          <div className="card-section" style={{ padding: '1rem', margin: 0, maxHeight: '720px', overflowY: 'auto' }}>
            <div style={{ fontSize: '0.8rem', fontWeight: 700, color: 'var(--text-secondary)', textTransform: 'uppercase', marginBottom: '0.75rem', padding: '0 0.25rem' }}>
              Tüm Backend Endpointleri
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
              {Object.entries(categories).map(([catName, eps]) => (
                <div key={catName}>
                  <div style={{ fontSize: '0.72rem', fontWeight: 700, color: 'var(--brand-primary)', textTransform: 'uppercase', marginBottom: '0.35rem', letterSpacing: '0.04em' }}>
                    {catName}
                  </div>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '0.3rem' }}>
                    {eps.map((ep) => {
                      const isSelected = selectedEndpoint.id === ep.id
                      const health = endpointHealth[ep.id]

                      return (
                        <div
                          key={ep.id}
                          onClick={() => handleSelectEndpoint(ep)}
                          style={{
                            padding: '0.5rem 0.65rem',
                            borderRadius: 'var(--radius-sm)',
                            background: isSelected ? 'var(--bg-surface-elevated)' : 'transparent',
                            border: `1px solid ${isSelected ? 'var(--border-medium)' : 'transparent'}`,
                            cursor: 'pointer',
                            display: 'flex',
                            flexDirection: 'column',
                            gap: '0.2rem',
                          }}
                        >
                          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                            <span
                              style={{
                                fontSize: '0.68rem',
                                fontWeight: 700,
                                padding: '0.1rem 0.35rem',
                                borderRadius: '2px',
                                backgroundColor: ep.method === 'GET' ? 'var(--state-info-bg)' : ep.method === 'POST' ? 'var(--state-warning-bg)' : 'var(--state-danger-bg)',
                                color: ep.method === 'GET' ? 'var(--state-info-text)' : ep.method === 'POST' ? 'var(--state-warning-text)' : 'var(--state-danger-text)',
                                border: `1px solid ${ep.method === 'GET' ? 'var(--state-info-border)' : 'var(--state-warning-border)'}`,
                                fontFamily: 'var(--font-mono)',
                              }}
                            >
                              {ep.method}
                            </span>

                            {health && (
                              <span
                                style={{
                                  fontSize: '0.68rem',
                                  fontWeight: 600,
                                  color: health.status === 200 ? 'var(--state-success-accent)' : health.status === 401 ? 'var(--state-warning-accent)' : 'var(--state-danger-accent)',
                                }}
                              >
                                {health.status ? `${health.status} (${health.latency}ms)` : 'Hata'}
                              </span>
                            )}
                          </div>

                          <div style={{ fontSize: '0.78rem', fontFamily: 'var(--font-mono)', color: isSelected ? 'var(--text-primary)' : 'var(--text-secondary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                            {ep.path}
                          </div>
                        </div>
                      )
                    })}
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="card-section" style={{ margin: 0 }}>
            <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', borderBottom: '1px solid var(--border-subtle)', paddingBottom: '1rem', marginBottom: '1.25rem', flexWrap: 'wrap', gap: '1rem' }}>
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.35rem' }}>
                  <span
                    style={{
                      fontSize: '0.75rem',
                      fontWeight: 700,
                      padding: '0.15rem 0.45rem',
                      borderRadius: 'var(--radius-sm)',
                      backgroundColor: selectedEndpoint.method === 'GET' ? 'var(--state-info-bg)' : 'var(--state-warning-bg)',
                      color: selectedEndpoint.method === 'GET' ? 'var(--state-info-text)' : 'var(--state-warning-text)',
                      border: `1px solid ${selectedEndpoint.method === 'GET' ? 'var(--state-info-border)' : 'var(--state-warning-border)'}`,
                      fontFamily: 'var(--font-mono)',
                    }}
                  >
                    {selectedEndpoint.method}
                  </span>
                  <span style={{ fontSize: '1rem', fontWeight: 600, fontFamily: 'var(--font-mono)' }}>
                    {selectedEndpoint.path}
                  </span>
                </div>
                <p style={{ fontSize: '0.82rem', color: 'var(--text-secondary)' }}>
                  {selectedEndpoint.description}
                </p>
              </div>

              <button
                type="button"
                className="btn-primary"
                style={{ width: 'auto', padding: '0.5rem 1.25rem', margin: 0 }}
                onClick={handleExecuteRequest}
                disabled={isExecuting}
              >
                {isExecuting ? 'İstek Gönderiliyor...' : '▶ İsteği Gönder (Send)'}
              </button>
            </div>

            <div style={{ marginBottom: '1.25rem' }}>
              {(selectedEndpoint.path.includes('{kod}') || selectedEndpoint.path.includes('{id}')) && (
                <div className="form-group" style={{ marginBottom: '0.75rem' }}>
                  <label>Path Değişkeni ({selectedEndpoint.path.includes('{kod}') ? '{kod}' : '{id}'}):</label>
                  <input
                    type="text"
                    className="form-input"
                    value={pathKod}
                    onChange={(e) => setPathKod(e.target.value)}
                    placeholder="Bayi veya Kayıt Kodu (Örn: B001)"
                  />
                </div>
              )}

              {Object.keys(queryParams).length > 0 && (
                <div style={{ marginBottom: '1rem' }}>
                  <label style={{ fontSize: '0.75rem', color: 'var(--text-muted)', textTransform: 'uppercase', fontWeight: 600, display: 'block', marginBottom: '0.4rem' }}>
                    Query Parametreleri:
                  </label>
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '0.5rem' }}>
                    {Object.entries(queryParams).map(([key, val]) => (
                      <div key={key}>
                        <span style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', fontWeight: 600, display: 'block', marginBottom: '0.2rem' }}>{key}</span>
                        <input
                          type="text"
                          className="form-input"
                          value={val}
                          onChange={(e) => setQueryParams({ ...queryParams, [key]: e.target.value })}
                        />
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {(selectedEndpoint.method === 'POST' || selectedEndpoint.method === 'PUT') && (
                <div className="form-group">
                  <label>Request JSON Body:</label>
                  <textarea
                    className="form-input"
                    style={{ fontFamily: 'var(--font-mono)', minHeight: '130px', resize: 'vertical' }}
                    value={requestBody}
                    onChange={(e) => setRequestBody(e.target.value)}
                  />
                </div>
              )}
            </div>

            <div>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '0.5rem' }}>
                <span style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--text-secondary)' }}>
                  Sunucu Yanıtı (Response):
                </span>
                {responseStatus !== null && (
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
                    <span
                      style={{
                        fontSize: '0.75rem',
                        fontWeight: 700,
                        padding: '0.15rem 0.5rem',
                        borderRadius: 'var(--radius-sm)',
                        background: responseStatus === 200 ? 'var(--state-success-bg)' : 'var(--state-warning-bg)',
                        color: responseStatus === 200 ? 'var(--state-success-text)' : 'var(--state-warning-text)',
                      }}
                    >
                      HTTP {responseStatus}
                    </span>
                    {responseLatency !== null && (
                      <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>⏱️ {responseLatency} ms</span>
                    )}
                  </div>
                )}
              </div>

              <div
                style={{
                  background: 'var(--bg-input)',
                  border: '1px solid var(--border-subtle)',
                  borderRadius: 'var(--radius-sm)',
                  padding: '1rem',
                  maxHeight: '380px',
                  overflowY: 'auto',
                  fontFamily: 'var(--font-mono)',
                  fontSize: '0.8rem',
                  whiteSpace: 'pre-wrap',
                }}
              >
                {isExecuting ? 'İstek gönderiliyor...' : responseData ? JSON.stringify(responseData, null, 2) : 'Henüz istek gönderilmedi.'}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* TAB 2: Admin CRUD Paneli */}
      {activeTab === 'crud' && (
        <div className="card-section animate-fade-in">
          <div className="card-section-header">
            <div className="card-section-title">
              <h2>🛠️ Admin Veri Girişi ve Güncelleme (CRUD)</h2>
            </div>
            <div className="auth-tabs" style={{ margin: 0 }}>
              <button type="button" className={`auth-tab ${crudType === 'dealer' ? 'active' : ''}`} onClick={() => setCrudType('dealer')}>
                Bayi Ekle (POST /bayiler)
              </button>
              <button type="button" className={`auth-tab ${crudType === 'product' ? 'active' : ''}`} onClick={() => setCrudType('product')}>
                Ürün Ekle (POST /urunler)
              </button>
              <button type="button" className={`auth-tab ${crudType === 'stock' ? 'active' : ''}`} onClick={() => setCrudType('stock')}>
                Stok Güncelle (POST /stok)
              </button>
            </div>
          </div>

          {crudType === 'dealer' && (
            <form onSubmit={handleSaveDealer} className="form-grid-2">
              <div className="form-group">
                <label>Bayi Kodu</label>
                <input type="text" className="form-input" placeholder="B026" value={cBayiKodu} onChange={(e) => setCBayiKodu(e.target.value)} required />
              </div>
              <div className="form-group">
                <label>Bayi Unvanı</label>
                <input type="text" className="form-input" placeholder="Anadolu Bilişim A.Ş." value={cUnvan} onChange={(e) => setCUnvan(e.target.value)} required />
              </div>
              <div className="form-group">
                <label>Segment</label>
                <select className="form-input" value={cSegment} onChange={(e) => setCSegment(e.target.value)}>
                  <option value="A">Segment A</option>
                  <option value="B">Segment B</option>
                  <option value="C">Segment C</option>
                </select>
              </div>
              <div className="form-group">
                <label>Bölge</label>
                <select className="form-input" value={cBolge} onChange={(e) => setCBolge(e.target.value)}>
                  <option value="Marmara">Marmara</option>
                  <option value="Ege">Ege</option>
                  <option value="İç Anadolu">İç Anadolu</option>
                  <option value="Akdeniz">Akdeniz</option>
                  <option value="Karadeniz">Karadeniz</option>
                </select>
              </div>
              <div style={{ gridColumn: 'span 2' }}>
                <button type="submit" className="btn-primary">Sisteme Bayi Kaydet (POST /bayiler)</button>
              </div>
            </form>
          )}

          {crudType === 'product' && (
            <form onSubmit={handleSaveProduct} className="form-grid-2">
              <div className="form-group">
                <label>Ürün Kodu</label>
                <input type="text" className="form-input" placeholder="U1500" value={cUrunKodu} onChange={(e) => setCUrunKodu(e.target.value)} required />
              </div>
              <div className="form-group">
                <label>Ürün Adı</label>
                <input type="text" className="form-input" placeholder="AI Accelerator GPU" value={cUrunAdi} onChange={(e) => setCUrunAdi(e.target.value)} required />
              </div>
              <div className="form-group">
                <label>Kategori</label>
                <input type="text" className="form-input" placeholder="Sunucu / Ağ / Depolama" value={cKategori} onChange={(e) => setCKategori(e.target.value)} required />
              </div>
              <div className="form-group">
                <label>Birim Maliyet (₺)</label>
                <input type="number" className="form-input" placeholder="145000" value={cBirimMaliyet} onChange={(e) => setCBirimMaliyet(e.target.value)} required />
              </div>
              <div style={{ gridColumn: 'span 2' }}>
                <button type="submit" className="btn-primary">Yeni Ürün Ekle (POST /urunler)</button>
              </div>
            </form>
          )}

          {crudType === 'stock' && (
            <form onSubmit={handleSaveStock} className="form-grid-2">
              <div className="form-group">
                <label>Ürün Kodu</label>
                <input type="text" className="form-input" placeholder="U1001" value={sUrunKodu} onChange={(e) => setSUrunKodu(e.target.value)} required />
              </div>
              <div className="form-group">
                <label>Depo Kodu</label>
                <input type="text" className="form-input" placeholder="DP01" value={sDepoKodu} onChange={(e) => setSDepoKodu(e.target.value)} required />
              </div>
              <div className="form-group" style={{ gridColumn: 'span 2' }}>
                <label>Mevcut Depo Stok Adedi</label>
                <input type="number" className="form-input" placeholder="50" value={sMevcutStok} onChange={(e) => setSMevcutStok(e.target.value)} required />
              </div>
              <div style={{ gridColumn: 'span 2' }}>
                <button type="submit" className="btn-primary">Stok Seviyesi Güncelle (POST /stok)</button>
              </div>
            </form>
          )}
        </div>
      )}
    </div>
  )
}
export default ControlPanel
