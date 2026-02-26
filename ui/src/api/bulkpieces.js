import { get, post, put, del } from './client.js'

const BASE = '/api/bulkpieces'

export const getAllBulkPieces = () => get(BASE)
export const getBulkPiece = (id) => get(`${BASE}/${id}`)
export const createBulkPiece = (body) => post(BASE, body)
export const updateBulkPiece = (id, body) => put(`${BASE}/${id}`, body)
export const deleteBulkPiece = (id) => del(`${BASE}/${id}`)

export const getBulkPieceStorage = (id) => get(`${BASE}/${id}/storage`)
export const assignBulkPieceToBox = (id, boxId) => put(`${BASE}/${id}/storage/box/${boxId}`)
export const assignBulkPieceToDrawer = (id, drawerId) => put(`${BASE}/${id}/storage/drawer/${drawerId}`)
export const removeBulkPieceStorage = (id) => del(`${BASE}/${id}/storage`)
