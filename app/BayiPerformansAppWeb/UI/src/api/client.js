// client.js - Canlı Backend API İstemcisi (A, B, C Segmentleri Temizlenmiş)
import {
  MOCK_DEALERS,
  MOCK_PRODUCTS,
  MOCK_WAREHOUSES,
  MOCK_RISK_LIST,
  MOCK_PERFORMANCES,
  MOCK_CRITICAL_STOCK,
  generateDynamicMockPerformance,
  generateMockHistory,
} from './mockData'

// İstemci ve Katalog Önbellekleri
const clientRequestCache = new Map()
let dealersCache = null
let warehousesCache = null
let productsCache = null

export function clearClientCache() {
  clientRequestCache.clear()
  dealersCache = null
  warehousesCache = null
  productsCache = null
}

export function isDemoMode() {
  const val = localStorage.getItem('bp_demo_mode')
  if (val === null) return false
  return val === 'true'
}

export function setDemoMode(val) {
  localStorage.setItem('bp_demo_mode', val ? 'true' : 'false')
  clearClientCache()
}

export function getToken() {
  return localStorage.getItem('bp_auth_token')
}

export function getRefreshToken() {
  return localStorage.getItem('bp_refresh_token') || 'demo_refresh_token'
}

export async function getMe() {
  const token = getToken()
  if (!token && !isDemoMode()) return null
  if (!isDemoMode()) {
    try {
      const data = await request('/auth/me')
      if (data) {
        return {
          email: data.email || data.Email,
          role: data.role || data.rol || data.Role || 'User',
          name: data.adSoyad || data.ad_soyad || data.name || data.AdSoyad || 'Kullanıcı',
          ...data,
        }
      }
    } catch {
      return null
    }
  }
  return { email: 'admin@bayiperformans.com', role: 'Admin', name: 'Yönetici' }
}

export function getApiBaseUrl() {
  const envUrl = import.meta.env.VITE_API_BASE_URL || ''
  return envUrl.replace(/\/+$/, '')
}

export function getApiUrl(endpoint = '') {
  if (!endpoint) return getApiBaseUrl()
  if (endpoint.startsWith('http://') || endpoint.startsWith('https://')) {
    return endpoint
  }
  const baseUrl = getApiBaseUrl()
  if (!baseUrl) return endpoint
  const path = endpoint.startsWith('/') ? endpoint : `/${endpoint}`
  return `${baseUrl}${path}`
}


async function request(endpoint, options = {}) {
  const method = (options.method || 'GET').toUpperCase()
  const isGet = method === 'GET'
  const bypassCache = options.bypassCache === true

  if (isGet && !bypassCache) {
    const cached = clientRequestCache.get(endpoint)
    if (cached && cached.expiry > Date.now()) {
      return cached.data
    }
  }

  // Mutating requests invalidate cache
  if (!isGet) {
    clientRequestCache.clear()
  }

  const token = getToken()
  const headers = {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...options.headers,
  }

  const url = getApiUrl(endpoint)
  const response = await fetch(url, { ...options, headers })

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}))
    const error = new Error(errorData.detail || errorData.title || errorData.message || `API Hatası (${response.status})`)
    error.status = response.status
    error.data = errorData
    throw error
  }

  if (response.status === 204) return null
  const data = await response.json()

  if (isGet) {
    // 30 saniyelik hafif istemci önbelleği (Sekme geçişlerini anında yapar)
    clientRequestCache.set(endpoint, {
      data,
      expiry: Date.now() + 30 * 1000,
    })
  }

  return data
}

export function getFastApiBaseUrl() {
  const envUrl = import.meta.env.VITE_FASTAPI_BASE_URL
  if (envUrl) return envUrl.replace(/\/+$/, '')
  return 'http://localhost:8000'
}

export function getFastApiUrl(endpoint = '') {
  if (!endpoint) return getFastApiBaseUrl()
  if (endpoint.startsWith('http://') || endpoint.startsWith('https://')) {
    return endpoint
  }
  const baseUrl = getFastApiBaseUrl()
  const path = endpoint.startsWith('/') ? endpoint : `/${endpoint}`
  return `${baseUrl}${path}`
}

async function fastApiRequest(endpoint, options = {}) {
  const token = getToken()
  const headers = {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...options.headers,
  }

  const url = getFastApiUrl(endpoint)
  const response = await fetch(url, { ...options, headers })

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}))
    const error = new Error(errorData.detail || errorData.title || errorData.message || `FastAPI Hatası (${response.status})`)
    error.status = response.status
    error.data = errorData
    throw error
  }

  if (response.status === 204) return null
  return await response.json()
}

function normalizeProductResponse(p) {
  return {
    urunKodu: p.urun_kodu || p.urunKodu || p.kod || p.UrunKodu || '',
    urunAdi: p.urun_adi || p.urunAdi || p.ad || p.UrunAdi || p.urun_kodu || p.urunKodu || '',
    kategori: p.kategori_adi || p.kategoriAdi || p.kategori || p.Kategori || 'Genel',
    birimMaliyet: p.birim_maliyet ?? p.birimMaliyet ?? p.BirimMaliyet ?? 0,
  }
}

function normalizeWarehouseResponse(w) {
  return {
    depoKodu: w.depo_kodu || w.depoKodu || w.kod || w.DepoKodu || '',
    depoAdi: w.depo_adi || w.depoAdi || w.ad || w.DepoAdi || w.depo_kodu || w.depoKodu || '',
    bolge: w.bolge || w.Bolge || '',
  }
}

