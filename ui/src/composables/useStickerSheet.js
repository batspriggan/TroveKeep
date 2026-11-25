import { reactive, watch } from 'vue'
import { DEFAULT_OPTIONS, planStickerSheet, resolveOptions } from '../utils/stickerSheets.js'

/**
 * State of the sticker sheet composer: ONLY the layout options, which are a printing preference
 * and therefore persisted in `localStorage`.
 *
 * The number of copies is not here any more: it is a field of the persisted project item, so a
 * saved project always prints the same sheet. The sheet is the last step of a project, not a
 * catalogue-wide counter.
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

/**
 * The items of a project that can actually be printed: artwork uploaded and at least one copy
 * requested. Ordering and pagination are the engine's business.
 */
export function printableStickerItems(items) {
  return (items ?? []).filter(
    (item) => item.imageCached !== false && Math.floor(Number(item.copies) || 0) > 0,
  )
}

/** One printable project item as a unit of the packing engine. */
function toUnit(item) {
  return {
    id: item.id,
    // The engine sorts and names unfit labels by `name`; a project item names its artwork.
    name: item.imageName ?? item.name ?? '',
    // The mount is what makes the engine read `brickHeight` instead of `depthStuds`; a missing
    // one is a tile, exactly like the backend default.
    mount: item.mount ?? 'Tile',
    widthStuds: item.widthStuds,
    depthStuds: item.depthStuds,
    brickHeight: item.brickHeight ?? null,
    copies: Math.floor(Number(item.copies) || 0),
    fit: item.fit,
  }
}

export function useStickerSheet() {
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

  /** The selection handed to the packing engine. */
  function selection(items) {
    return printableStickerItems(items).map(toUnit)
  }

  return {
    options,
    resetOptions,
    setOption,
    printableItems: printableStickerItems,
    selection,
    /** Total number of labels requested (only for items that can be printed). */
    totalCopies: (items) => selection(items).reduce((n, item) => n + item.copies, 0),
    /** Layout of the current selection, or the reason it cannot be laid out. */
    planFor(items) {
      try {
        return { plan: planStickerSheet(selection(items), options), error: null }
      } catch (error) {
        // `StickerFitError` names the labels that do not fit; anything else is a real bug
        // and is shown as-is rather than swallowed.
        return { plan: null, error }
      }
    },
  }
}

export { DEFAULT_OPTIONS }
