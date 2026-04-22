import { RouterProvider } from 'react-router-dom'
import { appRouter } from './app/router'
import { SessionProvider } from './app/session'

function App() {
  return (
    <SessionProvider>
      <RouterProvider router={appRouter} />
    </SessionProvider>
  )
}

export default App
