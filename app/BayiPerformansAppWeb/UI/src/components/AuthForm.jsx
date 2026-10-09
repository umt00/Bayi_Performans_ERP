import { useState, useEffect } from 'react'
import { useAuth } from '../context/AuthContext'
import { useTheme } from '../context/ThemeContext'
import { sendOtp, verifyOtp, setDemoMode } from '../api/client'

export function AuthForm() {
  const { login, register } = useAuth()
  const { toggleTheme, isDark } = useTheme()
  const [tab, setTab] = useState('login') // login | register | otp
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [showConfirmPassword, setShowConfirmPassword] = useState(false)
  const [loading, setLoading] = useState(false)
  const [alert, setAlert] = useState(null)

  // 2FA OTP State
  const [otpIslemId, setOtpIslemId] = useState(null)
  const [otpCode, setOtpCode] = useState('')
  const [otpTimer, setOtpTimer] = useState(180)

  // Kurumsal giriş ekranı açıldığında varsayılan olarak canlı moda (Live API) geç
  useEffect(() => {
    setDemoMode(false)
  }, [])

  // OTP Geri Sayım Zamanlayıcısı
  useEffect(() => {
    let interval = null
    if (tab === 'otp' && otpTimer > 0) {
      interval = setInterval(() => setOtpTimer((t) => t - 1), 1000)
    }
    return () => {
      if (interval) clearInterval(interval)
    }
  }, [tab, otpTimer])

  const handleSubmit = async (e) => {
    e.preventDefault()
    setAlert(null)

    if (tab === 'otp') {
      if (!otpCode || otpCode.trim().length < 6) {
        setAlert({ type: 'error', text: 'Lütfen 6 haneli OTP doğrulama kodunu giriniz.' })
        return
      }

      setLoading(true)
      try {
        setDemoMode(false)
        const verifyRes = await verifyOtp({ islemId: otpIslemId, kod: otpCode.trim(), email })
        if (!verifyRes || verifyRes.dogrulandi !== true) {
          throw new Error(verifyRes?.message || 'OTP doğrulaması onaylanmadı (dogrulandi !== true).')
        }
        const dogrulamaToken = verifyRes?.dogrulama_token || verifyRes?.dogrulamaToken || null
        await login(email, password, false, dogrulamaToken)
      } catch (err) {
        let text = err.data?.detail || err.message || 'OTP doğrulama başarısız. Lütfen kodunuzu kontrol ediniz.'
        if (err.status === 503 || text.includes('OTP_EPOSTA_GONDERILEMEDI') || err.data?.code === 'OTP_EPOSTA_GONDERILEMEDI') {
          text = 'OTP e-postası gönderilemedi (503 OTP_EPOSTA_GONDERILEMEDI). Backend sunucusunda SMTP / Google Uygulama Parolası User Secrets ayarlarını kontrol ediniz.'
        }
        setAlert({
          type: 'error',
          text,
        })
      } finally {
        setLoading(false)
      }
      return
    }

    if (!email || !password) {
      setAlert({ type: 'error', text: 'Lütfen e-posta ve parola alanlarını eksiksiz doldurunuz.' })
      return
    }

    if (tab === 'register' && password !== confirmPassword) {
      setAlert({ type: 'error', text: 'Girdiğiniz parolalar birbiriyle eşleşmiyor.' })
      return
    }

    if (password.length < 8) {
      setAlert({ type: 'error', text: 'Parola güvenlik gereği en az 8 karakter olmalıdır.' })
      return
    }

    setLoading(true)
    try {
      if (tab === 'login') {
        // Canlı moda geçip önce e-posta ve şifre kontrolü ile login 1. adımını çağır
        setDemoMode(false)
        const loginRes = await login(email, password, false)
        if (loginRes && loginRes.requireTwoFactor) {
          const receivedIslemId = loginRes.islemId || loginRes.islem_id
          setOtpIslemId(receivedIslemId)
          setOtpTimer(loginRes.gecerlilikSaniyesi || 180)
          setOtpCode('')
          setTab('otp')
          setAlert({
            type: 'success',
            text: `6 haneli OTP doğrulama kodu ${email} adresine gönderildi.`,
          })
        }
      } else {
        setDemoMode(false)
        const res = await register(email, password)
        if (res && (res.emailOnayiGerekli || res.email_onayi_gerekli)) {
          setAlert({
            type: 'success',
            text: 'Kayıt talebiniz alındı. Lütfen e-posta adresinize gönderilen aktivasyon linkini onaylayınız.',
          })
          setTab('login')
        } else {
          setAlert({ type: 'success', text: 'Kayıt tamamlandı. Şimdi giriş yapabilirsiniz.' })
          setTab('login')
        }
      }
    } catch (err) {
      let text = err.data?.detail || err.message || 'İşlem gerçekleştirilemedi. Backend servisinin açık olduğundan emin olunuz.'
      if (err.status === 503 || text.includes('OTP_EPOSTA_GONDERILEMEDI') || err.data?.code === 'OTP_EPOSTA_GONDERILEMEDI') {
        text = 'OTP e-postası gönderilemedi (503 OTP_EPOSTA_GONDERILEMEDI). Backend sunucusunda SMTP / Google Uygulama Parolası User Secrets ayarlarını kontrol ediniz.'
      }
      setAlert({
        type: 'error',
        text,
      })
    } finally {
      setLoading(false)
    }
  }

  const handleResendOtp = async () => {
    if (!email) {
      setAlert({ type: 'error', text: 'E-posta adresi bulunamadı. Lütfen giriş alanlarını doldurunuz.' })
      setTab('login')
      return
    }
    setLoading(true)
    setAlert(null)
    try {
      setDemoMode(false)
      const otpRes = await sendOtp(email)
      const receivedIslemId = otpRes?.islem_id || otpRes?.islemId || null
      setOtpIslemId(receivedIslemId)
      setOtpTimer(180)
      setOtpCode('')
      setAlert({
        type: 'success',
        text: `Yeni 6 haneli OTP doğrulama kodu ${email} adresine tekrar gönderildi.`,
      })
    } catch (err) {
      let text = err.data?.detail || err.message || 'OTP yeniden gönderilemedi.'
      setAlert({ type: 'error', text })
    } finally {
      setLoading(false)
    }
  }

  const handleDemoLogin = async () => {
    setLoading(true)
    setAlert(null)
    try {
      await login('yonetici@bayiperformans.com', 'Demo123!', true)
    } catch (err) {
      setAlert({ type: 'error', text: 'Demo oturumu açılamadı: ' + err.message })
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="auth-page" style={{ position: 'relative' }}>
      <div style={{ position: 'absolute', top: '1.25rem', right: '1.25rem', zIndex: 10 }}>
        <button
          type="button"
          className="theme-toggle-btn"
          onClick={toggleTheme}
          title={isDark ? 'Aydınlık Temaya Geç' : 'Karanlık Temaya Geç'}
        >
          <span>{isDark ? '☀️' : '🌙'}</span>
          <span>{isDark ? 'Aydınlık' : 'Karanlık'}</span>
        </button>
      </div>

      <div className="auth-card animate-fade-in">
        <div className="auth-header">
          <div className="brand-icon">BP</div>
          <h1>Bayi Performans Portalı</h1>
          <p>Yönetici & Karar Destek Girişi</p>
        </div>

        {tab !== 'otp' && (
          <div className="auth-tabs">
            <button
              type="button"
              className={`auth-tab ${tab === 'login' ? 'active' : ''}`}
              onClick={() => {
                setTab('login')
                setAlert(null)
              }}
            >
              Giriş Yap
            </button>
            <button
              type="button"
              className={`auth-tab ${tab === 'register' ? 'active' : ''}`}
              onClick={() => {
                setTab('register')
                setAlert(null)
              }}
            >
              Kayıt Ol
            </button>
          </div>
        )}

        {alert && (
          <div className={`auth-alert ${alert.type}`}>
            <span>{alert.text}</span>
          </div>
        )}

        <form onSubmit={handleSubmit}>
          {tab === 'otp' ? (
            <div className="animate-fade-in">
              <div style={{ textAlign: 'center', marginBottom: '1.25rem' }}>
                <h3 style={{ fontSize: '1.1rem', color: 'var(--text-primary)', marginBottom: '0.3rem' }}>
                  2FA / Redis OTP Doğrulama
                </h3>
                <p style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
                  <strong>{email}</strong> adresine gönderilen 6 haneli doğrulama kodunu giriniz.
                </p>
              </div>

              <div className="form-group">
                <label>6 Haneli OTP Kodu</label>
                <input
                  type="text"
                  maxLength="6"
                  className="form-input"
                  style={{ textAlign: 'center', letterSpacing: '0.4em', fontSize: '1.2rem', fontWeight: 700, fontFamily: 'var(--font-mono)' }}
                  placeholder="123456"
                  value={otpCode}
                  onChange={(e) => setOtpCode(e.target.value)}
                  required
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.75rem', color: 'var(--text-muted)', marginBottom: '1rem' }}>
                <span>İşlem ID: {otpIslemId || '-'}</span>
                <span>⏱️ Kalan Süre: {Math.floor(otpTimer / 60)}:{(otpTimer % 60).toString().padStart(2, '0')}</span>
              </div>

              <button type="submit" className="btn-primary" disabled={loading || otpCode.length < 6}>
                {loading ? 'Doğrulanıyor...' : 'OTP Kodunu Doğrula'}
              </button>

              <div style={{ display: 'flex', gap: '0.5rem', marginTop: '0.5rem' }}>
                <button
                  type="button"
                  className="btn-demo"
                  style={{ flex: 1 }}
                  onClick={handleResendOtp}
                  disabled={loading}
                >
                  🔄 Yeniden Kod Gönder
                </button>
                <button
                  type="button"
                  className="btn-demo"
                  style={{ flex: 1 }}
                  onClick={() => {
                    setTab('login')
                    setAlert(null)
                  }}
                  disabled={loading}
                >
                  ← Giriş Ekranına Dön
                </button>
              </div>
            </div>
          ) : (
            <>
              <div className="form-group">
                <label>Kurumsal E-posta</label>
                <input
                  type="email"
                  className="form-input"
                  placeholder="ornek@bayiperformans.com"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                />
              </div>

              <div className="form-group">
                <label>Parola</label>
                <div style={{ position: 'relative' }}>
                  <input
                    type={showPassword ? 'text' : 'password'}
                    className="form-input"
                    style={{ paddingRight: '2.5rem' }}
                    placeholder="••••••••"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    required
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword(!showPassword)}
                    style={{
                      position: 'absolute',
                      right: '0.75rem',
                      top: '50%',
                      transform: 'translateY(-50%)',
                      background: 'none',
                      border: 'none',
                      color: 'var(--text-muted)',
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      padding: '0.3rem',
                      borderRadius: '4px',
                      transition: 'color 0.2s ease',
                    }}
                    onMouseEnter={(e) => (e.currentTarget.style.color = 'var(--text-primary)')}
                    onMouseLeave={(e) => (e.currentTarget.style.color = 'var(--text-muted)')}
                    title={showPassword ? 'Parolayı Gizle' : 'Parolayı Göster'}
                  >
                    {showPassword ? <EyeOffIcon /> : <EyeIcon />}
                  </button>
                </div>
              </div>

              {tab === 'register' && (
                <div className="form-group">
                  <label>Parola Tekrarı</label>
                  <div style={{ position: 'relative' }}>
                    <input
                      type={showConfirmPassword ? 'text' : 'password'}
                      className="form-input"
                      style={{ paddingRight: '2.5rem' }}
                      placeholder="••••••••"
                      value={confirmPassword}
                      onChange={(e) => setConfirmPassword(e.target.value)}
                      required
                    />
                    <button
                      type="button"
                      onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                      style={{
                        position: 'absolute',
                        right: '0.75rem',
                        top: '50%',
                        transform: 'translateY(-50%)',
                        background: 'none',
                        border: 'none',
                        color: 'var(--text-muted)',
                        cursor: 'pointer',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        padding: '0.3rem',
                        borderRadius: '4px',
                        transition: 'color 0.2s ease',
                      }}
                      onMouseEnter={(e) => (e.currentTarget.style.color = 'var(--text-primary)')}
                      onMouseLeave={(e) => (e.currentTarget.style.color = 'var(--text-muted)')}
                      title={showConfirmPassword ? 'Parolayı Gizle' : 'Parolayı Göster'}
                    >
                      {showConfirmPassword ? <EyeOffIcon /> : <EyeIcon />}
                    </button>
                  </div>
                </div>
              )}

              <button type="submit" className="btn-primary" disabled={loading}>
                {loading ? 'İşleniyor...' : tab === 'login' ? 'Devam Et (OTP Adımı)' : 'Hesap Oluştur'}
              </button>

              <div style={{ margin: '1rem 0', textAlign: 'center', position: 'relative' }}>
                <hr style={{ border: 'none', borderTop: '1px solid var(--border-subtle)' }} />
                <span style={{ position: 'absolute', top: '-10px', left: '50%', transform: 'translateX(-50%)', background: 'var(--bg-surface)', padding: '0 0.5rem', fontSize: '0.72rem', color: 'var(--text-muted)' }}>
                  veya
                </span>
              </div>

              <button
                type="button"
                className="btn-demo"
                onClick={handleDemoLogin}
                disabled={loading}
              >
                🚀 Hızlı Demo Girişi (Örnek Verilerle)
              </button>
            </>
          )}
        </form>
      </div>
    </div>
  )
}

function EyeIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
      <circle cx="12" cy="12" r="3" />
    </svg>
  )
}

function EyeOffIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24" />
      <line x1="1" y1="1" x2="23" y2="23" />
    </svg>
  )
}

export default AuthForm