export function getDefaultWarehouseForDealer(dealer) {
  if (dealer?.varsayilan_depo_kodu || dealer?.varsayilanDepoKodu) {
    return dealer.varsayilan_depo_kodu || dealer.varsayilanDepoKodu
  }
  const sehir = (dealer?.sehir || dealer?.Sehir || '').toUpperCase()
  const bolge = (dealer?.bolge || dealer?.Bolge || '').toUpperCase()

  if (sehir.includes('ANKARA') || sehir.includes('ESKİŞEHİR') || sehir.includes('ESKISEHIR') || sehir.includes('KONYA') || sehir.includes('KAYSERİ') || sehir.includes('KAYSERI') || bolge.includes('İÇ ANADOLU') || bolge.includes('IC ANADOLU') || bolge.includes('DOĞU ANADOLU') || bolge.includes('DOGU ANADOLU')) {
    return 'DP02'
  }
  if (sehir.includes('İZMİR') || sehir.includes('IZMIR') || sehir.includes('MANİSA') || sehir.includes('MANISA') || sehir.includes('AYDIN') || sehir.includes('DENİZLİ') || sehir.includes('DENIZLI') || bolge.includes('EGE')) {
    return 'DP03'
  }
  if (sehir.includes('ADANA') || sehir.includes('MERSİN') || sehir.includes('MERSIN') || sehir.includes('ANTALYA') || sehir.includes('HATAY') || bolge.includes('AKDENİZ') || bolge.includes('AKDENIZ')) {
    return 'DP04'
  }
  if (sehir.includes('SAMSUN') || sehir.includes('TRABZON') || sehir.includes('ORDU') || sehir.includes('RİZE') || sehir.includes('RIZE') || bolge.includes('KARADENİZ') || bolge.includes('KARADENIZ')) {
    return 'DP05'
  }
  if (sehir.includes('GAZİANTEP') || sehir.includes('GAZIANTEP') || sehir.includes('ŞANLIURFA') || sehir.includes('SANLIURFA') || sehir.includes('DİYARBAKIR') || sehir.includes('DIYARBAKIR') || bolge.includes('GÜNEYDOĞU') || bolge.includes('GUNEYDOGU')) {
    return 'DP06'
  }
  return 'DP01'
}

function normalizeDealerResponse(d) {
  const sehir = d.sehir || d.Sehir || ''
  const bolge = d.bolge || d.Bolge || ''
  return {
    bayiKodu: d.bayi_kodu || d.bayiKodu || d.kod || d.BayiKodu || '',
    unvan: d.unvan || d.bayi_adi || d.bayiAdi || d.Unvan || `${d.bayi_kodu || d.bayiKodu || d.BayiKodu || ''} Bayisi`,
    segment: d.segment || d.Segment || '',
    bolge: bolge,
    sehir: sehir,
    varsayilanDepoKodu: d.varsayilan_depo_kodu || d.varsayilanDepoKodu || getDefaultWarehouseForDealer({ sehir, bolge }),
  }
}

function normalizeDealerPerformanceResponse(data) {
  const gerceklesenCiro = data.gerceklesen_ciro ?? data.gerceklesenCiro ?? data.GerceklesenCiro ?? data.ciro ?? data.Ciro ?? 0
  const hedefCiro = data.hedef_ciro ?? data.hedefCiro ?? data.HedefCiro ?? data.hedef ?? data.Hedef ?? null
  const hedefeUlasmaPct = data.hedefe_ulasma_pct ?? data.hedefeUlasmaPct ?? data.HedefeUlasmaPct ?? data.hedef_tutma_orani ?? data.hedefTutmaOrani ?? data.HedefTutmaOrani ?? null
  const ortalamaMarjPct = data.ortalama_marj_pct ?? data.ortalamaMarjPct ?? data.OrtalamaMarjPct ?? data.ortalama_kar_marji ?? data.ortalamaKarMarji ?? data.OrtalamaKarMarji ?? null
  const marjEsigiPct = data.marj_esigi_pct ?? data.marjEsigiPct ?? data.MarjEsigiPct ?? 0.15
  const ortalamaIskontoPct = data.ortalama_iskonto_pct ?? data.ortalamaIskontoPct ?? data.OrtalamaIskontoPct ?? data.ortalama_iskonto_orani ?? data.ortalamaIskontoOrani ?? data.OrtalamaIskontoOrani ?? null
  const iadeOraniPct = data.iade_orani_pct ?? data.iadeOraniPct ?? data.IadeOraniPct ?? data.ortalama_iade_orani ?? data.ortalamaIadeOrani ?? data.OrtalamaIadeOrani ?? 0
  const riskSkoru = data.risk_skoru ?? data.riskSkoru ?? data.RiskSkoru ?? 0
  const riskSeviyesi = data.risk_seviyesi || data.riskSeviyesi || data.RiskSeviyesi || 'DUSUK'
  const gerekceKodlari = data.gerekce_kodlari || data.gerekceKodlari || data.GerekceKodlari || []
  const kritikStokUrunSayisi = data.kritik_stok_urun_sayisi ?? data.kritikStokUrunSayisi ?? data.KritikStokUrunSayisi ?? data.kritikStokKalanUrunSayisi ?? data.kritik_stok_kalan_urun_sayisi ?? data.KritikStokKalanUrunSayisi ?? 0

  return {
    bayiKodu: data.bayi_kodu || data.bayiKodu || data.BayiKodu || '',
    donem: data.donem || data.Donem || '',
    segment: data.segment || data.Segment || '',
    bolge: data.bolge || data.Bolge || '',
    hedefCiro,
    gerceklesenCiro,
    hedefeUlasmaPct,
    ortalamaMarjPct,
    marjEsigiPct,
    ortalamaIskontoPct,
    iadeOraniPct,
    riskSkoru,
    riskSeviyesi,
    gerekceKodlari,
    kritikStokUrunSayisi,
    aiComment: data.ai_comment || data.aiComment || data.AiComment || null,
    alertMessageDraft: data.alert_message_draft || data.alertMessageDraft || data.AlertMessageDraft || null,
    commentSource: data.comment_source || data.commentSource || data.CommentSource || 'DETERMINISTIC',
  }
}

