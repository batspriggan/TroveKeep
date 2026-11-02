/**
 * Thin fetch wrapper for the API.
 *
 * Every request carries the session cookie (`credentials: 'include'`) and, when the user has
 * picked one, the active collection header (`X-TroveKeep-Collection`). A 401 means the session
 * is gone or was never established: we surface a typed error so the router guard can bounce the
 * user to /login without every caller having to know about auth.
 */
import { getActiveCollectionId } from '../composables/useActiveCollection.js'

export class UnauthorizedError extends Error {
  constructor(message = 'Not authenticated') {
    super(message)
    this.name = 'UnauthorizedError'
  }
}

async function request(path, options = {}) {
  const headers = { 'Content-Type': 'application/json', ...(options.headers ?? {}) }
  const collectionId = getActiveCollectionId()
  if (collectionId) headers['X-TroveKeep-Collection'] = collectionId

  const res = await fetch(path, {
    credentials: 'include',
    ...options,
    headers,
  })
  if (res.status === 401) throw new UnauthorizedError()
  if (res.status === 204) return null
  if (!res.ok) {
    // The API reports errors as `{ "error": "..." }`; prefer that over the raw body.
    const text = await res.text()
    let message = text
    let reported = false
    try {
      const data = JSON.parse(text)
      if (data?.error) {
        message = data.error
        reported = true
      }
    } catch {
      // non-JSON error body
    }
    // A write refused because the membership is read-only (Viewer) carries an `{ error }` body from
    // the backend. A refusal without one (framework-level "Forbidden", empty body) still has to
    // read as a permission problem instead of a generic status code.
    if (!reported && res.status === 403) {
      message = 'You do not have permission to do that (read-only access to this collection).'
      reported = true
    }
    const error = new Error(message || `HTTP ${res.status}`)
    // Callers that react differently per status (e.g. the setup form on 403/409) need it.
    error.status = res.status
    throw error
  }
  return res.json()
}

export const get = (path) => request(path)
export const post = (path, body) => request(path, { method: 'POST', body: JSON.stringify(body) })
export const put = (path, body) => request(path, { method: 'PUT', body: JSON.stringify(body) })
export const del = (path) => request(path, { method: 'DELETE' })
