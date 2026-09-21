import { get, post, put, del } from './client.js'

const BASE = '/api/sets'

export const getAllSets = (page = 1, size = 50, q = '') =>
  get(`${BASE}?page=${page}&size=${size}&q=${encodeURIComponent(q)}`)
export const getSet = (id) => get(`${BASE}/${id}`)
export const createSet = (body) => post(BASE, body)
export const updateSet = (id, body) => put(`${BASE}/${id}`, body)
export const deleteSet = (id) => del(`${BASE}/${id}`)

export const allocateSetToBox = (id, boxId, quantity) => post(`${BASE}/${id}/storage/box/${boxId}`, { quantity })
export const deallocateSetStorage = (id, storageId) => del(`${BASE}/${id}/storage/${storageId}`)
export const clearSetStorage = (id) => del(`${BASE}/${id}/storage`)

export const getSetPhotos = (id) => get(`${BASE}/${id}/photos`)

export const uploadSetPhoto = (id, file) => {
  const form = new FormData()
  form.append('file', file)
  return fetch(`${BASE}/${id}/photos`, { method: 'POST', body: form }).then(async r => {
    if (!r.ok) throw new Error((await r.json().catch(() => ({}))).error ?? `HTTP ${r.status}`)
    return r.json()
  })
}

export const deleteSetPhoto = (id, photoId) => del(`${BASE}/${id}/photos/${photoId}`)