function normalizeRiskListResponse(data) {
  const listRaw = data.bayiler || data.items || data.Bayiler || data.Items || (Array.isArray(data) ? data : [])
  const bayiler = listRaw.map((b) => ({
    bayiKodu: b.bayi_kodu || b.bayiKodu || b.kod || b.BayiKodu || '',
    bolge: b.bolge || b.Bolge || '',
    segment: b.segment || b.Segment || '',
    gerceklesenCiro: b.gerceklesen_ciro ?? b.gerceklesenCiro ?? b.ciro ?? b.Ciro ?? 0,
    hedefCiro: b.hedef_ciro ?? b.hedefCiro ?? b.hedef ?? b.Hedef ?? 0,
    hedefeUlasmaPct: b.hedefe_ulasma_pct ?? b.hedefeUlasmaPct ?? b.HedefeUlasmaPct ?? b.hedef_tutma_orani ?? b.hedefTutmaOrani ?? b.HedefTutmaOrani,
    ortalamaMarjPct: b.ortalama_marj_pct ?? b.ortalamaMarjPct ?? b.OrtalamaMarjPct ?? b.ortalama_kar_marji ?? b.ortalamaKarMarji ?? b.OrtalamaKarMarji,
    ortalamaIskontoPct: b.ortalama_iskonto_pct ?? b.ortalamaIskontoPct ?? b.OrtalamaIskontoPct ?? b.ortalama_iskonto_orani ?? b.ortalamaIskontoOrani ?? b.OrtalamaIskontoOrani,
    riskSkoru: b.risk_skoru ?? b.riskSkoru ?? b.RiskSkoru ?? 0,
    riskSeviyesi: b.risk_seviyesi || b.riskSeviyesi || b.RiskSeviyesi || 'DUSUK',
    gerekceKodlari: b.gerekce_kodlari || b.gerekceKodlari || b.GerekceKodlari || [],
    kritikStokVarMi: b.kritik_stok_var_mi ?? b.kritikStokVarMi ?? b.KritikStokVarMi ?? ((b.kritikStokKalanUrunSayisi ?? b.kritik_stok_kalan_urun_sayisi ?? 0) > 0),
  }))

  return {
    donem: data.donem || data.Donem || '',
    toplam: data.toplam ?? data.totalCount ?? data.Toplam ?? bayiler.length,
    bayiler,
  }
}

function normalizeCriticalStockResponse(data) {
  const listRaw = data.urunler || data.items || data.Urunler || data.Items || (Array.isArray(data) ? data : [])
  const list = listRaw.map((item) => {
    const kalanGun = item.kalan_stok_gunu ?? item.tahmini_stok_gunu ?? item.tahminiStokGunu ?? item.TahminiStokGunu ?? null
    const kritikMi = item.stok_kritik_mi ?? item.stokKritikMi ?? item.StokKritikMi ?? (kalanGun !== null && kalanGun < 7)
    const son90Satis = item.son_90_gun_satis ?? item.son_uc_ay_satis_adedi ?? item.sonUcAySatisAdedi ?? item.SonUcAySatisAdedi ?? item.son90GunToplamSatis ?? item.Son90GunToplamSatis ?? 0

    return {
      urunKodu: item.urun_kodu || item.urunKodu || item.kod || item.UrunKodu || '',
      urunAdi: item.urun_adi || item.urunAdi || item.UrunAdi || '',
      depoKodu: item.depo_kodu || item.depoKodu || item.DepoKodu || '',
      depoAdi: item.depo_adi || item.depoAdi || item.DepoAdi || '',
      mevcutStok: item.mevcut_stok ?? item.mevcutStok ?? item.MevcutStok ?? 0,
      son90GunSatis: son90Satis,
      sonUcAySatisAdedi: son90Satis,
      ortalamaGunlukSatis: item.ortalama_gunluk_satis ?? item.gunluk_ortalama_satis ?? item.ortalamaGunlukSatis ?? item.OrtalamaGunlukSatis ?? item.GunlukOrtalamaSatis ?? 0,
      tahminiStokGunu: kalanGun,
      kalanStokGunu: kalanGun,
      stokKritikMi: kritikMi,
      gerekceKodlari: item.gerekce_kodlari || item.gerekceKodlari || item.GerekceKodlari || (kritikMi ? ['DINAMIK_ESIK_ALTI', 'STOK_GUNU_KRITIK'] : ['STOK_YETERLI']),
    }
  })

  return {
    toplam: data.toplam ?? data.Toplam ?? list.length,
    urunler: list,
  }
}

// ─── AUTH SERVİSLERİ ───
export async function loginUser(email, password, dogrulamaToken = null) {
  if (!isDemoMode()) {
    const payload = { email, sifre: password, password }
    if (dogrulamaToken) {
      payload.dogrulama_token = dogrulamaToken
      payload.dogrulamaToken = dogrulamaToken
    }
    const data = await request('/auth/login', {
      method: 'POST',
      body: JSON.stringify(payload),
    })

    if (data?.otpGerekli || data?.otp_gerekli || data?.islemId || data?.islem_id) {
      return {
        requireTwoFactor: true,
        islemId: data.islemId || data.islem_id,
        islem_id: data.islemId || data.islem_id,
        maskelenmisEmail: data.maskelenmisEmail || data.maskelenmis_email,
        gecerlilikSaniyesi: data.gecerlilikSaniyesi || data.gecerlilik_saniyesi || 180,
      }
    }

    const tok = data?.accessToken || data?.access_token || data?.token
    if (tok) {
      localStorage.setItem('bp_auth_token', tok)
      if (data.refreshToken || data.refresh_token) {
        localStorage.setItem('bp_refresh_token', data.refreshToken || data.refresh_token)
      }
    }
    return {
      access_token: tok,
      token: tok,
      user: { email: data?.email || email, role: 'User' },
      requireTwoFactor: false,
    }
  }

  const tok = 'demo_jwt_token_' + Date.now()
  localStorage.setItem('bp_auth_token', tok)
  return {
    access_token: tok,
    token: tok,
    requireTwoFactor: false,
    user: { email: email || 'admin@bayiperformans.com', role: 'Admin' },
  }
}

