import { get, post } from './client.js'

const BASE = '/api/archives'

export const getColorsStatus = () => get(`${BASE}/colors`)
export const reloadColors = () => post(`${BASE}/colors/reload`)
export const getColorsList = () => get(`${BASE}/colors/list`)
