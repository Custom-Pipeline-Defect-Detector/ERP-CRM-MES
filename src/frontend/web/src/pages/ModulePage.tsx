import { useParams } from 'react-router-dom'
import { getModuleDefinition } from '../app/moduleRegistry'
import { AiModulePage } from './modules/AiModulePage'
import { CrmModulePage } from './modules/CrmModulePage'
import { ErpModulePage } from './modules/ErpModulePage'
import { IntegrationModulePage } from './modules/IntegrationModulePage'
import { MesModulePage } from './modules/MesModulePage'
import { PlatformModulePage } from './modules/PlatformModulePage'
import { QualityModulePage } from './modules/QualityModulePage'
import { ReportingModulePage } from './modules/ReportingModulePage'

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

  if (moduleDefinition.key === 'platform') {
    return <PlatformModulePage />
  }

  if (moduleDefinition.key === 'crm') {
    return <CrmModulePage />
  }

  if (moduleDefinition.key === 'erp') {
    return <ErpModulePage />
  }

  if (moduleDefinition.key === 'mes') {
    return <MesModulePage />
  }

  if (moduleDefinition.key === 'quality') {
    return <QualityModulePage />
  }

  if (moduleDefinition.key === 'reporting') {
    return <ReportingModulePage />
  }

  if (moduleDefinition.key === 'ai') {
    return <AiModulePage />
  }

  if (moduleDefinition.key === 'integration') {
    return <IntegrationModulePage />
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
