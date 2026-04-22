import type { ModuleDefinition } from '../../types/module-definition'

export const reportingModule: ModuleDefinition = {
  key: 'reporting',
  title: 'Reporting',
  phase: 'Phase 1',
  summary: 'Dashboards, report exports, scheduled jobs, and custom definitions.',
  description:
    'Presents management views across sales, inventory, production, quality, and cost while supporting export and later report customization.',
  capabilities: [
    'Management dashboards across core operations',
    'Excel and PDF exports',
    'Scheduled report generation',
    'Custom filter and report-definition support',
  ],
  placeholderEntities: ['DashboardDefinition', 'ReportDefinition', 'ReportSnapshot', 'ExportJob'],
  integrationTouchpoints: [
    'Consumes curated data from all business modules',
    'Supports department-level report access',
    'Provides the approved dataset surface for AI',
  ],
  deliveryFocus: [
    'Management visibility from month 1',
    'Operational report exports',
    'Foundation for custom report development',
  ],
}
