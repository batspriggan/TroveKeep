// Rasterises sticker artwork for the PDF engine. Browser only: it needs `fetch`, an
// `<img>`/`ImageBitmap` and a `<canvas>` — none of which exist in the Node test runner, so
// this module carries no unit test and is verified by printing a sheet from the UI.
//
// The point of resampling here rather than letting jsPDF embed the uploaded file is twofold:
//   * the raster gets the label's own aspect ratio, so the PDF can fill the graphic area
//     without a scaling decision and the artwork cannot be stretched by a rounding error;
//   * the printed resolution is pinned to 600 dpi of the physical label, which is what a
//     16 mm tile label needs to look crisp, while a huge upload does not bloat the PDF.
import { stickerSizeMm } from './stickerSheets.js'

/** Target print resolution of the artwork. */
export const STICKER_DPI = 600

/** Hard cap on the raster's long side: a 24×32 stud label at 600 dpi would be ~6000 px. */
export const MAX_RASTER_PX = 4000

/** URL of a sticker's artwork; `updatedAt` busts the cache when the file is replaced. */
export function stickerImageUrl(sticker) {
  const version = sticker.updatedAt ? `?v=${encodeURIComponent(sticker.updatedAt)}` : ''
  return `/api/stickers/${sticker.id}/image${version}`
}

const mmToPx = (mm) => Math.max(1, Math.round((mm / 25.4) * STICKER_DPI))

function fitTo(widthMm, heightMm) {
  let w = mmToPx(widthMm)
  let h = mmToPx(heightMm)
  const longest = Math.max(w, h)
  if (longest > MAX_RASTER_PX) {
    const scale = MAX_RASTER_PX / longest
    w = Math.max(1, Math.round(w * scale))
    h = Math.max(1, Math.round(h * scale))
  }
  return { w, h }
}

/** Loads the artwork of one sticker as a drawable bitmap. */
async function loadBitmap(sticker, signal) {
  const response = await fetch(stickerImageUrl(sticker), { credentials: 'include', signal })
  if (!response.ok) throw new Error(`Sticker image ${sticker.id}: HTTP ${response.status}`)
  const blob = await response.blob()
  // `createImageBitmap` is the cheap path; the object-URL fallback keeps older browsers
  // working (Safari < 15).
  if (typeof createImageBitmap === 'function') return { bitmap: await createImageBitmap(blob), type: blob.type }
  const url = URL.createObjectURL(blob)
  try {
    const image = await new Promise((resolve, reject) => {
      const img = new Image()
      img.onload = () => resolve(img)
      img.onerror = () => reject(new Error(`Sticker image ${sticker.id} is not decodable`))
      img.src = url
    })
    return { bitmap: image, type: blob.type }
  } finally {
    URL.revokeObjectURL(url)
  }
}

function draw(bitmap, width, height, transparentBackdrop) {
  const canvas = document.createElement('canvas')
  canvas.width = width
  canvas.height = height
  const ctx = canvas.getContext('2d')
  if (!transparentBackdrop) {
    // A JPEG has no alpha: paint white so the PDF does not show the page background
    // through it (and so the printer does not composit it oddly).
    ctx.fillStyle = '#ffffff'
    ctx.fillRect(0, 0, width, height)
  }
  const sw = bitmap.width ?? width
  const sh = bitmap.height ?? height
  // "contain": the whole artwork is visible, centred, never cropped.
  const scale = Math.min(width / sw, height / sh)
  const dw = sw * scale
  const dh = sh * scale
  ctx.drawImage(bitmap, (width - dw) / 2, (height - dh) / 2, dw, dh)
  return canvas
}

function rotated(canvas) {
  const out = document.createElement('canvas')
  out.width = canvas.height
  out.height = canvas.width
  const ctx = out.getContext('2d')
  ctx.translate(out.width / 2, out.height / 2)
  ctx.rotate(Math.PI / 2)
  ctx.drawImage(canvas, -canvas.width / 2, -canvas.height / 2)
  return out
}

/**
 * Loads every sticker of the selection as PDF-ready data URLs keyed by sticker id:
 * `{ dataUrl, format, rotatedDataUrl?, rotatedFormat? }`.
 *
 * A PNG keeps its transparency (the label sheet is transparent polyethylene, after all) and
 * is exported as PNG; anything else is flattened onto white and exported as a quality-0.95
 * JPEG. Non-square labels also get the 90°-rotated variant the packer may ask for.
 */
export async function loadStickerImages(stickers, options = {}, { signal, onProgress } = {}) {
  const images = {}
  let done = 0
  for (const sticker of stickers) {
    try {
      const { bitmap, type } = await loadBitmap(sticker, signal)
      const size = stickerSizeMm(sticker, options)
      const { w, h } = fitTo(size.w, size.h)
      const isPng = (type || '').toLowerCase().includes('png')
      const canvas = draw(bitmap, w, h, isPng)
      const format = isPng ? 'PNG' : 'JPEG'
      const entry = {
        dataUrl: canvas.toDataURL(isPng ? 'image/png' : 'image/jpeg', 0.95),
        format,
      }
      if (sticker.widthStuds !== sticker.depthStuds) {
        const turned = rotated(canvas)
        entry.rotatedDataUrl = turned.toDataURL(isPng ? 'image/png' : 'image/jpeg', 0.95)
        entry.rotatedFormat = format
      }
      images[String(sticker.id)] = entry
      bitmap.close?.()
    } catch (error) {
      if (signal?.aborted) throw error
      // A sticker whose artwork cannot be fetched is reported, not fatal: the sheet is
      // still generated and that label simply comes out as an empty cut box.
      images[String(sticker.id)] = null
    }
    done++
    onProgress?.({ done, total: stickers.length, sticker })
  }
  return images
}
