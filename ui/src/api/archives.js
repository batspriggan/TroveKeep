import { get, post } from './client.js'

const BASE = '/api/archives'

export const getColorsStatus = () => get(`${BASE}/colors`)
export const reloadColors = () => post(`${BASE}/colors/reload`)
export const getColorsList = () => get(`${BASE}/colors/list`)

export const getSetsStatus = () => get(`${BASE}/sets`)
export const reloadSets = () => post(`${BASE}/sets/reload`)
export const searchArchiveSets = (q, limit = 10) => get(`${BASE}/sets/search?q=${encodeURIComponent(q)}&limit=${limit}`)
