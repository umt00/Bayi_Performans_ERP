// MetricsCard.jsx - Kurumsal KPI Kartı

export function MetricsCard({
  title,
  value,
  subtext,
  badgeText,
  badgeType = 'info',
}) {
  return (
    <div className="metric-card">
      <div className="metric-card-top">
        <span className="metric-label">{title}</span>
        {badgeText && (
          <span className={`metric-badge ${badgeType}`}>
            {badgeText}
          </span>
        )}
      </div>

      <div className="metric-card-body">
        <div className="metric-value">{value}</div>
      </div>

      {subtext && (
        <div className="metric-card-footer">
          <span>{subtext}</span>
        </div>
      )}
    </div>
  )
}
export default MetricsCard
