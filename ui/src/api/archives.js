import { get } from './client.js'

const BASE = '/api/archives'

async function uploadFile(url, file) {
  const form = new FormData()
  form.append('file', file)
  const res = await fetch(url, { method: 'POST', body: form })
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

export const getColorsStatus = () => get(`${BASE}/colors`)
export const uploadColors = (file) => uploadFile(`${BASE}/colors/reload`, file)
export const getColorsList = () => get(`${BASE}/colors/list`)

export const getSetsStatus = () => get(`${BASE}/sets`)
export const uploadSets = (file) => uploadFile(`${BASE}/sets/reload`, file)
export const searchArchiveSets = (q, limit = 10) => get(`${BASE}/sets/search?q=${encodeURIComponent(q)}&limit=${limit}`)

export const getPartsStatus = () => get(`${BASE}/parts`)
export const uploadParts = (file) => uploadFile(`${BASE}/parts/reload`, file)
export const searchArchiveParts = (q, limit = 10, categoryId = null) => {
  const params = new URLSearchParams({ q, limit })
  if (categoryId !== null) params.append('categoryId', categoryId)
  return get(`${BASE}/parts/search?${params}`)
}

export const getPartsInventoryStatus = () => get(`${BASE}/partsinventory`)
export const uploadPartsInventory = (file) => uploadFile(`${BASE}/partsinventory/reload`, file)
export const searchArchivePartsInventory = (q, limit = 10) => get(`${BASE}/partsinventory/search?q=${encodeURIComponent(q)}&limit=${limit}`)

export const getPartCategoriesStatus = () => get(`${BASE}/partcategories`)
export const uploadPartCategories = (file) => uploadFile(`${BASE}/partcategories/reload`, file)
export const getPartCategoriesList = () => get(`${BASE}/partcategories/list`)
