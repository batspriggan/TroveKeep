import { get, post, put, del } from './client.js'

// Sticker projects: named print jobs whose embedded items reference the image library.
// A project carries no collection of its own, so reads are open to every regular account and
// writes need Editor/Owner access anywhere (or an admin).
const BASE = '/api/sticker-projects'

export const getStickerProjects = () => get(BASE)
export const getStickerProject = (id) => get(`${BASE}/${id}`)
export const createStickerProject = (body) => post(BASE, body)

/**
 * Replaces the project (name + whole item list, supplied item ids kept). `version` guards against
 * two tabs overwriting each other: a mismatch answers 409, which the editor surfaces by reloading.
 */
export const updateStickerProject = (id, body) => put(`${BASE}/${id}`, body)
export const deleteStickerProject = (id) => del(`${BASE}/${id}`)

/**
 * The `items` payload of a project, as the API expects it: the item ids travel along so the
 * rows survive an update (new rows keep the id the client generated).
 */
export function toItemPayload(items) {
  return (items ?? []).map((item) => ({
    id: item.id,
    imageId: item.imageId,
    // The mount decides the second label dimension: a tile adds its depth in studs, a brick stack
    // its height in bricks (`brickHeight`); the server ignores whichever one does not apply.
    mount: item.mount ?? 'Tile',
    widthStuds: item.widthStuds,
    depthStuds: item.depthStuds,
    brickHeight: item.brickHeight ?? null,
    copies: item.copies,
    fit: item.fit,
    // How the artwork sits inside the label; `null` lets the server apply the `Auto` default.
    orientation: item.orientation ?? null,
    notes: item.notes ?? null,
  }))
}
