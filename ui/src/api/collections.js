import { get, post, put, del } from './client.js'

const BASE = '/api/collections'

export const getAllCollections = () => get(BASE)
export const getCollection = (id) => get(`${BASE}/${id}`)
export const createCollection = (body) => post(BASE, body)
export const updateCollection = (id, body) => put(`${BASE}/${id}`, body)
export const deleteCollection = (id) => del(`${BASE}/${id}`)

export const getCollectionMembers = (id) => get(`${BASE}/${id}/members`)
export const addCollectionMember = (id, userId, access) =>
  post(`${BASE}/${id}/members`, { userId, access })
export const removeCollectionMember = (id, userId) => del(`${BASE}/${id}/members/${userId}`)