export const login = loginUser

export async function registerUser(email, password, role = 'Manager') {
  if (!isDemoMode()) {
    return await request('/auth/kayit', {
      method: 'POST',
      body: JSON.stringify({ email, sifre: password, password, role, adSoyad: email.split('@')[0] }),
    })
  }
  return { message: 'Kayıt başarılı. Şimdi giriş yapabilirsiniz.' }
}

export const register = registerUser

export async function sendOtp(params) {
  const email = typeof params === 'string' ? params : params?.email || ''
  if (!email) {
    throw new Error('E-posta adresi zorunludur.')
  }

  if (!isDemoMode()) {
    const res = await request('/auth/otp/gonder', {
      method: 'POST',
      body: JSON.stringify({ email, islem_tipi: 'LOGIN_2FA' }),
    })

    const islemId = res?.islem_id || res?.islemId
    if (!res || !islemId) {
      throw new Error(res?.message || 'OTP doğrulama kodu ve işlem ID (islem_id) alınamadı.')
    }

    return {
      islem_id: islemId,
      islemId: islemId,
      message: res.message || `6 haneli doğrulama kodu ${email} adresine gönderildi.`,
      ...res,
    }
  }

  return {
    islem_id: `DEMO-OTP-${Date.now()}`,
    islemId: `DEMO-OTP-${Date.now()}`,
    message: `6 haneli doğrulama kodu ${email} adresine gönderildi.`,
  }
}

export async function verifyTwoFactor(params) {
  const kod = typeof params === 'string' ? params : params?.kod || params?.code || ''
  const islemId = params?.islemId || params?.islem_id || ''

  if (!kod || kod.trim().length < 6) {
    throw new Error('Lütfen 6 haneli OTP doğrulama kodunu giriniz.')
  }

  if (!islemId && !isDemoMode()) {
    throw new Error('Geçerli bir OTP işlem kimliği (islem_id) bulunamadı.')
  }

  if (!isDemoMode()) {
    const data = await request('/auth/otp/dogrula', {
      method: 'POST',
      body: JSON.stringify({ islem_id: islemId, kod: kod.trim() }),
    })

    if (data?.dogrulandi !== true) {
      throw new Error(data?.message || data?.detail || 'OTP doğrulama başarısız (dogrulandi !== true). Kod geçersiz veya süresi dolmuş.')
    }

    const verificationToken = data?.dogrulama_token || data?.dogrulamaToken
    const tok = data?.accessToken || data?.access_token || data?.token
    if (tok) localStorage.setItem('bp_auth_token', tok)

    return {
      dogrulandi: true,
      basarili: true,
      access_token: tok,
      token: tok,
      dogrulama_token: verificationToken,
      dogrulamaToken: verificationToken,
      ...data,
    }
  }

  const tok = 'demo_2fa_token_' + Date.now()
  localStorage.setItem('bp_auth_token', tok)
  return {
    dogrulandi: true,
    basarili: true,
    access_token: tok,
    token: tok,
    user: { email: 'admin@bayiperformans.com', role: 'Admin' },
  }
}

export const verifyOtp = verifyTwoFactor

export async function getOtpStatus() {
  return { enabled: true, isConfigured: true }
}

export async function logoutUser() {
  localStorage.removeItem('bp_auth_token')
  localStorage.removeItem('bp_refresh_token')
}

export const logout = logoutUser

// ─── DENETİM & YÖNETİM SERVİSLERİ ───
export async function createDealer(dealerData) {
  if (!isDemoMode()) {
    return await request('/bayiler', {
      method: 'POST',
      body: JSON.stringify(dealerData),
    })
  }
  return { message: 'Bayi başarıyla kaydedildi.', bayi: dealerData }
}

export async function updateDealer(kod, dealerData) {
  if (!isDemoMode()) {
    return await request(`/bayiler/${encodeURIComponent(kod)}`, {
      method: 'PUT',
      body: JSON.stringify(dealerData),
    })
  }
  return { message: 'Bayi güncellendi.', kod }
}

export async function createProduct(productData) {
  if (!isDemoMode()) {
    return await request('/urunler', {
      method: 'POST',
      body: JSON.stringify(productData),
    })
  }
  return { message: 'Ürün başarıyla eklendi.', product: productData }
}

export async function upsertStock(stockData) {
  if (!isDemoMode()) {
    return await request('/stok', {
      method: 'POST',
      body: JSON.stringify(stockData),
    })
  }
  return { message: 'Stok hareketi işlendi.', stock: stockData }
}

export async function getJobStatus(jobId) {
  if (!isDemoMode()) {
    return await request(`/jobs/${encodeURIComponent(jobId)}`)
  }
  return { jobId, status: 'COMPLETED', progressPct: 100 }
}

// ─── KATALOG & TANIM SERVİSLERİ ───


const sortByCode = (list, key) =>
  [...list].sort((a, b) =>
    (a[key] || '').localeCompare(b[key] || '', undefined, { numeric: true, sensitivity: 'base' })
  )

export async function getDealersList() {
  if (!isDemoMode()) {
    const data = await request('/bayiler')
    const list = Array.isArray(data) ? data : data?.items || data?.bayiler || data?.Bayiler || []
    if (Array.isArray(list) && list.length > 0) {
      return sortByCode(list.map(normalizeDealerResponse), 'bayiKodu')
    }
    return []
  }

  if (dealersCache && dealersCache.length > 0) return dealersCache
  dealersCache = sortByCode(MOCK_DEALERS.map(normalizeDealerResponse), 'bayiKodu')
  return dealersCache
}

