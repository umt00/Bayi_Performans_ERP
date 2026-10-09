// Sidebar.jsx - Kurumsal Sol Navigasyon Menüsü
import { useAuth } from '../context/AuthContext'

export function Sidebar({ activePage, setActivePage }) {
  const { user, logout, isDemo, toggleDemoMode } = useAuth()

  const navGroups = [
    {
      title: 'GENEL BAKIŞ',
      items: [
        { id: 'dashboard', label: 'Yönetici Dashboard', icon: '📊', desc: 'Genel KPI ve bayi risk tablosu' },
      ],
    },
    {
      title: 'ANALİTİK & YÖNETİM',
      items: [
        { id: 'dealer', label: 'Bayi & Ürün Analizi', icon: '🏢', desc: 'Performans, hedef ve dinamik stok' },
      ],
    },
    {
      title: 'OPERASYONEL KARARLAR',
      items: [
        { id: 'offer', label: 'Teklif Yönetimi & Karar', icon: '💼', desc: 'Kural motoru ve AI karar desteği' },
      ],
    },
  ]

  return (
    <aside className="corporate-sidebar">
      {/* Kurumsal Başlık & Logo */}
      <div className="sidebar-brand">
        <div className="brand-badge">BP</div>
        <div className="brand-info">
          <span className="brand-name">BAYİ PERFORMANS</span>
          <span className="brand-tag">Karar Destek Portalı</span>
        </div>
      </div>

      {/* Menü Grupları */}
      <div className="sidebar-nav">
        {navGroups.map((group, gIdx) => (
          <div key={gIdx} className="nav-group">
            <div className="nav-group-title">{group.title}</div>
            <div className="nav-group-items">
              {group.items.map((item) => {
                const isActive = activePage === item.id
                return (
                  <button
                    key={item.id}
                    type="button"
                    className={`sidebar-link ${isActive ? 'active' : ''}`}
                    onClick={() => setActivePage(item.id)}
                  >
                    <span className="sidebar-link-icon">{item.icon}</span>
                    <div className="sidebar-link-text">
                      <span className="sidebar-link-title">{item.label}</span>
                    </div>
                  </button>
                )
              })}
            </div>
          </div>
        ))}
      </div>

      {/* Alt Profil & Bağlantı Durumu */}
      <div className="sidebar-footer">
        <div className="system-status-card">
          <div className="status-indicator">
            <span className="status-dot"></span>
            <span className="status-text">{isDemo ? 'Demo Modu' : 'Canlı Backend API'}</span>
          </div>
          <button
            type="button"
            className="status-toggle-btn"
            onClick={() => toggleDemoMode(!isDemo)}
            title="Canlı API / Demo Modu Geçişi"
          >
            {isDemo ? 'Canlıya Geç' : 'Demo Aç'}
          </button>
        </div>

        {user && (
          <div className="sidebar-user-card">
            <div className="sidebar-user-avatar">
              {user.email ? user.email.charAt(0).toUpperCase() : 'Y'}
            </div>
            <div className="sidebar-user-info">
              <span className="sidebar-user-name">{user.email ? user.email.split('@')[0] : 'Yönetici'}</span>
              <span className="sidebar-user-email" title={user.email}>{user.email || 'Admin'}</span>
            </div>
            <button
              type="button"
              className="sidebar-logout-btn"
              onClick={logout}
              title="Güvenli Çıkış"
            >
              ➔
            </button>
          </div>
        )}
      </div>
    </aside>
  )
}
export default Sidebar
