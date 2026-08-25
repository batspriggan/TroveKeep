import { get, post, put, del } from './client.js'

const BASE = '/api/boxes'

export const getAllBoxes = () => get(BASE)
export const getBox = (id) => get(`${BASE}/${id}`)
export const getBoxContents = (id) => get(`${BASE}/${id}/contents`)
export const createBox = (body) => post(BASE, body)
export const updateBox = (id, body) => put(`${BASE}/${id}`, body)
export const deleteBox = (id) => del(`${BASE}/${id}`)

export async function uploadBoxImage(id, file) {
  const form = new FormData()
  form.append('file', file)
  const res = await fetch(`${BASE}/${id}/image`, { method: 'POST', body: form })
  if (!res.ok) throw new Error(`${res.status}: ${await res.text()}`)
}

export const deleteBoxImage = (id) => del(`${BASE}/${id}/image`)

// Triggers browser downloads of the two box label JSON files (saved to the watch folder).
export function downloadBoxSummary(id) {
  triggerDownload(`${BASE}/${id}/label-summary`)
}
export function downloadBoxQr(id) {
  triggerDownload(`${BASE}/${id}/label-qr`)
}
// Downloads a zip with the label JSON for every bulk piece in the box.
export function downloadBoxPieceLabels(id) {
  triggerDownload(`${BASE}/${id}/labels.zip`)
}

function triggerDownload(href) {
  const a = document.createElement('a')
  a.href = href
  a.download = ''
  document.body.appendChild(a)
  a.click()
  a.remove()
}