export async function getProductsList() {
  if (!isDemoMode()) {
    const data = await request('/urunler')
    const list = Array.isArray(data) ? data : data?.items || data?.urunler || data?.Urunler || []
    if (Array.isArray(list) && list.length > 0) {
      return sortByCode(list.map(normalizeProductResponse), 'urunKodu')
    }
    return []
  }

  if (productsCache && productsCache.length > 0) return productsCache
  productsCache = sortByCode(MOCK_PRODUCTS.map(normalizeProductResponse), 'urunKodu')
  return productsCache
}

export async function getWarehousesList() {
  if (!isDemoMode()) {
    const dataDepo = await request('/depolar')
    const list = Array.isArray(dataDepo) ? dataDepo : dataDepo?.items || dataDepo?.depolar || dataDepo?.Depolar || []
    if (Array.isArray(list) && list.length > 0) {
      return sortByCode(list.map(normalizeWarehouseResponse), 'depoKodu')
    }
    return []
  }

  if (warehousesCache && warehousesCache.length > 0) return warehousesCache
  warehousesCache = sortByCode(MOCK_WAREHOUSES.map(normalizeWarehouseResponse), 'depoKodu')
  return warehousesCache
}

export async function getFilterOptions() {
  if (!isDemoMode()) {
    const data = await request('/tanimlar/filtre-secenekleri')
    if (data && (data.segmentler || data.bolgeler || data.Segmentler || data.Bolgeler || data.donemler || data.Donemler)) {
      const rawSegs = data.segmentler || data.Segmentler || []
      const filteredSegs = rawSegs.filter((s) => !['A', 'B', 'C', 'Segment A', 'Segment B', 'Segment C'].includes(s))
      return {
        segmentler: filteredSegs.length > 0 ? filteredSegs : ['Bireysel', 'KOBİ', 'Kurumsal'],
        bolgeler: data.bolgeler || data.Bolgeler || [],
        donemler: data.donemler || data.Donemler || [],
      }
    }
    return { segmentler: [], bolgeler: [], donemler: [] }
  }
  return {
    segmentler: ['Bireysel', 'KOBİ', 'Kurumsal'],
    bolgeler: ['Marmara', 'Ege', 'İç Anadolu', 'Akdeniz', 'Karadeniz', 'Güneydoğu Anadolu', 'Doğu Anadolu'],
    donemler: [],
  }
}

// ─── PERFORMANS & RİSK ANALİZİ SERVİSLERİ ───
export async function getRiskList({ donem, segment, bolge } = {}) {
  if (!isDemoMode()) {
    const params = new URLSearchParams()
    if (donem) params.append('donem', donem)
    if (segment) params.append('segment', segment)
    if (bolge) params.append('bolge', bolge)
    const queryString = params.toString() ? `?${params.toString()}` : ''

    const data = await request(`/bayiler/risk-listesi${queryString}`)
    if (data && (data.bayiler || data.Bayiler || Array.isArray(data))) {
      return normalizeRiskListResponse(data)
    }
    throw new Error('Bayi risk listesi verisi alinamadi.')
  }

  let list = [...MOCK_RISK_LIST.bayiler]
  if (segment) {
    list = list.filter((b) =>
      b.segment === segment ||
      `Segment ${b.segment}`.toLowerCase() === segment.toLowerCase() ||
      b.segment.toLowerCase() === segment.toLowerCase()
    )
  }
  if (bolge) list = list.filter((b) => b.bolge === bolge)

  return {
    donem: donem || MOCK_RISK_LIST.donem,
    toplam: list.length,
    bayiler: list,
  }
}

export async function getDealerPerformance(kod, donem) {
  if (!isDemoMode()) {
    const params = new URLSearchParams()
    if (donem) params.append('donem', donem)
    const queryString = params.toString() ? `?${params.toString()}` : ''

    const perfData = await request(`/api/v1/performance/dealers/${encodeURIComponent(kod)}${queryString}`)
    if (perfData) return normalizeDealerPerformanceResponse(perfData)
    throw new Error(`${kod} icin performans kaydi alinamadi.`)
  }

  return MOCK_PERFORMANCES[kod] || generateDynamicMockPerformance(kod, donem)
}

export async function getDealerHistory(kod, aySayisi = 60) {
  const safeAySayisi = Math.min(Math.max(aySayisi || 60, 1), 60)
  if (!isDemoMode()) {
    const data = await request(`/bayi/${encodeURIComponent(kod)}/gecmis?ay_sayisi=${safeAySayisi}`)
    const rawList = Array.isArray(data)
      ? data
      : data?.aylik_performanslar || data?.aylikPerformanslar || data?.gecmis || data?.items || data?.history || []

    if (Array.isArray(rawList)) {
      return rawList.map((item) => ({
        donem: item.donem || item.Donem || '',
        gerceklesenCiro: item.gerceklesen_ciro ?? item.gerceklesenCiro ?? item.GerceklesenCiro ?? 0,
        hedefCiro: item.hedef_ciro ?? item.hedefCiro ?? item.HedefCiro,
        hedefeUlasmaPct: item.hedefe_ulasma_pct ?? item.hedefeUlasmaPct ?? item.HedefeUlasmaPct,
        ortalamaMarjPct: item.ortalama_marj_pct ?? item.ortalamaMarjPct ?? item.OrtalamaMarjPct,
        riskSkoru: item.risk_skoru ?? item.riskSkoru ?? item.RiskSkoru ?? 0,
        riskSeviyesi: item.risk_seviyesi || item.riskSeviyesi || item.RiskSeviyesi || 'DUSUK',
        gerekceKodlari: item.gerekce_kodlari || item.gerekceKodlari || item.GerekceKodlari || [],
      }))
    }
    return []
  }
  return generateMockHistory(kod, safeAySayisi)
}

