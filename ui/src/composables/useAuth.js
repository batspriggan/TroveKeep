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
  /**
   * Access level the user holds on a collection as reported by `/api/auth/me`:
   * `"Viewer" | "Editor" | "Owner" | "Admin"`, or `null` when the collection is not in the
   * caller's list. The backend is the boundary (a Viewer gets a 403 on every write); this is only
   * used to render the controls the user can actually use.
   */
  function accessFor(collectionId) {
    if (!collectionId) return null
    const key = String(collectionId).toLowerCase()
    const found = (state.user?.collections ?? []).find((c) => c.id?.toLowerCase() === key)
    return found?.access ?? null
  }

  /** True when the user may create/update/delete inside the collection. */
  function canWriteIn(collectionId) {
    return ['Editor', 'Owner', 'Admin'].includes(accessFor(collectionId))
  }

  /** True when the user may manage the members of the collection (admin or Owner). */
  function canManageMembersIn(collectionId) {
    return ['Owner', 'Admin'].includes(accessFor(collectionId))
  }

  return {
    state,
    fetchMe,
    login,
    logout,
    accessFor,
    canWriteIn,
    canManageMembersIn,
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
    /** True when at least one visible collection is writable (used to hide empty add sections). */
    get canWriteAnywhere() {
      return (state.user?.collections ?? []).some((c) => canWriteIn(c.id))
    },
    /** True when the user owns at least one collection (owner of the collection admin page). */
    get ownsAnyCollection() {
      return (state.user?.collections ?? []).some((c) => canManageMembersIn(c.id))
    },
  }
}

export { request as authRequest }
