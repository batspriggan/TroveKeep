/**
 * Thin fetch wrapper for the API.
 *
 * Every request carries the session cookie (`credentials: 'include'`). A 401 means the session
 * is gone or was never established: we surface a typed error so the router guard can bounce the
 * user to /login without every caller having to know about auth.
 */
export class UnauthorizedError extends Error {
  constructor(message = 'Not authenticated') {
    super(message)
    this.name = 'UnauthorizedError'
  }
}

async function request(path, options = {}) {
  const res = await fetch(path, {
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    ...options,
  })
  if (res.status === 401) throw new UnauthorizedError()
  if (!res.ok) throw new Error(`${res.status}: ${await res.text()}`)
  if (res.status === 204) return null
  return res.json()
}

export const get = (path) => request(path)
export const post = (path, body) => request(path, { method: 'POST', body: JSON.stringify(body) })
export const put = (path, body) => request(path, { method: 'PUT', body: JSON.stringify(body) })
export const del = (path) => request(path, { method: 'DELETE' })