export async function getCriticalStock({ depoKodu, urunKodu, bayiKodu, sadeceKritik } = {}) {
  if (!isDemoMode()) {
    const params = new URLSearchParams()
    if (depoKodu) params.append('depo_kodu', depoKodu)
    if (urunKodu) params.append('urun_kodu', urunKodu)
    if (bayiKodu) params.append('bayi_kodu', bayiKodu)
    if (sadeceKritik !== undefined) params.append('sadece_kritik', String(sadeceKritik))
    const queryString = params.toString() ? `?${params.toString()}` : ''

    const data = await request(`/stok/kritik${queryString}`)
    if (data) return normalizeCriticalStockResponse(data)
    return { toplam: 0, urunler: [] }
  }

  let list = MOCK_CRITICAL_STOCK.urunler || []
  if (depoKodu) list = list.filter((u) => u.depoKodu === depoKodu)
  if (urunKodu) list = list.filter((u) => u.urunKodu === urunKodu)

  return {
    toplam: list.length,
    urunler: list,
  }
}

// ─── TEKLİF & SİMÜLASYON SERVİSLERİ ───
export async function createAndEvaluateOffer({ bayiKodu, aciklama, kalemler }) {
  const payload = {
    bayi_kodu: bayiKodu?.trim(),
    aciklama: aciklama || '',
    kalemler: kalemler.map((k, idx) => ({
      sira_no: k.siraNo || k.sira_no || idx + 1,
      urun_kodu: k.urunKodu?.trim() || k.urun_kodu?.trim(),
      depo_kodu: k.depoKodu?.trim() || k.depo_kodu?.trim() || 'DP01',
      miktar: Number(k.miktar || 1),
      birim_fiyat: Number(k.birimFiyat || k.birim_fiyat || 0),
      iskonto_orani: Number(k.iskontoOrani != null ? (Number(k.iskontoOrani) > 1 ? Number(k.iskontoOrani) / 100 : Number(k.iskontoOrani)) : 0),
    })),
  }

  if (!isDemoMode()) {
    const data = await fastApiRequest('/api/v1/teklifler/olustur-ve-degerlendir', {
      method: 'POST',
      body: JSON.stringify(payload),
    })
    return normalizeOfferDto(data)
  }

  return generateMockOfferDto(payload)
}

export async function createDraftOffer({ bayiKodu, depoKodu, kalemler }) {
  return createAndEvaluateOffer({ bayiKodu, depoKodu, aciklama: '', kalemler })
}

export async function reEvaluateOffer(teklifId) {
  if (!isDemoMode()) {
    const data = await fastApiRequest(`/api/v1/teklifler/${encodeURIComponent(teklifId)}/yeniden-degerlendir`, {
      method: 'POST',
    })
    return normalizeOfferDto(data)
  }
  return generateMockOfferDto({ teklif_id: teklifId })
}

export async function evaluateDraftOffer(teklifId) {
  return reEvaluateOffer(teklifId)
}

export async function getOfferDetail(teklifId) {
  if (!isDemoMode()) {
    const data = await fastApiRequest(`/api/v1/teklifler/${encodeURIComponent(teklifId)}/detay`)
    return normalizeOfferDto(data)
  }
  return generateMockOfferDto({ teklif_id: teklifId })
}

export async function updateOfferStatus(teklifId, newDecision) {
  if (!isDemoMode()) {
    const data = await fastApiRequest(`/api/v1/teklifler/${encodeURIComponent(teklifId)}/durum`, {
      method: 'POST',
      body: JSON.stringify({ durum: newDecision }),
    })
    return data
  }
  return { status: 'success', teklif_id: teklifId, durum: newDecision }
}

export async function getOffersList(params = {}) {
  if (!isDemoMode()) {
    const queryParams = new URLSearchParams()
    if (params.bayiKodu || params.bayi_kodu) queryParams.append('bayi_kodu', params.bayiKodu || params.bayi_kodu)
    if (params.durum) queryParams.append('durum', params.durum)
    const queryString = queryParams.toString() ? `?${queryParams.toString()}` : ''

    const data = await fastApiRequest(`/api/v1/teklifler${queryString}`)
    const rawList = Array.isArray(data) ? data : data?.items || data?.teklifler || data?.Teklifler || []
    if (Array.isArray(rawList)) {
      return rawList.map(normalizeOfferSummary)
    }
    return []
  }
  return [
    {
      teklifId: 'TEK-20260826-0001',
      bayiKodu: 'B0001',
      bayiAdi: 'B0001 Bayisi',
      sevkDepolari: 'DP02',
      durum: 'ONAYLANDI',
      genelKarar: 'APPROVED',
      toplamNetTutar: 45200,
      genelMarjPct: 18.4,
      riskSkoru: 15,
      riskSeviyesi: 'DUSUK',
      olusturulmaTarihi: new Date().toISOString(),
      gerekceKodlari: ['MARJ_UYGUN'],
    }
  ]
}

