// App.jsx - Kurumsal Yönetici Portalı (2024-01 ile 2025-06 Varsayılan Dönem)
import { useState, useEffect } from 'react'
import { AuthProvider, useAuth } from './context/AuthContext'
import { ThemeProvider } from './context/ThemeContext'
import Sidebar from './components/Sidebar'
import Header from './components/Header'
import AuthForm from './components/AuthForm'
import Dashboard from './components/Dashboard'
import DealerAnalysis from './components/DealerAnalysis'
import OfferEvaluator from './components/OfferEvaluator'
import './App.css'

function AppContent() {
  const { isAuthenticated, loading } = useAuth()

  // Sayfa yenilendiğinde bulunulan sayfada kalınması için initial state
  const getInitialPage = () => {
    const hash = window.location.hash.replace('#', '').trim()
    const validPages = ['dashboard', 'dealer', 'offer']
    if (hash && validPages.includes(hash)) {
      return hash
    }
    const saved = localStorage.getItem('bp_active_page')
    if (saved && validPages.includes(saved)) {
      return saved
    }
    return 'dashboard'
  }

  const [activePage, setActivePageState] = useState(getInitialPage)

  const setActivePage = (page) => {
    localStorage.setItem('bp_active_page', page)
    window.location.hash = page
    setActivePageState(page)
  }

  const [selectedDealerCode, setSelectedDealerCodeState] = useState('')

  const setSelectedDealerCode = (code) => {
    setSelectedDealerCodeState(code || '')
  }

  const [selectedPeriod, setSelectedPeriod] = useState('2026-08')

  useEffect(() => {
    const handleHashChange = () => {
      const hash = window.location.hash.replace('#', '').trim()
      const validPages = ['dashboard', 'dealer', 'offer']
      if (hash && validPages.includes(hash)) {
        setActivePageState(hash)
        localStorage.setItem('bp_active_page', hash)
      }
    }
    window.addEventListener('hashchange', handleHashChange)
    return () => window.removeEventListener('hashchange', handleHashChange)
  }, [])

  if (loading) {
    return (
      <div className="auth-page">
        <div style={{ textAlign: 'center', color: 'var(--text-muted)' }}>
          <div className="brand-icon" style={{ margin: '0 auto 1rem', width: '48px', height: '48px', fontSize: '1.2rem', backgroundColor: 'var(--brand-primary)' }}>
            BP
          </div>
          <h2>Bayi Performans Portalı</h2>
          <p style={{ marginTop: '0.4rem', fontSize: '0.85rem' }}>Oturum durumu kontrol ediliyor...</p>
        </div>
      </div>
    )
  }

  if (!isAuthenticated) {
    return <AuthForm />
  }

  const handleSelectDealerFromDashboard = (code) => {
    setSelectedDealerCode(code)
    setActivePage('dealer')
  }

  return (
    <div className="corporate-layout">
      <Sidebar activePage={activePage} setActivePage={setActivePage} />

      <div className="corporate-main">
        <Header activePage={activePage} selectedPeriod={selectedPeriod} />

        <main className="corporate-content">
          {activePage === 'dashboard' && (
            <Dashboard
              onSelectDealer={handleSelectDealerFromDashboard}
              selectedPeriod={selectedPeriod}
              onPeriodChange={setSelectedPeriod}
            />
          )}

          {activePage === 'dealer' && (
            <DealerAnalysis
              selectedDealerCode={selectedDealerCode}
              onSelectDealer={(code) => setSelectedDealerCode(code)}
              selectedPeriod={selectedPeriod}
              onPeriodChange={setSelectedPeriod}
            />
          )}

          {activePage === 'offer' && <OfferEvaluator />}
        </main>

        <footer className="corporate-footer">
          <div className="footer-left">
            <span>© 2026 Bayi Performans ve Karar Destek Sistemi</span>
            <span className="footer-sep">•</span>
            <span>Tüm Hakları Saklıdır</span>
          </div>
          <div className="footer-right">
            <span className="footer-version">v1.4.0 Enterprise</span>
          </div>
        </footer>
      </div>
    </div>
  )
}

export function App() {
  return (
    <ThemeProvider>
      <AuthProvider>
        <AppContent />
      </AuthProvider>
    </ThemeProvider>
  )
}

export default App
