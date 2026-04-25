import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { ApiError, clearStoredSessionToken, platformApi, setStoredSessionToken } from '../lib/api'
import type { Session } from '../types/runtime'

interface SessionContextValue {
  session: Session | null
  isLoading: boolean
  error: string | null
  login: (userName: string, password: string) => Promise<void>
  logout: () => void
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>
  availableUsers: string[]
}

const availableUsers = ['admin', 'sales', 'planner', 'operator', 'quality', 'finance']

const SessionContext = createContext<SessionContextValue | null>(null)

export function SessionProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let isCancelled = false

    async function initialize() {
      try {
        const existingSession = await platformApi.getSession()

        if (!isCancelled) {
          setSession(existingSession)
          setError(null)
        }
      } catch (initializationError) {
        if (!isCancelled) {
          const hasStoredSession = initializationError instanceof ApiError && initializationError.status !== 401
          clearStoredSessionToken()
          setSession(null)
          setError(hasStoredSession ? initializationError.message : null)
        }
      } finally {
        if (!isCancelled) {
          setIsLoading(false)
        }
      }
    }

    void initialize()

    return () => {
      isCancelled = true
    }
  }, [])

  async function login(userName: string, password: string) {
    setIsLoading(true)

    try {
      const nextSession = await platformApi.login(userName, password)
      setStoredSessionToken(nextSession.token)
      setSession(nextSession)
      setError(null)
    } catch (loginError) {
      clearStoredSessionToken()
      setSession(null)
      setError(loginError instanceof Error ? loginError.message : 'Unable to sign in.')
      throw loginError
    } finally {
      setIsLoading(false)
    }
  }

  function logout() {
    clearStoredSessionToken()
    setSession(null)
    setError(null)
  }

  async function changePassword(currentPassword: string, newPassword: string) {
    setIsLoading(true)

    try {
      const nextSession = await platformApi.changePassword({ currentPassword, newPassword })
      setStoredSessionToken(nextSession.token)
      setSession(nextSession)
      setError(null)
    } catch (changeError) {
      setError(changeError instanceof Error ? changeError.message : 'Unable to change password.')
      throw changeError
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <SessionContext.Provider
      value={{
        session,
        isLoading,
        error,
        login,
        logout,
        changePassword,
        availableUsers,
      }}
    >
      {children}
    </SessionContext.Provider>
  )
}

export function useSession() {
  const context = useContext(SessionContext)

  if (!context) {
    throw new Error('useSession must be used within SessionProvider.')
  }

  return context
}

export function getApiErrorMessage(error: unknown) {
  if (error instanceof ApiError) {
    return error.message
  }

  if (error instanceof Error) {
    return error.message
  }

  return 'Unexpected request failure.'
}
