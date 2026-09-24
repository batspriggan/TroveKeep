import { get, post, put, del } from './client.js'

const BASE = '/api/baseplates'

export const getAllBaseplates = () => get(BASE)
export const createBaseplate = (body, imported = false) =>
  post(`${BASE}${imported ? '?imported=true' : ''}`, body)
export const updateBaseplate = (id, body) => put(`${BASE}/${id}`, body)
export const confirmBaseplate = (id) => post(`${BASE}/${id}/confirm`, {})
export const deleteBaseplate = (id) => del(`${BASE}/${id}`)
export const getBaseplateImageUrl = (id) => `${BASE}/${id}/image`

export async function uploadBaseplateImage(id, file) {
  const form = new FormData()
  form.append('file', file)
  const res = await fetch(`${BASE}/${id}/image`, { method: 'POST', body: form })
  if (!res.ok) throw new Error(`HTTP ${res.status}`)
}

export const deleteBaseplateImage = (id) => del(`${BASE}/${id}/image`)

export const addReservation = (id, setId, quantity) =>
  post(`${BASE}/${id}/reservations`, { setId, quantity })
export const removeReservation = (id, setId) =>
  del(`${BASE}/${id}/reservations/${setId}`)

export const reconcileBaseplate = (id, body) => post(`${BASE}/${id}/reconcile`, body)
export const unquarantineBaseplate = (id, body) => post(`${BASE}/${id}/unquarantine`, body)
export const getBaseplatesBySet = (setId) => get(`${BASE}/by-set/${setId}`)

export const checkFeasibility = (aggregates) =>
  post(`${BASE}/feasibility`, { aggregates })

// Sets/MOCs that own baseplate reservations, as placeable planner entities.
export const getPlannerEntities = () => get(`${BASE}/planner-entities`)
