import { reactive, ref } from 'vue'

const state = reactive({
  user: null,
  loaded: false,
})

/**
 * The caller's own identity, fetched once at startup. `user` is null when not authenticated.
 * Kept as a module-level singleton so every component sees the same session (same approach as
 * useSettings).
 */
function applyUser(me) {
  state.user = me
  state.loaded = true
}

async function request(path, options = {}) {
  const res = await fetch(path, {
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    ...options,
  })
  if (res.status === 204) return null
  const text = await res.text()
  const data = text ? JSON.parse(text) : null
  if (!res.ok) throw new Error(`${res.status}: ${data?.error ?? text}`)
  return data
}

async function fetchMe() {
  try {
    const me = await request('/api/auth/me')
    applyUser(me)
    return me
  } catch {
    state.user = null
    state.loaded = true
    return null
  }
}

async function login(email, password) {
  const me = await request('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  })
  applyUser(me)
  return me
}

async function logout() {
  try {
    await request('/api/auth/logout', { method: 'POST' })
  } finally {
    state.user = null
  }
}

export function useAuth() {
  return {
    state,
    fetchMe,
    login,
    logout,
    get isAuthenticated() {
      return state.user != null
    },
    get isAdmin() {
      return state.user?.role === 'Admin'
    },
    get isGuest() {
      return state.user?.role === 'Guest'
    },
    get collections() {
      return state.user?.collections ?? []
    },
  }
}

export { request as authRequest }
