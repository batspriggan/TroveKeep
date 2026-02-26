import { get, put, del } from './client.js'

const BASE = '/api/drawers'

export const getDrawer = (id) => get(`${BASE}/${id}`)
export const getDrawerContents = (id) => get(`${BASE}/${id}/contents`)
export const updateDrawer = (id, body) => put(`${BASE}/${id}`, body)
export const deleteDrawer = (id) => del(`${BASE}/${id}`)
