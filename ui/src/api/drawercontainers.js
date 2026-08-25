import { get, post, put, del } from './client.js'

const BASE = '/api/drawercontainers'

export const getAllDrawerContainers = () => get(BASE)
export const getDrawerContainer = (id) => get(`${BASE}/${id}`)
export const getDrawerContainerDrawers = (id) => get(`${BASE}/${id}/drawers`)
export const createDrawerContainer = (body) => post(BASE, body)
export const updateDrawerContainer = (id, body) => put(`${BASE}/${id}`, body)
export const deleteDrawerContainer = (id) => del(`${BASE}/${id}`)

export const addDrawer = (containerId, body) => post(`${BASE}/${containerId}/drawers`, body)

function triggerDownload(href) {
  const a = document.createElement('a')
  a.href = href
  a.download = ''
  document.body.appendChild(a)
  a.click()
  a.remove()
}
// Downloads a zip with the label JSON for every bulk piece in the container.
export function downloadContainerPieceLabels(id) {
  triggerDownload(`${BASE}/${id}/labels.zip`)
}

export async function uploadContainerImage(id, file) {
  const form = new FormData()
  form.append('file', file)
  const res = await fetch(`${BASE}/${id}/image`, { method: 'POST', body: form })
  if (!res.ok) throw new Error(`${res.status}: ${await res.text()}`)
}

export const deleteContainerImage = (id) => del(`${BASE}/${id}/image`)
