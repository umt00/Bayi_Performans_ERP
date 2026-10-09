// OfferEvaluator.jsx - B2B Teklif Yönetim ve Karar Konsolu (3 Panelli Yönetici Mimarisi & Supabase Uyumlu)
import { useState, useEffect, useMemo } from 'react'
import {
  createAndEvaluateOffer,
  getOffersList,
  getOfferDetail,
  reEvaluateOffer,
  updateOfferStatus,
  getDealersList,
  getProductsList,
  getWarehousesList,
  getDefaultWarehouseForDealer,
} from '../api/client'
import ReasonCodes from './ReasonCodes'
import SearchableSelect from './SearchableSelect'

export function OfferEvaluator() {
  const [dealers, setDealers] = useState([])
  const [products, setProducts] = useState([])
  const [warehouses, setWarehouses] = useState([])
  const [pastOffers, setPastOffers] = useState([])

  // Sol Form State
  const [bayiKodu, setBayiKodu] = useState('')
  const [depoKodu, setDepoKodu] = useState('')
  const [autoWarehouseCode, setAutoWarehouseCode] = useState('')
  const [aciklama, setAciklama] = useState('')
  const [items, setItems] = useState([
    { urunKodu: '', miktar: '1', birimFiyat: '', iskontoOrani: '0', depoKodu: '' },
  ])

  // Sağ Panel (Aktif Karar Raporu) State
  const [selectedOffer, setSelectedOffer] = useState(null)
  const [loading, setLoading] = useState(false)
  const [reEvalLoading, setReEvalLoading] = useState(false)
  const [statusUpdating, setStatusUpdating] = useState(false)
  const [detailLoading, setDetailLoading] = useState(false)
  const [error, setError] = useState(null)
  const [actionNotice, setActionNotice] = useState(null)

  // Alt Panel Filtre & Arama State
  const [statusFilter, setStatusFilter] = useState('ALL')
  const [searchQuery, setSearchQuery] = useState('')

  // Katalog ve Geçmiş Teklifleri Yükleme
  useEffect(() => {
    async function loadCatalogAndHistory() {
      const [dRes, pRes, wRes, oRes] = await Promise.allSettled([
        getDealersList(),
        getProductsList(),
        getWarehousesList(),
        getOffersList(),
      ])
      if (dRes.status === 'fulfilled' && dRes.value) setDealers(dRes.value)
      if (pRes.status === 'fulfilled' && pRes.value) setProducts(pRes.value)
      if (wRes.status === 'fulfilled' && wRes.value) setWarehouses(wRes.value)
      if (oRes.status === 'fulfilled' && oRes.value) {
        setPastOffers(oRes.value)
        // Kullanıcı seçmeden veya teklif girmeden otomatik doldurma YAPILMAZ (Madde 5)
      }
    }
    loadCatalogAndHistory()
  }, [])

  const loadOfferDetails = async (teklifId) => {
    if (!teklifId) return
    setDetailLoading(true)
    try {
      const detail = await getOfferDetail(teklifId)
      setSelectedOffer(detail)
    } catch (err) {
      console.error('Teklif detayı yüklenemedi:', err)
    } finally {
      setDetailLoading(false)
    }
  }

  // Bayi Seçildiğinde En Yakın Depoyu Otomatik Belirle
  const handleDealerChange = (selectedCode) => {
    setBayiKodu(selectedCode)
    const targetDealer = dealers.find((d) => d.bayiKodu === selectedCode)
    if (targetDealer) {
      const autoDepo = targetDealer.varsayilanDepoKodu || getDefaultWarehouseForDealer(targetDealer) || 'DP01'
      setAutoWarehouseCode(autoDepo)
      setDepoKodu(autoDepo)
      // Kalemlerdeki depoyu da güncelle
      setItems((prev) => prev.map((item) => ({ ...item, depoKodu: item.depoKodu || autoDepo })))
    }
  }

  const handleAddItem = () => {
    setItems((prev) => [
      ...prev,
      { urunKodu: '', miktar: '1', birimFiyat: '', iskontoOrani: '0', depoKodu: depoKodu || autoWarehouseCode || 'DP01' },
    ])
  }

  const handleRemoveItem = (index) => {
    if (items.length <= 1) return
    setItems((prev) => prev.filter((_, i) => i !== index))
  }

  const handleItemChange = (index, field, value) => {
    setItems((prev) => {
      const copy = [...prev]
      copy[index] = { ...copy[index], [field]: value }
      return copy
    })
  }

  // Sol Panel Anlık Toplam Tutar Önizlemesi
  const totalRevenue = useMemo(() => {
    return items.reduce((sum, item) => {
      const price = parseFloat(item.birimFiyat) || 0
      const qty = parseFloat(item.miktar) || 0
      const disc = (parseFloat(item.iskontoOrani) || 0) / 100
      return sum + price * (1 - disc) * qty
    }, 0)
  }, [items])

  const formatTRY = (val) =>
    new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', maximumFractionDigits: 2 }).format(val || 0)

  // Excel Formatında Dışa Aktarma (Madde 6)
  const handleExportExcel = () => {
    if (!pastOffers || pastOffers.length === 0) {
      alert('Dışa aktarılacak kayıtlı teklif bulunamadı.')
      return
    }

    const headers = [
      'Teklif ID',
      'Oluşturulma Tarihi',
      'Bayi Kodu',
      'Bayi Ünvanı',
      'Sevk Depoları',
      'Toplam Tutar (TL)',
      'Kâr Marjı (%)',
      'Risk Skoru',
      'Karar Durumu',
      'Kalem Sayısı',
      'Gerekçe Kodları',
      'AI Yönetici Notu'
    ]

    const rows = pastOffers.map((o) => [
      `"${o.teklifId || ''}"`,
      `"${o.olusturulmaTarihi ? new Date(o.olusturulmaTarihi).toLocaleString('tr-TR') : ''}"`,
      `"${o.bayiKodu || ''}"`,
      `"${(o.bayiAdi || '').replace(/"/g, '""')}"`,
      `"${o.sevkDepolari || 'DP01'}"`,
      `"${(o.toplamNetTutar || 0).toFixed(2).replace('.', ',')}"`,
      `"${(o.genelMarjPct || 0).toFixed(1).replace('.', ',')}"`,
      `"${o.riskSkoru ?? 0}"`,
      `"${o.durum === 'ONAYLANDI' ? 'Onaylandı' : o.durum === 'REDDEDILDI' ? 'Reddedildi' : 'Onay Bekliyor'}"`,
      `"${o.kalemler ? o.kalemler.length : (o.kalemSayisi || 1)}"`,
      `"${((o.gerekceKodlari || []).join(', ')).replace(/"/g, '""')}"`,
      `"${(o.aiComment || '').replace(/"/g, '""')}"`
    ])

    const csvContent = '\uFEFF' + [headers.join(';'), ...rows.map((r) => r.join(';'))].join('\r\n')
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    const dateStr = new Date().toISOString().split('T')[0]
    link.setAttribute('href', url)
    link.setAttribute('download', `Gecmis_Teklifler_Raporu_${dateStr}.csv`)
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    URL.revokeObjectURL(url)

    setActionNotice({
      type: 'success',
      text: `Geçmiş teklifler listesi (${pastOffers.length} kayıt) Excel formatında başarıyla indirildi.`,
    })
  }

  // Değerlendirme Raporunu Yazdırma (Madde 7)
  const handlePrintReport = () => {
    window.print()
  }

  // TEK ADIMDA: Teklifi Kaydet ve Kural Motoruyla Değerlendir
  const handleSubmitOffer = async (e) => {
    e.preventDefault()
    setLoading(true)
    setError(null)
    setActionNotice(null)

    if (!bayiKodu) {
      setError('Lütfen bir bayi seçiniz.')
      setLoading(false)
      return
    }

    const validItems = items.filter((i) => i.urunKodu && Number(i.miktar) > 0 && Number(i.birimFiyat) >= 0)
    if (validItems.length === 0) {
      setError('Lütfen geçerli ürün, miktar ve birim fiyat girilmiş en az 1 kalem ekleyiniz.')
      setLoading(false)
      return
    }

    try {
      const resultDto = await createAndEvaluateOffer({
        bayiKodu,
        aciklama,
        kalemler: validItems.map((k, i) => ({
          siraNo: i + 1,
          urunKodu: k.urunKodu,
          depoKodu: k.depoKodu || depoKodu || autoWarehouseCode || 'DP01',
          miktar: Number(k.miktar),
          birimFiyat: Number(k.birimFiyat || 0),
          iskontoOrani: parseFloat(k.iskontoOrani) || 0,
        })),
      })

      // Sonucu sağ panelde göster
      setSelectedOffer(resultDto)
      setActionNotice({
        type: resultDto.durum === 'ONAYLANDI' ? 'success' : resultDto.durum === 'REDDEDILDI' ? 'danger' : 'warning',
        text: `Teklif (${resultDto.teklifId}) başarıyla oluşturuldu ve analiz edildi.`,
      })

      // Formu temizle ve alt listeyi güncelle
      setBayiKodu('')
      setDepoKodu('')
      setAutoWarehouseCode('')
      setAciklama('')
      setItems([{ urunKodu: '', miktar: '1', birimFiyat: '', iskontoOrani: '0', depoKodu: '' }])
      const updatedList = await getOffersList()
      setPastOffers(updatedList)
    } catch (err) {
      setError(err.data?.detail || err.message || 'Teklif oluşturulup değerlendirilemedi.')
    } finally {
      setLoading(false)
    }
  }

  // Geçmiş Teklifi Yeniden Analiz Et (Re-evaluate)
  const handleReEvaluate = async (teklifId) => {
    if (!teklifId) return
    setReEvalLoading(true)
    try {
      const freshDto = await reEvaluateOffer(teklifId)
      setSelectedOffer(freshDto)
      setActionNotice({
        type: freshDto.durum === 'ONAYLANDI' ? 'success' : freshDto.durum === 'REDDEDILDI' ? 'danger' : 'warning',
        text: `Teklif (${teklifId}) güncel stok ve kurallara göre yeniden değerlendirildi.`,
      })
      const updatedList = await getOffersList()
      setPastOffers(updatedList)
    } catch (err) {
      setError(err.message || 'Yeniden değerlendirme başarısız oldu.')
    } finally {
      setReEvalLoading(false)
    }
  }

  // Yönetici Onay / Ret Kararı Güncelleme
  const handleUpdateStatus = async (teklifId, newDecision) => {
    if (!teklifId) return
    setStatusUpdating(true)
    try {
      await updateOfferStatus(teklifId, newDecision)
      setActionNotice({
        type: newDecision === 'APPROVE' ? 'success' : 'danger',
        text: `Teklif (${teklifId}) ${newDecision === 'APPROVE' ? 'ONAYLANDI' : 'REDDEDİLDİ'} olarak güncellendi.`,
      })
      await loadOfferDetails(teklifId)
      const updatedList = await getOffersList()
      setPastOffers(updatedList)
    } catch (err) {
      setError('Teklif durumu güncellenemedi: ' + err.message)
    } finally {
      setStatusUpdating(false)
    }
  }

  // Alt Tablo Filtreleme
  const filteredOffers = useMemo(() => {
    let list = [...pastOffers]
    if (statusFilter === 'BEKLIYOR') {
      list = list.filter((o) => o.durum === 'BEKLIYOR' || o.genelKarar === 'NEED_APPROVAL')
    } else if (statusFilter === 'ONAYLANDI') {
      list = list.filter((o) => o.durum === 'ONAYLANDI' || o.genelKarar === 'APPROVED')
    } else if (statusFilter === 'REDDEDILDI') {
      list = list.filter((o) => o.durum === 'REDDEDILDI' || o.genelKarar === 'REJECTED')
    }

    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase()
      list = list.filter((o) =>
        (o.teklifId || '').toLowerCase().includes(q) ||
        (o.bayiKodu || '').toLowerCase().includes(q) ||
        (o.bayiAdi || '').toLowerCase().includes(q)
      )
    }
    return list
  }, [pastOffers, statusFilter, searchQuery])

  const counts = useMemo(() => ({
    all: pastOffers.length,
    bekliyor: pastOffers.filter((o) => o.durum === 'BEKLIYOR' || o.genelKarar === 'NEED_APPROVAL').length,
    onaylandi: pastOffers.filter((o) => o.durum === 'ONAYLANDI' || o.genelKarar === 'APPROVED').length,
    reddedildi: pastOffers.filter((o) => o.durum === 'REDDEDILDI' || o.genelKarar === 'REJECTED').length,
  }), [pastOffers])

  const dealerOptions = useMemo(() => {
    return dealers.map((d) => ({
      value: d.bayiKodu,
      label: d.unvan ? `${d.bayiKodu} — ${d.unvan}` : `${d.bayiKodu} Bayisi`,
      sublabel: `${d.bolge || ''} | ${d.segment || ''}`,
    }))
  }, [dealers])

  const warehouseOptions = useMemo(() => {
    return warehouses.map((w) => ({
      value: w.depoKodu,
      label: `${w.depoKodu} — ${w.depoAdi || w.depoKodu}`,
      sublabel: w.bolge ? `Bölge: ${w.bolge}` : '',
    }))
  }, [warehouses])

  const productOptions = useMemo(() => {
    return products.map((p) => ({
      value: p.urunKodu,
      label: `${p.urunKodu} — ${p.urunAdi || p.urunKodu}`,
      sublabel: p.kategori ? `Kategori: ${p.kategori}` : '',
    }))
  }, [products])

  return (
    <div className="animate-fade-in" style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem', width: '100%' }}>
      {/* ─── ÜST BAŞLIK ─── */}
      <div className="page-header" style={{ marginBottom: 0 }}>
        <div className="page-title">
          <h1>B2B Teklif Yönetim ve Karar Konsolu</h1>
          <p>Deterministik marj/iskonto kural motoru, depo stok uygunluğu ve yapay zeka karar desteği</p>
        </div>
      </div>

      {/* ─── ÜST İKİLİ IZGARA (SOL: FORM | SAĞ: RAPOR) ─── */}
      <div style={{ display: 'grid', gridTemplateColumns: 'minmax(420px, 1.15fr) minmax(480px, 1.45fr)', gap: '1.25rem', alignItems: 'stretch' }}>
        {/* SOL PANEL: YENİ TEKLİF FORMU */}
        <div className="card-section" style={{ margin: 0, padding: '1.35rem', display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1rem' }}>
              <h2 style={{ fontSize: '1.05rem', fontWeight: 700, margin: 0 }}>📝 Yeni B2B Teklif Girişi</h2>
              <span className="segment-badge">Form</span>
            </div>

            {error && <div className="auth-alert error" style={{ marginBottom: '1rem', fontSize: '0.8rem' }}>⚠️ {error}</div>}

            <form onSubmit={handleSubmitOffer}>
              <div style={{ display: 'grid', gridTemplateColumns: '1.2fr 1fr', gap: '0.75rem', marginBottom: '0.85rem' }}>
                <div className="form-group" style={{ margin: 0 }}>
                  <label style={{ fontSize: '0.78rem', fontWeight: 600 }}>* Bayi Seçimi:</label>
                  <SearchableSelect
                    value={bayiKodu}
                    onChange={handleDealerChange}
                    options={dealerOptions}
                    placeholder="Bayi Seçin veya Arayın..."
                  />
                </div>

                <div className="form-group" style={{ margin: 0 }}>
                  <label style={{ fontSize: '0.78rem', fontWeight: 600 }}>
                    Sevk Deposu {autoWarehouseCode && <span style={{ color: 'var(--brand-primary)', fontSize: '0.68rem' }}>⚡ Otomatik</span>}
                  </label>
                  <SearchableSelect
                    value={depoKodu}
                    onChange={(val) => setDepoKodu(val)}
                    options={warehouseOptions}
                    placeholder="Depo Seçin..."
                  />
                </div>
              </div>

              <div className="form-group" style={{ marginBottom: '0.85rem' }}>
                <label style={{ fontSize: '0.78rem', fontWeight: 600 }}>Teklif Açıklaması (Opsiyonel):</label>
                <input
                  type="text"
                  className="form-input"
                  placeholder="Örn: 2026 Q3 Kampanyalı Toplu Alım Teklifi"
                  value={aciklama}
                  onChange={(e) => setAciklama(e.target.value)}
                  style={{ fontSize: '0.8rem', padding: '0.45rem 0.65rem' }}
                />
              </div>

              {/* Ürün Kalemleri */}
              <div style={{ marginBottom: '1rem' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
                  <span style={{ fontSize: '0.82rem', fontWeight: 700 }}>Ürün Kalemleri ({items.length})</span>
                  <button type="button" className="btn-demo" style={{ padding: '0.25rem 0.65rem', fontSize: '0.74rem' }} onClick={handleAddItem}>
                    + Kalem Ekle
                  </button>
                </div>

                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.6rem', maxHeight: '320px', overflowY: 'auto', paddingRight: '0.2rem' }}>
                  {items.map((item, idx) => (
                    <div key={idx} style={{ background: 'var(--bg-input)', border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-sm)', padding: '0.75rem' }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.35rem' }}>
                        <span style={{ fontSize: '0.74rem', fontWeight: 700, color: 'var(--brand-primary)' }}>Kalem #{idx + 1}</span>
                        {items.length > 1 && (
                          <button type="button" onClick={() => handleRemoveItem(idx)} style={{ background: 'none', border: 'none', color: 'var(--state-danger-accent)', cursor: 'pointer', fontSize: '0.75rem', fontWeight: 700 }}>✕ Sil</button>
                        )}
                      </div>

                      <SearchableSelect
                        value={item.urunKodu}
                        onChange={(val) => handleItemChange(idx, 'urunKodu', val)}
                        options={productOptions}
                        placeholder="Ürün Seçin..."
                      />

                      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1.3fr 1fr', gap: '0.5rem', marginTop: '0.45rem' }}>
                        <div>
                          <label style={{ fontSize: '0.68rem', color: 'var(--text-muted)', display: 'block', marginBottom: '0.15rem' }}>Miktar</label>
                          <input type="number" min="1" className="form-input" placeholder="1" value={item.miktar} onChange={(e) => handleItemChange(idx, 'miktar', e.target.value)} required style={{ fontSize: '0.78rem', padding: '0.35rem 0.5rem' }} />
                        </div>
                        <div>
                          <label style={{ fontSize: '0.68rem', color: 'var(--text-muted)', display: 'block', marginBottom: '0.15rem' }}>Birim Fiyat (₺)</label>
                          <input type="number" min="0" step="any" className="form-input" placeholder="0.00 ₺" value={item.birimFiyat} onChange={(e) => handleItemChange(idx, 'birimFiyat', e.target.value)} required style={{ fontSize: '0.78rem', padding: '0.35rem 0.5rem' }} />
                        </div>
                        <div>
                          <label style={{ fontSize: '0.68rem', color: 'var(--text-muted)', display: 'block', marginBottom: '0.15rem' }}>İskonto (%)</label>
                          <input type="number" min="0" max="100" className="form-input" placeholder="%0" value={item.iskontoOrani} onChange={(e) => handleItemChange(idx, 'iskontoOrani', e.target.value)} style={{ fontSize: '0.78rem', padding: '0.35rem 0.5rem' }} />
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              <div style={{ background: 'var(--bg-surface-elevated)', padding: '0.75rem 1rem', borderRadius: 'var(--radius-sm)', display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.85rem', border: '1px solid var(--border-subtle)' }}>
                <span style={{ fontSize: '0.82rem', fontWeight: 600, color: 'var(--text-secondary)' }}>Hesaplanan Toplam Tutar:</span>
                <span style={{ fontSize: '1.25rem', fontWeight: 800, color: 'var(--brand-primary)', fontFamily: 'var(--font-mono)' }}>{formatTRY(totalRevenue)}</span>
              </div>

              <button type="submit" className="btn-primary" style={{ width: '100%', padding: '0.75rem' }} disabled={loading}>
                {loading ? '⏳ Analiz Ediliyor...' : '⚡ Teklifi Oluştur ve Değerlendir'}
              </button>
            </form>
          </div>
        </div>

        {/* SAĞ PANEL: TEKLİF ANALİZİ & AI RAPORU */}
        <div className="card-section printable-report-card" style={{ margin: 0, padding: '1.35rem', display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem', flexWrap: 'wrap', gap: '0.5rem' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <h2 style={{ fontSize: '1.05rem', fontWeight: 700, margin: 0 }}>📊 Teklif Değerlendirme ve AI Karar Raporu</h2>
                {selectedOffer && <span className="segment-badge">{selectedOffer.teklifId}</span>}
              </div>

              {selectedOffer && (
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.45rem' }}>
                  <button
                    type="button"
                    className="action-btn-sm"
                    style={{ padding: '0.25rem 0.6rem', fontSize: '0.74rem' }}
                    onClick={handlePrintReport}
                    title="Bu değerlendirme raporunu resmi belge formatında yazdır / PDF kaydet"
                  >
                    🖨️ Raporu Yazdır / PDF
                  </button>
                  <button
                    type="button"
                    className="btn-demo"
                    style={{ padding: '0.25rem 0.65rem', fontSize: '0.72rem', margin: 0 }}
                    onClick={() => handleReEvaluate(selectedOffer.teklifId)}
                    disabled={reEvalLoading}
                  >
                    {reEvalLoading ? '...' : '🔄 Yeniden Analiz Et'}
                  </button>
                </div>
              )}
            </div>

            {actionNotice && <div className={`auth-alert ${actionNotice.type}`} style={{ marginBottom: '0.85rem', fontSize: '0.8rem' }}>{actionNotice.text}</div>}

            {detailLoading ? (
              <div style={{ textAlign: 'center', padding: '3.5rem', color: 'var(--text-muted)' }}>
                <div style={{ fontSize: '1.6rem', marginBottom: '0.4rem' }}>⏳</div>
                <span>Teklif analitiği ve kural motoru kararları yükleniyor...</span>
              </div>
            ) : selectedOffer ? (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '0.85rem' }}>
                {/* Karar Rozeti */}
                <div style={{
                  borderRadius: 'var(--radius-sm)',
                  padding: '0.85rem 1.1rem',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  backgroundColor: selectedOffer.durum === 'ONAYLANDI' ? 'var(--state-success-bg)' : selectedOffer.durum === 'REDDEDILDI' ? 'var(--state-danger-bg)' : 'var(--state-warning-bg)',
                  border: `1px solid ${selectedOffer.durum === 'ONAYLANDI' ? 'var(--state-success-border)' : selectedOffer.durum === 'REDDEDILDI' ? 'var(--state-danger-border)' : 'var(--state-warning-border)'}`,
                }}>
                  <div>
                    <div style={{ fontSize: '0.7rem', fontWeight: 700, textTransform: 'uppercase', color: selectedOffer.durum === 'ONAYLANDI' ? 'var(--state-success-text)' : selectedOffer.durum === 'REDDEDILDI' ? 'var(--state-danger-text)' : 'var(--state-warning-text)' }}>
                      SİSTEM KURAL MOTORU KARARI ({selectedOffer.durum || 'BEKLIYOR'})
                    </div>
                    <div style={{ fontSize: '1.25rem', fontWeight: 800, marginTop: '0.15rem', color: selectedOffer.durum === 'ONAYLANDI' ? 'var(--state-success-accent)' : selectedOffer.durum === 'REDDEDILDI' ? 'var(--state-danger-accent)' : 'var(--state-warning-accent)' }}>
                      {selectedOffer.durum === 'ONAYLANDI' ? '🟢 ONAYLANDI' : selectedOffer.durum === 'REDDEDILDI' ? '🔴 REDDEDİLDİ' : '🟡 ONAY BEKLİYOR'}
                    </div>
                  </div>

                  <div style={{ textAlign: 'right' }}>
                    <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>Ağırlıklı Risk Skoru</div>
                    <div style={{ fontSize: '1.4rem', fontWeight: 800, fontFamily: 'var(--font-mono)' }}>
                      {selectedOffer.riskSkoru ?? 0} <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>/ 100</span>
                    </div>
                  </div>
                </div>

                {/* Gerekçe Kodları */}
                <div>
                  <div style={{ fontSize: '0.74rem', color: 'var(--text-secondary)', fontWeight: 600, marginBottom: '0.35rem' }}>
                    Tespit Edilen Gerekçe ve Risk Kodları:
                  </div>
                  <ReasonCodes codes={selectedOffer.gerekceKodlari} max={99} />
                </div>

                {/* AI Yönetici Özeti */}
                {selectedOffer.aiComment && (
                  <div style={{ background: 'var(--bg-surface-elevated)', borderLeft: '4px solid var(--brand-primary)', padding: '0.85rem 1rem', borderRadius: 'var(--radius-sm)' }}>
                    <div style={{ fontSize: '0.76rem', fontWeight: 700, color: 'var(--brand-primary)', marginBottom: '0.25rem' }}>🤖 Yapay Zeka Yönetici Değerlendirme Notu</div>
                    <p style={{ margin: 0, fontSize: '0.82rem', color: 'var(--text-primary)', lineHeight: 1.5 }}>“{selectedOffer.aiComment}”</p>
                  </div>
                )}

                {/* 4 Finansal KPI Kartı */}
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '0.5rem' }}>
                  <div style={{ background: 'var(--bg-input)', padding: '0.65rem 0.75rem', borderRadius: 'var(--radius-sm)', border: '1px solid var(--border-subtle)', textAlign: 'center' }}>
                    <div style={{ fontSize: '0.68rem', color: 'var(--text-muted)', fontWeight: 600 }}>Teklif Kâr Marjı</div>
                    <div style={{ fontSize: '1.1rem', fontWeight: 800, marginTop: '0.15rem', color: (selectedOffer.genelMarjPct || 0) >= 15 ? 'var(--state-success-accent)' : 'var(--state-danger-accent)' }}>
                      %{Number(selectedOffer.genelMarjPct || 0).toFixed(1)}
                    </div>
                  </div>

                  <div style={{ background: 'var(--bg-input)', padding: '0.65rem 0.75rem', borderRadius: 'var(--radius-sm)', border: '1px solid var(--border-subtle)', textAlign: 'center' }}>
                    <div style={{ fontSize: '0.68rem', color: 'var(--text-muted)', fontWeight: 600 }}>Bayi Dönem Marjı</div>
                    <div style={{ fontSize: '1.1rem', fontWeight: 800, marginTop: '0.15rem' }}>
                      %{Number(selectedOffer.bayiMarji || 0).toFixed(1)}
                    </div>
                  </div>

                  <div style={{ background: 'var(--bg-input)', padding: '0.65rem 0.75rem', borderRadius: 'var(--radius-sm)', border: '1px solid var(--border-subtle)', textAlign: 'center' }}>
                    <div style={{ fontSize: '0.68rem', color: 'var(--text-muted)', fontWeight: 600 }}>Hedef Tutturma</div>
                    <div style={{ fontSize: '1.1rem', fontWeight: 800, marginTop: '0.15rem', color: (selectedOffer.hedefTutturmaYuzdesi || 100) >= 80 ? 'var(--state-success-accent)' : 'var(--state-warning-accent)' }}>
                      %{Number(selectedOffer.hedefTutturmaYuzdesi || 100).toFixed(1)}
                    </div>
                  </div>

                  <div style={{ background: 'var(--bg-input)', padding: '0.65rem 0.75rem', borderRadius: 'var(--radius-sm)', border: '1px solid var(--border-subtle)', textAlign: 'center' }}>
                    <div style={{ fontSize: '0.68rem', color: 'var(--text-muted)', fontWeight: 600 }}>Toplam Tutar</div>
                    <div style={{ fontSize: '0.95rem', fontWeight: 800, marginTop: '0.15rem', color: 'var(--brand-primary)' }}>
                      {formatTRY(selectedOffer.toplamNetTutar)}
                    </div>
                  </div>
                </div>

                {/* Kalem Bazlı Karar Detayı */}
                {selectedOffer.kalemler && selectedOffer.kalemler.length > 0 && (
                  <div style={{ marginTop: '0.25rem' }}>
                    <div style={{ fontSize: '0.74rem', fontWeight: 700, color: 'var(--text-secondary)', marginBottom: '0.4rem' }}>
                      Kalem Bazlı Kural Denetim Sonuçları ({selectedOffer.kalemler.length} Kalem):
                    </div>
                    <div className="table-responsive" style={{ maxHeight: '200px', overflowY: 'auto' }}>
                      <table className="data-table" style={{ width: '100%', minWidth: '460px', fontSize: '0.74rem' }}>
                        <thead>
                          <tr>
                            <th style={{ width: '30%', padding: '0.45rem 0.6rem' }}>Ürün</th>
                            <th style={{ width: '12%', padding: '0.45rem 0.5rem' }}>Depo</th>
                            <th style={{ width: '14%', padding: '0.45rem 0.5rem' }}>Miktar</th>
                            <th style={{ width: '16%', padding: '0.45rem 0.5rem' }}>Net Fiyat</th>
                            <th style={{ width: '14%', padding: '0.45rem 0.5rem' }}>Marj %</th>
                            <th style={{ width: '14%', padding: '0.45rem 0.5rem' }}>Karar</th>
                          </tr>
                        </thead>
                        <tbody>
                          {selectedOffer.kalemler.map((k, i) => (
                            <tr key={i}>
                              <td style={{ padding: '0.45rem 0.6rem', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                <strong>{k.urunKodu}</strong> {k.urunAdi ? `— ${k.urunAdi}` : ''}
                              </td>
                              <td style={{ padding: '0.45rem 0.5rem' }}>{k.depoKodu}</td>
                              <td style={{ padding: '0.45rem 0.5rem', fontWeight: 600 }}>{k.miktar} Adet</td>
                              <td style={{ padding: '0.45rem 0.5rem' }}>{formatTRY(k.netBirimFiyat || k.birimFiyat)}</td>
                              <td style={{ padding: '0.45rem 0.5rem', fontWeight: 700, color: (k.hesaplananMarjPct || 0) >= 15 ? 'var(--state-success-accent)' : 'var(--state-danger-accent)' }}>
                                %{Number(k.hesaplananMarjPct || 0).toFixed(1)}
                              </td>
                              <td style={{ padding: '0.45rem 0.5rem' }}>
                                <span
                                  style={{
                                    fontSize: '0.68rem',
                                    fontWeight: 700,
                                    padding: '0.12rem 0.4rem',
                                    borderRadius: 'var(--radius-sm)',
                                    background: k.karar === 'APPROVED' ? 'var(--state-success-bg)' : k.karar === 'REJECTED' ? 'var(--state-danger-bg)' : 'var(--state-warning-bg)',
                                    color: k.karar === 'APPROVED' ? 'var(--state-success-text)' : k.karar === 'REJECTED' ? 'var(--state-danger-text)' : 'var(--state-warning-text)',
                                  }}
                                >
                                  {k.karar === 'APPROVED' ? 'ONAY' : k.karar === 'REJECTED' ? 'RET' : 'BEKLE'}
                                </span>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                )}

                {/* Yönetici Hızlı Aksiyon Butonları */}
                <div style={{ display: 'flex', gap: '0.6rem', marginTop: '0.5rem' }}>
                  <button
                    type="button"
                    className="btn-primary"
                    style={{ flex: 1, padding: '0.65rem', fontSize: '0.82rem', backgroundColor: 'var(--state-success-accent)' }}
                    onClick={() => handleUpdateStatus(selectedOffer.teklifId, 'APPROVE')}
                    disabled={statusUpdating || selectedOffer.durum === 'ONAYLANDI'}
                  >
                    ✓ Teklifi Onayla (APPROVE)
                  </button>

                  <button
                    type="button"
                    className="btn-secondary"
                    style={{ flex: 1, padding: '0.65rem', fontSize: '0.82rem', color: 'var(--state-danger-accent)', borderColor: 'var(--state-danger-border)' }}
                    onClick={() => handleUpdateStatus(selectedOffer.teklifId, 'REJECT')}
                    disabled={statusUpdating || selectedOffer.durum === 'REDDEDILDI'}
                  >
                    ✕ Teklifi Reddet (REJECT)
                  </button>
                </div>
              </div>
            ) : (
              /* İLK AÇILIŞ BOŞ DURUM (Madde 5) */
              <div style={{ textAlign: 'center', padding: '3.5rem 1.5rem', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center' }}>
                <div style={{
                  width: '56px',
                  height: '56px',
                  borderRadius: '50%',
                  background: 'var(--brand-primary-subtle)',
                  color: 'var(--brand-primary)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: '1.75rem',
                  marginBottom: '1rem',
                }}>
                  📋
                </div>
                <h3 style={{ fontSize: '1.15rem', fontWeight: 700, color: 'var(--text-primary)', marginBottom: '0.4rem' }}>
                  Değerlendirme Raporu Hazır
                </h3>
                <p style={{ fontSize: '0.86rem', color: 'var(--text-secondary)', maxWidth: '440px', lineHeight: 1.5, marginBottom: '1.5rem' }}>
                  Sol panelden yeni bir teklif oluşturup analiz başlatabilir veya aşağıdaki listeden geçmiş bir teklife tıklayarak sistemin kural motoru kararlarını ve yapay zeka analiz raporunu görüntüleyebilirsiniz.
                </p>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '0.55rem', width: '100%', maxWidth: '380px', textAlign: 'left' }}>
                  <div style={{ background: 'var(--bg-input)', border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-sm)', padding: '0.6rem 0.85rem', display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
                    <span style={{ fontSize: '1rem' }}>🎯</span>
                    <div>
                      <div style={{ fontSize: '0.78rem', fontWeight: 700, color: 'var(--text-primary)' }}>Deterministik Marj Denetimi</div>
                      <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>%15 referans eşik ve bayi geçmiş marj uyumu</div>
                    </div>
                  </div>

                  <div style={{ background: 'var(--bg-input)', border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-sm)', padding: '0.6rem 0.85rem', display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
                    <span style={{ fontSize: '1rem' }}>📦</span>
                    <div>
                      <div style={{ fontSize: '0.78rem', fontWeight: 700, color: 'var(--text-primary)' }}>Dinamik Depo Stok Kontrolü</div>
                      <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>Son 90 günlük satış hızı bazlı stok tahsisi</div>
                    </div>
                  </div>

                  <div style={{ background: 'var(--bg-input)', border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-sm)', padding: '0.6rem 0.85rem', display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
                    <span style={{ fontSize: '1rem' }}>🤖</span>
                    <div>
                      <div style={{ fontSize: '0.78rem', fontWeight: 700, color: 'var(--text-primary)' }}>Yapay Zeka Karar Özeti</div>
                      <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>Yönetici için otomatik oluşturulan aksiyon notu</div>
                    </div>
                  </div>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* ─── ALT PANEL: GEÇMİŞ TEKLİFLER TABLOSU (KURUMSAL GENİŞ VE ESNEK TASARIM) ─── */}
      <div className="card-section" style={{ margin: 0, padding: '1.35rem', overflow: 'hidden' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.75rem', marginBottom: '0.85rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <h2 style={{ fontSize: '1.05rem', fontWeight: 700, margin: 0 }}>🗂️ Geçmiş Teklifler ve Karar Denetim Günlüğü</h2>
            <span className="segment-badge">{counts.all} Kayıt</span>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem', flexWrap: 'wrap' }}>
            {/* Excel Dışa Aktarma Butonu (Madde 6) */}
            <button
              type="button"
              className="action-btn-sm"
              style={{
                padding: '0.42rem 0.75rem',
                fontSize: '0.78rem',
                display: 'flex',
                alignItems: 'center',
                gap: '0.4rem',
                background: 'var(--brand-primary)',
                color: '#ffffff',
                border: 'none',
                fontWeight: 600,
              }}
              onClick={handleExportExcel}
              title="Geçmiş teklifleri Excel CSV formatında indir"
            >
              <span>📊</span>
              <span>Excel Formatında İndir</span>
            </button>

            <div className="search-input-wrapper" style={{ minWidth: '220px' }}>
              <span className="search-icon">🔍</span>
              <input
                type="text"
                className="search-input"
                placeholder="Bayi / Teklif ID Ara..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
              />
            </div>
          </div>
        </div>

        {/* Hızlı Segmented Filtre Butonları (Madde 8) */}
        <div className="segmented-filter-bar" style={{ display: 'flex', gap: '0.45rem', flexWrap: 'wrap', marginBottom: '1rem' }}>
          <button
            type="button"
            className={`segmented-filter-btn ${statusFilter === 'ALL' ? 'active' : ''}`}
            onClick={() => setStatusFilter('ALL')}
          >
            <span>📋 Tüm Teklifler</span>
            <span className="count-badge">{counts.all}</span>
          </button>

          <button
            type="button"
            className={`segmented-filter-btn warning ${statusFilter === 'BEKLIYOR' ? 'active' : ''}`}
            onClick={() => setStatusFilter('BEKLIYOR')}
          >
            <span>🟡 Onay Bekleyenler</span>
            <span className="count-badge">{counts.bekliyor}</span>
          </button>

          <button
            type="button"
            className={`segmented-filter-btn success ${statusFilter === 'ONAYLANDI' ? 'active' : ''}`}
            onClick={() => setStatusFilter('ONAYLANDI')}
          >
            <span>🟢 Onaylananlar</span>
            <span className="count-badge">{counts.onaylandi}</span>
          </button>

          <button
            type="button"
            className={`segmented-filter-btn danger ${statusFilter === 'REDDEDILDI' ? 'active' : ''}`}
            onClick={() => setStatusFilter('REDDEDILDI')}
          >
            <span>🔴 Reddedilenler</span>
            <span className="count-badge">{counts.reddedildi}</span>
          </button>
        </div>

        <div className="table-responsive" style={{ maxHeight: '420px', overflowY: 'auto' }}>
          <table className="data-table" style={{ minWidth: '1020px' }}>
            <thead>
              <tr>
                <th style={{ width: '15%', minWidth: '130px', padding: '0.65rem 0.85rem', textAlign: 'left' }}>Teklif ID</th>
                <th style={{ width: '14%', minWidth: '120px', padding: '0.65rem 0.75rem', textAlign: 'left' }}>Tarih</th>
                <th style={{ width: '23%', minWidth: '190px', padding: '0.65rem 0.75rem', textAlign: 'left' }}>Bayi Ünvanı</th>
                <th style={{ width: '9%', minWidth: '85px', padding: '0.65rem 0.75rem', textAlign: 'left' }}>Depo</th>
                <th style={{ width: '13%', minWidth: '110px', padding: '0.65rem 0.75rem', textAlign: 'left' }}>Tutar (₺)</th>
                <th style={{ width: '9%', minWidth: '85px', padding: '0.65rem 0.75rem', textAlign: 'left' }}>Marj %</th>
                <th style={{ width: '8%', minWidth: '75px', padding: '0.65rem 0.75rem', textAlign: 'left' }}>Risk</th>
                <th style={{ width: '9%', minWidth: '85px', padding: '0.65rem 0.75rem', textAlign: 'left' }}>Durum</th>
                <th style={{ width: '6%', minWidth: '70px', padding: '0.65rem 0.85rem', textAlign: 'right' }}>İşlem</th>
              </tr>
            </thead>
            <tbody>
              {filteredOffers.length === 0 ? (
                <tr>
                  <td colSpan="9" style={{ textAlign: 'center', padding: '2.5rem', color: 'var(--text-muted)' }}>
                    Kayıtlı teklif bulunamadı.
                  </td>
                </tr>
              ) : (
                filteredOffers.map((o) => {
                  const isSelected = selectedOffer?.teklifId === o.teklifId
                  return (
                    <tr
                      key={o.teklifId}
                      onClick={() => loadOfferDetails(o.teklifId)}
                      style={{
                        cursor: 'pointer',
                        background: isSelected ? 'var(--bg-surface-hover)' : undefined,
                        borderLeft: isSelected ? '3px solid var(--brand-primary)' : '3px solid transparent',
                      }}
                    >
                      <td className="dealer-code-cell" style={{ padding: '0.6rem 0.85rem', fontWeight: 700, fontFamily: 'var(--font-mono)' }}>
                        {o.teklifId}
                      </td>
                      <td style={{ padding: '0.6rem 0.75rem', color: 'var(--text-muted)', fontSize: '0.74rem' }}>
                        {o.olusturulmaTarihi ? new Date(o.olusturulmaTarihi).toLocaleDateString('tr-TR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '-'}
                      </td>
                      <td style={{ padding: '0.6rem 0.75rem' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', flexWrap: 'wrap' }}>
                          <span style={{ fontWeight: 700, fontFamily: 'var(--font-mono)' }}>{o.bayiKodu}</span>
                          {o.bayiAdi && o.bayiAdi !== o.bayiKodu && (
                            <span style={{ fontSize: '0.74rem', color: 'var(--text-muted)' }}>
                              — {o.bayiAdi}
                            </span>
                          )}
                        </div>
                      </td>
                      <td style={{ padding: '0.6rem 0.75rem' }}>
                        <span className="segment-badge" style={{ fontSize: '0.72rem' }}>{o.sevkDepolari || 'DP01'}</span>
                      </td>
                      <td style={{ padding: '0.6rem 0.75rem', fontWeight: 700, fontFamily: 'var(--font-mono)' }}>
                        {formatTRY(o.toplamNetTutar)}
                      </td>
                      <td style={{ padding: '0.6rem 0.75rem', fontWeight: 700, color: (o.genelMarjPct || 0) >= 15 ? 'var(--state-success-accent)' : 'var(--state-danger-accent)' }}>
                        %{Number(o.genelMarjPct || 0).toFixed(1)}
                      </td>
                      <td style={{ padding: '0.6rem 0.75rem' }}>
                        <span style={{ fontWeight: 700, fontFamily: 'var(--font-mono)', fontSize: '0.76rem' }}>{o.riskSkoru ?? 0}</span>
                      </td>
                      <td style={{ padding: '0.6rem 0.75rem' }}>
                        <span
                          className={`metric-badge ${o.durum === 'ONAYLANDI' ? 'success' : o.durum === 'REDDEDILDI' ? 'danger' : 'warning'}`}
                          style={{ fontSize: '0.68rem', padding: '0.15rem 0.45rem' }}
                        >
                          {o.durum === 'ONAYLANDI' ? 'Onaylandı' : o.durum === 'REDDEDILDI' ? 'Reddedildi' : 'Bekliyor'}
                        </span>
                      </td>
                      <td style={{ padding: '0.6rem 0.85rem', textAlign: 'right' }}>
                        <button
                          type="button"
                          className="action-btn-sm"
                          style={{ padding: '0.22rem 0.5rem', fontSize: '0.72rem' }}
                          onClick={(e) => {
                            e.stopPropagation()
                            loadOfferDetails(o.teklifId)
                          }}
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
    </div>
  )
}
export default OfferEvaluator
