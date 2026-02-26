import { get, post, put, del } from './client.js'

const BASE = '/api/drawercontainers'

export const getAllDrawerContainers = () => get(BASE)
export const getDrawerContainer = (id) => get(`${BASE}/${id}`)
export const getDrawerContainerDrawers = (id) => get(`${BASE}/${id}/drawers`)
export const createDrawerContainer = (body) => post(BASE, body)
export const updateDrawerContainer = (id, body) => put(`${BASE}/${id}`, body)
export const deleteDrawerContainer = (id) => del(`${BASE}/${id}`)

export const addDrawer = (containerId, body) => post(`${BASE}/${containerId}/drawers`, body)
