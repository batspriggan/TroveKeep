import { get, post, put, del } from './client.js'

const BASE = '/api/bulkpieces'

export const getAllBulkPieces = (page = 1, size = 50, q = '') =>
  get(`${BASE}?page=${page}&size=${size}&q=${encodeURIComponent(q)}`)
export const getBulkPiece = (id) => get(`${BASE}/${id}`)
export const createBulkPiece = (body) => post(BASE, body)
export const updateBulkPiece = (id, body) => put(`${BASE}/${id}`, body)
export const deleteBulkPiece = (id) => del(`${BASE}/${id}`)

export const allocatePieceToBox = (id, boxId, quantity) => post(`${BASE}/${id}/storage/box/${boxId}`, { quantity })
export const allocatePieceToDrawer = (id, containerId, position, quantity) =>
  post(`${BASE}/${id}/storage/drawer/${containerId}/${position}`, { quantity })
export const deallocatePieceFromBox = (id, boxId) => del(`${BASE}/${id}/storage/box/${boxId}`)
export const deallocatePieceFromDrawer = (id, containerId, position) =>
  del(`${BASE}/${id}/storage/drawer/${containerId}/${position}`)
export const setDrawerQuantity = (id, containerId, position, quantity) =>
  put(`${BASE}/${id}/storage/drawer/${containerId}/${position}`, { quantity })
export const clearPieceStorage = (id) => del(`${BASE}/${id}/storage`)

// Triggers a browser download of the label JSON (the UI saves it to the label-tool watch folder).
export function downloadBulkPieceLabel(id) {
  const a = document.createElement('a')
  a.href = `${BASE}/${id}/label-file`
  a.download = ''
  document.body.appendChild(a)
  a.click()
  a.remove()
}
