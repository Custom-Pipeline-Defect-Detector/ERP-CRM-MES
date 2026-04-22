import { useParams } from 'react-router-dom'
import { getModuleDefinition } from '../app/moduleRegistry'

export function ModulePage() {
  const { moduleKey } = useParams()
  const moduleDefinition = getModuleDefinition(moduleKey)

  if (!moduleDefinition) {
    return (
      <section className="surface-card">
        <h1 className="surface-title">Unknown module</h1>
        <p>The selected module is not part of the current scaffold.</p>
      </section>
    )
  }

  return (
    <div className="page-shell">
      <section className="hero-card">
        <div className="tag-row">
          <span className="tag-chip tag-chip-gold">{moduleDefinition.phase}</span>
          <span className="tag-chip tag-chip-blue">{moduleDefinition.key.toUpperCase()}</span>
        </div>
        <h1 className="hero-title">{moduleDefinition.title}</h1>
        <p className="hero-copy">{moduleDefinition.description}</p>
      </section>

      <section className="content-grid">
        <article className="surface-card">
          <h3 className="surface-title">Planned Capabilities</h3>
          <ul className="surface-list">
            {moduleDefinition.capabilities.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </article>

        <article className="surface-card">
          <h3 className="surface-title">Placeholder Entities</h3>
          <div className="tag-row">
            {moduleDefinition.placeholderEntities.map((entity) => (
              <span key={entity} className="tag-chip tag-chip-outline">
                {entity}
              </span>
            ))}
          </div>
        </article>

        <article className="surface-card">
          <h3 className="surface-title">Integration Touchpoints</h3>
          <ul className="surface-list">
            {moduleDefinition.integrationTouchpoints.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </article>

        <article className="surface-card">
          <h3 className="surface-title">Delivery Focus</h3>
          <ul className="surface-list">
            {moduleDefinition.deliveryFocus.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </article>
      </section>
    </div>
  )
}
