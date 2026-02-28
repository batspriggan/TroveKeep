import { get, post, put, del } from './client.js'

const TEMPLATES = '/api/table-templates'
const ROOMS = '/api/rooms'

// Templates
export const getAllTemplates = () => get(TEMPLATES)
export const createTemplate = (body) => post(TEMPLATES, body)
export const updateTemplate = (id, body) => put(`${TEMPLATES}/${id}`, body)
export const deleteTemplate = (id) => del(`${TEMPLATES}/${id}`)

// Rooms
export const getAllRooms = () => get(ROOMS)
export const getRoom = (id) => get(`${ROOMS}/${id}`)
export const createRoom = (body) => post(ROOMS, body)
export const updateRoom = (id, body) => put(`${ROOMS}/${id}`, body)
export const saveRoomLayout = (id, layout) => put(`${ROOMS}/${id}/layout`, { layout })
export const deleteRoom = (id) => del(`${ROOMS}/${id}`)

export async function exportRoom(id) {
  const res = await fetch(`${ROOMS}/${id}/export`)
  if (!res.ok) throw new Error(`Server error: ${res.status}`)

  const blob = await res.blob()
  const contentDisposition = res.headers.get('Content-Disposition')
  let fileName = `room-${id}.zip`
  if (contentDisposition) {
    const match = contentDisposition.match(/filename[^;=\n]*=\s*["']?([^"';\n]+)["']?/)
    if (match) fileName = match[1].trim()
  }

  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  a.click()
  URL.revokeObjectURL(url)
}

export async function importRoom(file) {
  const form = new FormData()
  form.append('file', file)
  const res = await fetch(`${ROOMS}/import`, { method: 'POST', body: form })
  if (!res.ok) {
    let msg = `Server error: ${res.status}`
    try {
      const json = await res.json()
      if (json.error) msg = json.error
    } catch {
      // non-JSON error body
    }
    throw new Error(msg)
  }
  return res.json()
}
