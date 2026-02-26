import { get, post, put, del } from './client.js'

const BASE = '/api/sets'

export const getAllSets = () => get(BASE)
export const getSet = (id) => get(`${BASE}/${id}`)
export const createSet = (body) => post(BASE, body)
export const updateSet = (id, body) => put(`${BASE}/${id}`, body)
export const deleteSet = (id) => del(`${BASE}/${id}`)

export const getSetStorage = (id) => get(`${BASE}/${id}/storage`)
export const assignSetToBox = (id, boxId) => put(`${BASE}/${id}/storage/box/${boxId}`)
export const removeSetStorage = (id) => del(`${BASE}/${id}/storage`)
