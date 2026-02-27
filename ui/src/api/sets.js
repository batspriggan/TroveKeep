import { get, post, put, del } from './client.js'

const BASE = '/api/sets'

export const getAllSets = () => get(BASE)
export const getSet = (id) => get(`${BASE}/${id}`)
export const createSet = (body) => post(BASE, body)
export const updateSet = (id, body) => put(`${BASE}/${id}`, body)
export const deleteSet = (id) => del(`${BASE}/${id}`)

export const allocateSetToBox = (id, boxId, quantity) => post(`${BASE}/${id}/storage/box/${boxId}`, { quantity })
export const deallocateSetStorage = (id, storageId) => del(`${BASE}/${id}/storage/${storageId}`)
export const clearSetStorage = (id) => del(`${BASE}/${id}/storage`)
