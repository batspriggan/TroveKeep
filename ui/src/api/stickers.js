import { get, post, put, del } from './client.js'

const BASE = '/api/stickers'

/** The global sticker library. Reads are open to every account; writes need Editor anywhere. */
export const getStickers = () => get(BASE)
export const getSticker = (id) => get(`${BASE}/${id}`)
export const createSticker = (body) => post(BASE, body)
export const updateSticker = (id, body) => put(`${BASE}/${id}`, body)
export const deleteSticker = (id) => del(`${BASE}/${id}`)

export const stickerImageUrl = (sticker) =>
  `${BASE}/${sticker.id}/image${sticker.updatedAt ? `?v=${encodeURIComponent(sticker.updatedAt)}` : ''}`

/**
 * Uploads the artwork of a sticker. Raw `fetch` instead of the JSON client: a FormData body
 * must keep the browser's own multipart boundary, so no Content-Type may be set by hand.
 * Answers 200 with the updated row (new `version`, `imageCached: true`).
 */
export async function uploadStickerImage(id, file) {
  const form = new FormData()
  form.append('file', file)
  const res = await fetch(`${BASE}/${id}/image`, {
    method: 'POST',
    body: form,
    credentials: 'include',
  })
  if (!res.ok) {
    let message = `HTTP ${res.status}`
    try {
      const data = await res.json()
      if (data?.error) message = data.error
    } catch {
      // non-JSON error body
    }
    const error = new Error(message)
    error.status = res.status
    throw error
  }
  return res.json()
}

/** Removes the artwork; answers 200 with the updated row, like the upload. */
export const deleteStickerImage = (id) => del(`${BASE}/${id}/image`)
