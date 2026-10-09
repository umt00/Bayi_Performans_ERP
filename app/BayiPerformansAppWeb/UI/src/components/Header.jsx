// Header.jsx - Kurumsal Üst Başlık Barı & Dinamik Dönem Gösterimi
import { useTheme } from '../context/ThemeContext'

export function Header({ activePage, selectedPeriod }) {
  const { toggleTheme, isDark } = useTheme()

  const pageTitles = {
    dashboard: { title: 'Yönetici Özet ve Risk Analiz Portalı', category: 'Genel Bakış' },
    dealer: { title: 'Bayi Performans ve Stok Analiz Merkezi', category: 'Analitik & Yönetim' },
    offer: { title: 'B2B Teklif Yönetim ve Karar Konsolu', category: 'Operasyonel Kararlar' },
  }

  const current = pageTitles[activePage] || { title: 'Yönetici Portalı', category: 'Portal' }

  const todayStr = new Intl.DateTimeFormat('tr-TR', {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  }).format(new Date())

  return (
    <header className="corporate-header">
      <div className="header-left">
        <div className="breadcrumb">
          <span className="breadcrumb-root">Portal</span>
          <span className="breadcrumb-sep">/</span>
          <span className="breadcrumb-category">{current.category}</span>
          <span className="breadcrumb-sep">/</span>
          <span className="breadcrumb-current">{current.title}</span>
        </div>
      </div>

      <div className="header-right">
        {/* Tema Değiştirme Butonu */}
        <button
          type="button"
          className="theme-toggle-btn"
          onClick={toggleTheme}
          title={isDark ? 'Aydınlık Temaya Geç' : 'Karanlık Temaya Geç'}
        >
          <span>{isDark ? '☀️' : '🌙'}</span>
          <span>{isDark ? 'Aydınlık' : 'Karanlık'}</span>
        </button>

        {/* Dinamik Canlı Dönem Rozeti */}
        <div className="header-badge">
          <span className="badge-label">Dönem:</span>
          <span className="badge-val">{selectedPeriod || '2025-08'}</span>
        </div>

        <div className="header-date">
          <span>📅 {todayStr}</span>
        </div>
      </div>
    </header>
  )
}
export default Header