function normalizeOfferSummary(t) {
  const tid = t.teklif_id || t.teklifId || t.id || t.TeklifId || ''
  const bKod = t.bayi_kodu || t.bayiKodu || t.BayiKodu || ''
  const bAdi = t.bayi_adi || t.bayiAdi || t.BayiAdi || (bKod ? `${bKod} Bayisi` : '')
  const marj = t.genel_marj_pct ?? t.genelMarjPct ?? t.bayi_marji ?? t.bayiMarji ?? 0
  const tutar = t.toplam_net_tutar ?? t.toplamNetTutar ?? 0
  const gerekceler = t.gerekce_kodlari || t.gerekceKodlari || []

  return {
    teklifId: tid,
    bayiKodu: bKod,
    bayiAdi: bAdi,
    bayiSehir: t.bayi_sehir || t.bayiSehir || '',
    bayiBolge: t.bayi_bolge || t.bayiBolge || '',
    sevkDepolari: t.sevk_depolari || t.sevkDepolari || t.depo_kodu || t.depoKodu || 'DP01',
    durum: t.durum || t.Durum || 'BEKLIYOR',
    genelKarar: t.karar || t.genel_karar || t.genelKarar || 'NEED_APPROVAL',
    toplamNetTutar: Number(tutar),
    genelMarjPct: Number(marj),
    bayiMarji: Number(t.bayi_marji ?? t.bayiMarji ?? marj),
    hedefTutturmaYuzdesi: Number(t.hedef_tutturma_yuzdesi ?? t.hedefTutturmaYuzdesi ?? 100),
    riskSkoru: Number(t.risk_skoru ?? t.riskSkoru ?? 0),
    riskSeviyesi: t.risk_seviyesi || t.riskSeviyesi || 'ORTA',
    gerekceKodlari: Array.isArray(gerekceler) ? gerekceler : [],
    olusturulmaTarihi: t.olusturulma_tarihi || t.olusturulmaTarihi || new Date().toISOString(),
    aciklama: t.aciklama || '',
    kalemSayisi: t.kalem_sayisi || t.kalemSayisi || (t.kalemler ? t.kalemler.length : 1),
    aiComment: t.ai_comment || t.aiComment || null,
    alertMessage: t.alert_message || t.alertMessage || null,
  }
}

function normalizeOfferDto(data) {
  if (!data) return null
  const kalemlerRaw = data.kalem_sonuclari || data.kalemSonuclari || data.kalemler || data.Kalemler || []
  const kalemlerMapped = kalemlerRaw.map((k) => ({
    siraNo: k.sira_no ?? k.siraNo ?? 1,
    urunKodu: k.urun_kodu || k.urunKodu || '',
    urunAdi: k.urun_adi || k.urunAdi || '',
    depoKodu: k.depo_kodu || k.depoKodu || 'DP01',
    depoAdi: k.depo_adi || k.depoAdi || '',
    miktar: Number(k.miktar ?? 1),
    birimFiyat: Number(k.birim_fiyat ?? k.birimFiyat ?? 0),
    birimMaliyet: Number(k.birim_maliyet ?? k.birimMaliyet ?? 0),
    iskontoOrani: Number(k.iskonto_orani ?? k.iskontoOrani ?? 0),
    netBirimFiyat: Number(k.net_birim_fiyat ?? k.netBirimFiyat ?? (k.birim_fiyat || 0)),
    netTutar: Number(k.net_tutar ?? k.netTutar ?? 0),
    karar: k.karar || 'NEED_APPROVAL',
    hesaplananMarjPct: Number(k.hesaplanan_marj_pct ?? k.hesaplananMarjPct ?? k.marj_orani ?? 0),
    mevcutDepoStok: Number(k.mevcut_depo_stok ?? k.mevcutDepoStok ?? 0),
    kalanStokGunu: k.kalan_stok_gunu ?? k.kalanStokGunu ?? null,
    gerekceKodlari: k.gerekce_kodlari || k.gerekceKodlari || [],
  }))

  const summary = normalizeOfferSummary(data)
  return {
    ...summary,
    kalemler: kalemlerMapped,
    kalemSonuclari: kalemlerMapped,
  }
}

function generateMockOfferDto(payload) {
  const tid = payload.teklif_id || `TEK-${Date.now()}`
  return {
    teklifId: tid,
    bayiKodu: payload.bayi_kodu || 'B0001',
    bayiAdi: 'B0001 Bayisi',
    sevkDepolari: 'DP02',
    durum: 'BEKLIYOR',
    genelKarar: 'NEED_APPROVAL',
    toplamNetTutar: 25000,
    genelMarjPct: 16.5,
    bayiMarji: 16.5,
    hedefTutturmaYuzdesi: 95.0,
    riskSkoru: 20,
    riskSeviyesi: 'DUSUK',
    gerekceKodlari: ['MARJ_UYGUN'],
    aiComment: 'Teklif marjı %16.5 ile şirket standartlarını karşılamaktadır.',
    alertMessage: 'Teklif onaylanmaya uygundur.',
    kalemler: (payload.kalemler || []).map((k, i) => ({
      siraNo: i + 1,
      urunKodu: k.urun_kodu || 'U1001',
      urunAdi: 'Örnek Ürün',
      depoKodu: k.depo_kodu || 'DP02',
      depoAdi: 'Ankara Depo',
      miktar: k.miktar || 1,
      birimFiyat: k.birim_fiyat || 100,
      birimMaliyet: 80,
      iskontoOrani: k.iskonto_orani || 0,
      netBirimFiyat: k.birim_fiyat || 100,
      netTutar: (k.miktar || 1) * (k.birim_fiyat || 100),
      karar: 'APPROVED',
      hesaplananMarjPct: 20.0,
      mevcutDepoStok: 50,
      kalanStokGunu: 15.0,
      gerekceKodlari: ['MARJ_UYGUN'],
    })),
    olusturulmaTarihi: new Date().toISOString(),
  }
}

