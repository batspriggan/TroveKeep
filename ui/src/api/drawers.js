import { get, put, del } from './client.js'

const BASE = '/api/drawers'

export const getDrawer = (containerId, position) => get(`${BASE}/${containerId}/${position}`)
export const getDrawerContents = (containerId, position) => get(`${BASE}/${containerId}/${position}/contents`)
export const updateDrawer = (containerId, position, body) => put(`${BASE}/${containerId}/${position}`, body)
export const deleteDrawer = (containerId, position) => del(`${BASE}/${containerId}/${position}`)
