import { get, post, put } from './client.js'

const BASE = '/api/labels'

// ---- Config ----
export const getLabelConfig = () => get(`${BASE}/config`)
export const updateLabelConfig = (body) => put(`${BASE}/config`, body)

// ---- Print (server sink) ----
// kind: 'set' | 'bulkpiece' | 'box-summary' | 'box-qr' | 'box-pieces' | 'container-pieces'
export const printLabel = (kind, id, { copies = null, size = null } = {}) =>
  post(`${BASE}/print`, { kind, id, copies, size })

// ---- Remote server introspection ----
export const getServerHealth = () => get(`${BASE}/server/health`)
export const getServerJobs = (state = null, limit = 100) => {
  let url = `${BASE}/server/jobs?limit=${limit}`
  if (state) url += `&state=${encodeURIComponent(state)}`
  return get(url)
}
export const retryServerJob = (jobId) => post(`${BASE}/server/jobs/${encodeURIComponent(jobId)}/retry`)
export const getServerFormats = () => get(`${BASE}/server/formats`)

// ---- Client sink (existing download endpoints) ----
export function downloadLabelByKind(kind, id) {
  const path = {
    'set': `/api/sets/${id}/label-file`,
    'bulkpiece': `/api/bulkpieces/${id}/label-file`,
    'box-summary': `/api/boxes/${id}/label-summary`,
    'box-qr': `/api/boxes/${id}/label-qr`,
    'box-pieces': `/api/boxes/${id}/labels.zip`,
    'container-pieces': `/api/drawercontainers/${id}/labels.zip`,
  }[kind]
  if (!path) throw new Error(`kind non valido: ${kind}`)
  triggerDownload(path)
}

function triggerDownload(href) {
  const a = document.createElement('a')
  a.href = href
  a.download = ''
  document.body.appendChild(a)
  a.click()
  a.remove()
}
