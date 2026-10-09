// SearchableSelect.jsx - Hem Seçilebilir hem Yazılabilir Arama Kutusu (Mükemmel Dropdown Davranışı)
import { useState, useRef, useEffect } from 'react'

export function SearchableSelect({
  value = '',
  onChange,
  options = [],
  placeholder = 'Seçiniz veya yazınız...',
  className = '',
  required = false,
  disabled = false,
}) {
  const [isOpen, setIsOpen] = useState(false)
  const [inputValue, setInputValue] = useState('')
  const [isUserTyping, setIsUserTyping] = useState(false)
  const containerRef = useRef(null)

  // Seçenek nesnesinden gösterim etiketini oluştur: "[KOD] - [AD]"
  const getOptionLabel = (opt) => {
    if (!opt) return ''
    const val = String(opt.value || '')
    const label = String(opt.label || '')
    if (label && label !== val) {
      // Eğer label zaten kodu içeriyorsa tekrar ekleme
      if (label.startsWith(val)) return label
      return `${val} - ${label}`
    }
    return val
  }

  const [prevValueKey, setPrevValueKey] = useState({ value, options, isUserTyping })
  if (
    !isUserTyping &&
    (prevValueKey.value !== value || prevValueKey.options !== options || prevValueKey.isUserTyping !== isUserTyping)
  ) {
    setPrevValueKey({ value, options, isUserTyping })
    if (value === null || value === undefined || value === '') {
      setInputValue('')
    } else {
      const found = options.find((o) => String(o.value) === String(value))
      if (found) {
        setInputValue(getOptionLabel(found))
      } else {
        setInputValue(String(value))
      }
    }
  }

  // Dışarı tıklanınca menüyü kapat ve arama durumunu sıfırla
  useEffect(() => {
    function handleClickOutside(event) {
      if (containerRef.current && !containerRef.current.contains(event.target)) {
        setIsOpen(false)
        setIsUserTyping(false)
        // Eğer dışarı tıklandıysa ve geçerli seçili değer varsa gösterimini formatla
        if (value) {
          const found = options.find((o) => String(o.value) === String(value))
          if (found) setInputValue(getOptionLabel(found))
        }
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [value, options])

  // Odaklanıldığında (Focus) veya Tıklandığında
  const handleFocus = (e) => {
    setIsOpen(true)
    setIsUserTyping(false) // Tıklanınca tüm liste açılsın
    e.target.select() // İçindeki metni seç ki direkt yazınca üzerini yazsın
  }

  // Yazı Değiştiğinde
  const handleInputChange = (e) => {
    const text = e.target.value
    setInputValue(text)
    setIsUserTyping(true)
    setIsOpen(true)

    if (onChange) {
      // Girilen metin bir seçeneğin koduna veya etiketine tam uyuyorsa onun value'sunu ilet
      const matched = options.find((o) => {
        const valStr = String(o.value).toLowerCase()
        const formattedStr = getOptionLabel(o).toLowerCase()
        return valStr === text.toLowerCase() || formattedStr === text.toLowerCase()
      })
      onChange(matched ? matched.value : text)
    }
  }

  // Seçenek Seçildiğinde
  const handleSelectOption = (opt) => {
    const formatted = getOptionLabel(opt)
    setInputValue(formatted)
    setIsUserTyping(false)
    setIsOpen(false)
    if (onChange) onChange(opt.value)
  }

  // Arama Filtresi: Kullanıcı yazmaya başladıysa filtrele, sadece tıklayıp açtıysa TÜM LİSTEYİ göster!
  const filteredOptions = options.filter((opt) => {
    if (!isUserTyping || !inputValue.trim()) return true
    const q = inputValue.toLowerCase().trim()
    const valStr = String(opt.value || '').toLowerCase()
    const labelStr = String(opt.label || '').toLowerCase()
    const subStr = String(opt.sublabel || '').toLowerCase()
    const formattedStr = getOptionLabel(opt).toLowerCase()

    return (
      valStr.includes(q) ||
      labelStr.includes(q) ||
      subStr.includes(q) ||
      formattedStr.includes(q)
    )
  })

  return (
    <div
      ref={containerRef}
      style={{ position: 'relative', width: '100%' }}
      className={`searchable-select-container ${className}`}
    >
      <div style={{ position: 'relative', display: 'flex', alignItems: 'center' }}>
        <input
          type="text"
          className="form-input"
          style={{ paddingRight: '2.2rem' }}
          value={inputValue}
          onChange={handleInputChange}
          onFocus={handleFocus}
          onClick={() => setIsOpen(true)}
          placeholder={placeholder}
          required={required}
          disabled={disabled}
        />
        <button
          type="button"
          tabIndex={-1}
          onClick={() => {
            setIsOpen((prev) => !prev)
            setIsUserTyping(false)
          }}
          style={{
            position: 'absolute',
            right: '0.5rem',
            background: 'transparent',
            border: 'none',
            color: 'var(--text-muted)',
            cursor: 'pointer',
            fontSize: '0.75rem',
            padding: '0.25rem',
            display: 'flex',
            alignItems: 'center',
          }}
        >
          ▼
        </button>
      </div>

      {isOpen && (
        <div
          style={{
            position: 'absolute',
            top: '100%',
            left: 0,
            right: 0,
            zIndex: 1000,
            marginTop: '0.25rem',
            maxHeight: '250px',
            overflowY: 'auto',
            background: 'var(--bg-surface)',
            border: '1px solid var(--border-medium)',
            borderRadius: 'var(--radius-sm)',
            boxShadow: 'var(--shadow-md)',
            padding: '0.25rem 0',
          }}
        >
          {filteredOptions.length === 0 ? (
            <div
              style={{
                padding: '0.6rem 0.8rem',
                fontSize: '0.78rem',
                color: 'var(--text-muted)',
                fontStyle: 'italic',
              }}
            >
              Uyan seçenek bulunamadı. (Girilen metin kullanılacak: "{inputValue}")
            </div>
          ) : (
            filteredOptions.map((opt, idx) => {
              const isSelected = String(opt.value) === String(value)
              const formattedLabel = getOptionLabel(opt)

              return (
                <div
                  key={`${opt.value}-${idx}`}
                  onClick={() => handleSelectOption(opt)}
                  style={{
                    padding: '0.55rem 0.8rem',
                    cursor: 'pointer',
                    fontSize: '0.8rem',
                    background: isSelected ? 'var(--brand-primary-subtle)' : 'transparent',
                    color: isSelected ? 'var(--brand-primary)' : 'var(--text-primary)',
                    display: 'flex',
                    flexDirection: 'column',
                    borderBottom: idx < filteredOptions.length - 1 ? '1px solid var(--border-subtle)' : 'none',
                    transition: 'background-color 0.1s ease',
                  }}
                  onMouseEnter={(e) => {
                    if (!isSelected) e.currentTarget.style.background = 'var(--bg-surface-hover)'
                  }}
                  onMouseLeave={(e) => {
                    if (!isSelected) e.currentTarget.style.background = 'transparent'
                  }}
                >
                  <div style={{ fontWeight: 600, fontFamily: 'var(--font-mono)' }}>
                    {formattedLabel}
                  </div>
                  {opt.sublabel && (
                    <div style={{ fontSize: '0.72rem', color: 'var(--text-muted)', marginTop: '0.1rem' }}>
                      {opt.sublabel}
                    </div>
                  )}
                </div>
              )
            })
          )}
        </div>
      )}
    </div>
  )
}

export default SearchableSelect
