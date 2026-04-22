import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { ApiError, clearStoredSessionToken, platformApi, setStoredSessionToken } from '../lib/api'
import type { Session } from '../types/runtime'

interface SessionContextValue {
  session: Session | null
  isLoading: boolean
  error: string | null
  loginAs: (userName: string) => Promise<void>
  availableUsers: string[]
}

const availableUsers = ['admin', 'sales', 'planner', 'operator', 'quality']

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
      } catch {
        try {
          const defaultSession = await platformApi.login('admin')

          if (!isCancelled) {
            setStoredSessionToken(defaultSession.token)
            setSession(defaultSession)
            setError(null)
          }
        } catch (initializationError) {
          if (!isCancelled) {
            const message =
              initializationError instanceof Error
                ? initializationError.message
                : 'Unable to initialize the session.'

            clearStoredSessionToken()
            setSession(null)
            setError(message)
          }
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

  async function loginAs(userName: string) {
    setIsLoading(true)

    try {
      const nextSession = await platformApi.login(userName)
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

  return (
    <SessionContext.Provider
      value={{
        session,
        isLoading,
        error,
        loginAs,
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
