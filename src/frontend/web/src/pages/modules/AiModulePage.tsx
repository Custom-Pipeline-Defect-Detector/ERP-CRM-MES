import { useEffect, useState } from 'react'
import { useSession, getApiErrorMessage } from '../../app/session'
import { aiApi } from '../../lib/api'
import { formatDate } from '../../lib/formatters'
import type { AiManagementSummary } from '../../types/runtime'

export function AiModulePage() {
  const { session } = useSession()
  const [summary, setSummary] = useState<AiManagementSummary | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  async function refreshData() {
    setIsLoading(true)

    try {
      const summaryResponse = await aiApi.getManagementSummary()
      setSummary(summaryResponse)
      setError(null)
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    if (!session) {
      return
    }

    void refreshData()
  }, [session?.token])

  return (
    <div className="page-shell">
      <section className="hero-card">
        <div className="tag-row">
          <span className="tag-chip tag-chip-gold">AI</span>
          <span className="tag-chip tag-chip-blue">Operational Summary</span>
        </div>
        <h1 className="hero-title">Summarize factory condition from governed operational data.</h1>
        <p className="hero-copy">
          The AI layer now runs over approved dashboard, quality, and accounting datasets. If the
          configured gateway is unavailable, the platform falls back to deterministic local rules instead of failing blind.
        </p>
      </section>

      {error ? <div className="notice-banner notice-banner-error">{error}</div> : null}

      <section className="content-grid">
        <article className="surface-card">
          <h3 className="surface-title">Summary Output</h3>
          {isLoading || !summary ? (
            <p className="muted-copy">Generating the management summary.</p>
          ) : (
            <>
              <div className="definition-grid">
                <div>
                  <span className="definition-label">Source</span>
                  <strong>{summary.source}</strong>
                </div>
                <div>
                  <span className="definition-label">Model</span>
                  <strong>{summary.model}</strong>
                </div>
                <div>
                  <span className="definition-label">Generated</span>
                  <strong>{formatDate(summary.generatedAt)}</strong>
                </div>
              </div>
              <p>{summary.summary}</p>
            </>
          )}
        </article>

        <article className="surface-card">
          <div className="section-header">
            <h3 className="surface-title">Warnings</h3>
            <button className="button button-secondary" onClick={() => void refreshData()} type="button">
              Refresh
            </button>
          </div>
          {isLoading || !summary ? (
            <p className="muted-copy">Loading warnings.</p>
          ) : summary.warnings.length === 0 ? (
            <p className="muted-copy">No operational warnings were produced for the current snapshot.</p>
          ) : (
            <ul className="surface-list">
              {summary.warnings.map((warning) => (
                <li key={warning}>{warning}</li>
              ))}
            </ul>
          )}
        </article>
      </section>
    </div>
  )
}
