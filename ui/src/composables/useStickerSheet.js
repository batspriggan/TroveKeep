import { computed, reactive, watch } from 'vue'
import { DEFAULT_OPTIONS, planStickerSheet, resolveOptions } from '../utils/stickerSheets.js'

/**
 * State of the sticker sheet composer: the layout options (persisted, they are a printing
 * preference) and the number of copies per sticker (volatile on purpose — a print run is a
 * one-off, and keeping it would silently re-print yesterday's labels).
 *
 * Singleton, same approach as `useSettings`/`useAuth`: no Pinia in this project.
 */
const STORAGE_KEY = 'trovekeep_sticker_sheet'

function loadOptions() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (raw) return JSON.parse(raw)
  } catch {
    // Unreadable storage: fall back to the defaults.
  }
  return null
}

/** Layout options, clamped so a hand-edited localStorage entry cannot break the sheet. */
const options = reactive(resolveOptions({ ...DEFAULT_OPTIONS, ...(loadOptions() ?? {}) }))

watch(options, (value) => {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ ...value }))
  } catch {
    // Storage unavailable (private mode, quota): the options simply do not persist.
  }
})

/** Copies per sticker id — never persisted. */
const copies = reactive({})

export function useStickerSheet() {
  function copiesOf(id) {
    return copies[String(id)] ?? 0
  }

  function setCopies(id, value) {
    const next = Math.max(0, Math.min(999, Math.floor(Number(value) || 0)))
    if (next === 0) delete copies[String(id)]
    else copies[String(id)] = next
  }

  function addCopies(id, delta) {
    setCopies(id, copiesOf(id) + delta)
  }

  function clearCopies() {
    for (const key of Object.keys(copies)) delete copies[key]
  }

  function resetOptions() {
    Object.assign(options, resolveOptions(DEFAULT_OPTIONS))
  }

  /**
   * Sets one layout option, clamped to its accepted range straight away: the field must not
   * show a value the engine would print differently.
   */
  function setOption(key, value) {
    const candidate = { ...options, [key]: value }
    Object.assign(options, resolveOptions(candidate))
  }

  /**
   * The selection handed to the engine: only stickers that are actually printable (artwork
   * uploaded) and have copies requested. Ordering is the engine's business.
   */
  function selection(stickers) {
    return stickers
      .filter((s) => s.imageCached && copiesOf(s.id) > 0)
      .map((s) => ({
        id: s.id,
        name: s.name,
        widthStuds: s.widthStuds,
        depthStuds: s.depthStuds,
        copies: copiesOf(s.id),
      }))
  }

  return {
    options,
    copies,
    copiesOf,
    setCopies,
    addCopies,
    clearCopies,
    resetOptions,
    setOption,
    selection,
    /** Total number of labels requested (only for stickers that can be printed). */
    totalCopies: (stickers) => selection(stickers).reduce((n, s) => n + s.copies, 0),
    /** Layout of the current selection, or the reason it cannot be laid out. */
    planFor(stickers) {
      try {
        return { plan: planStickerSheet(selection(stickers), options), error: null }
      } catch (error) {
        // `StickerFitError` names the labels that do not fit; anything else is a real bug
        // and is shown as-is rather than swallowed.
        return { plan: null, error }
      }
    },
  }
}

export { DEFAULT_OPTIONS }
