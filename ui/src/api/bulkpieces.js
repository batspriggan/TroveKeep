import { get, post, put, del } from './client.js'

const BASE = '/api/bulkpieces'

export const getAllBulkPieces = () => get(BASE)
export const getBulkPiece = (id) => get(`${BASE}/${id}`)
export const createBulkPiece = (body) => post(BASE, body)
export const updateBulkPiece = (id, body) => put(`${BASE}/${id}`, body)
export const deleteBulkPiece = (id) => del(`${BASE}/${id}`)

export const allocatePieceToBox = (id, boxId, quantity) => post(`${BASE}/${id}/storage/box/${boxId}`, { quantity })
export const allocatePieceToDrawer = (id, drawerId, quantity) => post(`${BASE}/${id}/storage/drawer/${drawerId}`, { quantity })
export const deallocatePieceStorage = (id, storageId) => del(`${BASE}/${id}/storage/${storageId}`)
export const clearPieceStorage = (id) => del(`${BASE}/${id}/storage`)
