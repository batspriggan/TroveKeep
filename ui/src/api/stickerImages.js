import { get, post, put, del } from './client.js'

// The global sticker image library: the artwork shelf. A row has a name and (optionally) one
// PNG/JPEG file; the footprint, copy count and fit live on a project item, not here.
const BASE = '/api/sticker-images'

/** Reads are open to every regular account (a Viewer may browse and print). */
export const getStickerImages = () => get(BASE)
export const getStickerImage = (id) => get(`${BASE}/${id}`)

/** Writes need Editor/Owner access in at least one collection (or an admin). */
export const createStickerImage = (body) => post(BASE, body)
export const updateStickerImage = (id, body) => put(`${BASE}/${id}`, body)

/**
 * Deletes an image. Answers 400 with an `{ error }` message when a project item still prints it
 * (the message names the usage count): the caller shows it, it is not a bug.
 */
export const deleteStickerImage = (id) => del(`${BASE}/${id}`)

/**
 * Uploads (or replaces) the artwork. Raw `fetch` instead of the JSON client: a FormData body must
 * keep the browser's own multipart boundary, so no Content-Type may be set by hand. Answers 200
 * with the updated row (new `version`, `imageCached: true`).
 */
export async function uploadStickerArtwork(id, file) {
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

/** Removes the artwork, keeping the row. Answers 200 with the updated row, like the upload. */
export const deleteStickerArtwork = (id) => del(`${BASE}/${id}/image`)
