import { get, post, put, del } from './client.js'

const BASE = '/api/collections'

export const getAllCollections = () => get(BASE)
export const getCollection = (id) => get(`${BASE}/${id}`)
export const createCollection = (body) => post(BASE, body)
export const updateCollection = (id, body) => put(`${BASE}/${id}`, body)
export const deleteCollection = (id) => del(`${BASE}/${id}`)

export const getCollectionMembers = (id) => get(`${BASE}/${id}/members`)
// Active accounts that are not members yet, for the add-member picker. Allowed for an admin and
// for the collection's Owner (GET /api/users stays admin-only).
export const getCollectionMemberCandidates = (id) => get(`${BASE}/${id}/member-candidates`)
export const addCollectionMember = (id, userId, access) =>
  post(`${BASE}/${id}/members`, { userId, access })
export const removeCollectionMember = (id, userId) => del(`${BASE}/${id}/members/${userId}`)
