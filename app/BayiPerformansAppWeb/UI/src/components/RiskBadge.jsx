// RiskBadge.jsx - Deterministik Risk Seviyesi Rozeti

export function RiskBadge({ level, score }) {
  const normalizedLevel = (level || 'DUSUK').toUpperCase()

  const labels = {
    DUSUK: 'Düşük Risk',
    ORTA: 'Orta Risk',
    YUKSEK: 'Yüksek Risk',
  }

  return (
    <span className={`risk-badge ${normalizedLevel}`} title={`Hesaplanan Risk Skoru: ${score ?? 0}`}>
      <span>●</span>
      <span>{labels[normalizedLevel] || normalizedLevel}</span>
      {score !== undefined && score !== null && (
        <span style={{ opacity: 0.85, fontWeight: 700, marginLeft: '2px' }}>
          ({score})
        </span>
      )}
    </span>
  )
}
export default RiskBadge
