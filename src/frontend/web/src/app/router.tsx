import { createBrowserRouter } from 'react-router-dom'
import { AppShell } from '../layouts/AppShell'
import { DashboardPage } from '../pages/DashboardPage'
import { ModulePage } from '../pages/ModulePage'

export const appRouter = createBrowserRouter([
  {
    path: '/',
    element: <AppShell />,
    children: [
      {
        index: true,
        element: <DashboardPage />,
      },
      {
        path: 'modules/:moduleKey',
        element: <ModulePage />,
      },
    ],
  },
])
