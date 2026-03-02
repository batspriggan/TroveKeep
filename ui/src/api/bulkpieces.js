import { get, post, put, del } from './client.js'

const BASE = '/api/bulkpieces'

export const getAllBulkPieces = () => get(BASE)
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
export const clearPieceStorage = (id) => del(`${BASE}/${id}/storage`)
