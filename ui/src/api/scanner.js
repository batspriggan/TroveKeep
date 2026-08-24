import { get } from './client.js'

const BASE = '/api/scanner'

export const resolveCode = (code) => get(`${BASE}/resolve?code=${encodeURIComponent(code)}`)