// ─── DASHBOARD ÖZET & METRİKLER ───
export async function getDealersSummary({ donem, segment, bolge } = {}) {
  if (!isDemoMode()) {
    const params = new URLSearchParams()
    if (donem) params.append('donem', donem)
    if (segment) params.append('segment', segment)
    if (bolge) params.append('bolge', bolge)
    const queryString = params.toString() ? `?${params.toString()}` : ''

    const data = await request(`/api/v1/performance/dashboard-stats${queryString}`)
    if (data) {
      const ciroVal = data.toplamCiro ?? data.toplam_ciro ?? data.ToplamCiro ?? data.toplamGerceklesenCiro ?? data.toplam_gerceklesen_ciro ?? data.ToplamGerceklesenCiro ?? 0
      const avgTarget = data.genelHedefeUlasmaPct ?? data.genel_hedefe_ulasma_pct ?? data.GenelHedefeUlasmaPct ?? data.ortalamaHedefeUlasmaPct ?? data.ortalama_hedefe_ulasma_pct ?? data.ortalamaHedefTutmaOrani ?? data.OrtalamaHedefTutmaOrani
      const avgMargin = data.genelOrtalamaMarjPct ?? data.genel_ortalama_marj_pct ?? data.GenelOrtalamaMarjPct ?? data.ortalamaMarjPct ?? data.ortalama_marj_pct ?? data.ortalamaKarMarji ?? data.OrtalamaKarMarji
      return {
        toplamBayiSayisi: data.toplamBayiSayisi ?? data.toplam_bayi_sayisi ?? data.ToplamBayiSayisi ?? 0,
        toplamCiro: ciroVal,
        toplamGerceklesenCiro: ciroVal,
        genelHedefeUlasmaPct: avgTarget,
        ortalamaHedefeUlasmaPct: avgTarget,
        genelOrtalamaMarjPct: avgMargin,
        ortalamaMarjPct: avgMargin,
        yuksekRiskliBayiSayisi: data.yuksekRiskliBayiSayisi ?? data.yuksek_riskli_bayi_sayisi ?? data.YuksekRiskliBayiSayisi ?? 0,
        ...data,
      }
    }
    throw new Error('Yonetici ozet istatistikleri alinamadi.')
  }
  return {
    toplamBayiSayisi: MOCK_DEALERS.length,
    toplamCiro: 45200000.0,
    genelHedefeUlasmaPct: 0.942,
    ortalamaHedefeUlasmaPct: 0.942,
    genelOrtalamaMarjPct: 0.174,
    ortalamaMarjPct: 0.174,
    yuksekRiskliBayiSayisi: 3,
  }
}

export async function getCityDistribution() {
  if (!isDemoMode()) {
    const data = await request('/bayiler/sehir-dagilimi')
    const rawList = Array.isArray(data)
      ? data
      : data?.sehirler || data?.sehir_listesi || data?.sehirDagilimi || data?.items || []

    if (Array.isArray(rawList) && rawList.length > 0) {
      return rawList.map((item) => ({
        sehir: item.sehir || item.Sehir || item.ad || item.name || '',
        bayiSayisi: item.bayi_sayisi ?? item.bayiSayisi ?? item.BayiSayisi ?? item.sayi ?? item.count ?? 0,
        ciroPayiPct: item.ciro_payi_pct ?? item.ciroPayiPct ?? item.CiroPayiPct ?? 0,
      }))
    }
    return []
  }
  return [
    { sehir: 'İstanbul', bayiSayisi: 8 },
    { sehir: 'İzmir', bayiSayisi: 5 },
    { sehir: 'Ankara', bayiSayisi: 4 },
    { sehir: 'Bursa', bayiSayisi: 3 },
    { sehir: 'Antalya', bayiSayisi: 3 },
    { sehir: 'Trabzon', bayiSayisi: 2 },
  ]
}

export async function getRegionDistribution(donem) {
  if (!isDemoMode()) {
    const params = new URLSearchParams()
    if (donem) params.append('donem', donem)
    const queryString = params.toString() ? `?${params.toString()}` : ''

    const data = await request(`/bayiler/bolge-dagilimi${queryString}`)
    const rawList = Array.isArray(data)
      ? data
      : data?.bolgeler || data?.Bolgeler || data?.bolge_listesi || data?.bolgeDagilimi || data?.items || []

    const totalCiro = rawList.reduce((sum, item) => sum + (item.toplamCiro ?? item.toplam_ciro ?? item.ciro ?? 0), 0)

    if (Array.isArray(rawList)) {
      return rawList.map((item) => {
        const ciro = item.toplamCiro ?? item.toplam_ciro ?? item.ciro ?? 0
        const ciroPayi = item.ciro_payi_pct ?? item.ciroPayiPct ?? (totalCiro > 0 ? ciro / totalCiro : 0)
        const count = item.toplamBayiSayisi ?? item.toplam_bayi_sayisi ?? item.bayiSayisi ?? item.bayi_sayisi ?? item.BayiSayisi ?? 0

        return {
          bolge: item.bolge || item.Bolge || item.ad || item.name || '',
          bayiSayisi: count,
          toplamCiro: ciro,
          ciroPayiPct: ciroPayi,
        }
      })
    }
    return []
  }
  return [
    { bolge: 'Marmara', bayiSayisi: 10, ciroPayiPct: 0.42 },
    { bolge: 'Ege', bayiSayisi: 6, ciroPayiPct: 0.25 },
    { bolge: 'İç Anadolu', bayiSayisi: 5, ciroPayiPct: 0.18 },
    { bolge: 'Akdeniz', bayiSayisi: 4, ciroPayiPct: 0.15 },
  ]
}

export async function getStockMatrix() {
  if (!isDemoMode()) {
    const data = await request('/stok/urun-dagilimi')
    if (Array.isArray(data)) return data
    if (data && Array.isArray(data.items)) return data.items
    return []
  }
  return [
    { depoKodu: 'DP01', depoAdi: 'Merkez Depo', kritikKalemSayisi: 2, toplamStokValue: 12500000 },
    { depoKodu: 'DP02', depoAdi: 'Ege Lojistik', kritikKalemSayisi: 1, toplamStokValue: 8400000 },
  ]
}
