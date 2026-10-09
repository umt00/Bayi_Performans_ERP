/* eslint-disable react-refresh/only-export-components */
// AuthContext.jsx - Oturum ve Kullanıcı Yönetimi
import { createContext, useContext, useState, useEffect } from 'react'
import {
  getToken,
  getMe,
  login as apiLogin,
  register as apiRegister,
  logout as apiLogout,
  isDemoMode,
  setDemoMode,
} from '../api/client'

const AuthContext = createContext(null)

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
  const [token, setTokenState] = useState(getToken())
  const [demo, setDemo] = useState(isDemoMode())
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    async function initAuth() {
      const storedToken = getToken()
      if (storedToken && !demo) {
        try {
          const me = await getMe()
          if (me) {
            setUser(me)
          } else {
            localStorage.removeItem('bp_auth_token')
            setTokenState(null)
            setUser(null)
          }
        } catch {
          localStorage.removeItem('bp_auth_token')
          setTokenState(null)
          setUser(null)
        }
      } else if (demo) {
        setUser({ id: 'demo-user', email: 'demo@bayiperformans.com', role: 'Admin' })
      } else {
        setUser(null)
      }
      setLoading(false)
    }
    initAuth()
  }, [demo])

  const login = async (email, sifre, forceDemo = false, dogrulamaToken = null) => {
    if (forceDemo) {
      setDemoMode(true)
      setDemo(true)
      const res = await apiLogin(email || 'demo@bayiperformans.com', sifre || 'Demo123!', dogrulamaToken)
      setTokenState(res.access_token)
      setUser(res.user || { email: email || 'demo@bayiperformans.com' })
      return res
    }

    setDemoMode(false)
    setDemo(false)
    const res = await apiLogin(email, sifre, dogrulamaToken)
    if (res && res.access_token) {
      setTokenState(res.access_token)
      setUser(res.user || { email })
    }
    return res
  }

  const register = async (email, sifre) => {
    return await apiRegister(email, sifre)
  }

  const logout = async () => {
    await apiLogout()
    setTokenState(null)
    setUser(null)
  }

  const toggleDemoMode = (enabled) => {
    setDemoMode(enabled)
    setDemo(enabled)
    if (enabled) {
      setUser({ id: 'demo-user', email: 'demo@bayiperformans.com', role: 'Admin' })
    } else {
      setUser(null)
    }
  }

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        isAuthenticated: demo ? true : !!user,
        isDemo: demo,
        loading,
        login,
        register,
        logout,
        toggleDemoMode,
      }}
    >
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}
