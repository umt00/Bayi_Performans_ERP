// AIComment.jsx - AI / Kural Motoru Açıklama Vitrini

export function AIComment({ comment, alertDraft, source, isFallback }) {
  if (!comment && !alertDraft) return null

  const isLLM = source === 'LLM' && !isFallback

  return (
    <div className="ai-comment-card">
      <div className="ai-comment-header">
        <span>
          {isLLM
            ? '✨ Yapay Zeka Performans Analizi (Groq LLM)'
            : isFallback
            ? '⚡ AI Fallback Yorumu'
            : '⚡ Kural Motoru Karar Özeti'}
        </span>
        {isFallback && (
          <span style={{ fontSize: '0.72rem', fontWeight: 600, color: '#d97706', background: 'rgba(245, 158, 11, 0.15)', padding: '2px 8px', borderRadius: '4px', border: '1px solid rgba(245, 158, 11, 0.4)' }}>
            is_fallback: true
          </span>
        )}
      </div>

      {comment && <p className="ai-comment-body">“{comment}”</p>}

      {alertDraft && (
        <div className="ai-alert-draft">
          <div style={{ fontWeight: 700, fontSize: '0.78rem', textTransform: 'uppercase', marginBottom: '0.2rem' }}>
            🔔 Bayi İçin Önerilen Uyarı Mesajı:
          </div>
          <div>{alertDraft}</div>
        </div>
      )}
    </div>
  )
}
export default AIComment

