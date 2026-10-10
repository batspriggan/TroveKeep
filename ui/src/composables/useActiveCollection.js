import { computed, ref, watch } from 'vue'
import { useAuth } from './useAuth.js'

/**
 * The collection the user is currently working in. Persisted in localStorage and replayed on
 * every API request as the `X-TroveKeep-Collection` header, which the backend reads in
 * `CurrentUser.ActiveCollectionId`. A stored id that is no longer among the user's collections
 * (deleted, or access revoked) is dropped and the backend falls back to its own default.
 *
 * Singleton state, same approach as `useSettings` / `useAuth`: no Pinia in this project.
 */
const STORAGE_KEY = 'trovekeep_active_collection'

function loadStored() {
  try {
    return localStorage.getItem(STORAGE_KEY) || null
  } catch {
    return null
  }
}

function persist(id) {
  try {
    if (id) localStorage.setItem(STORAGE_KEY, id)
    else localStorage.removeItem(STORAGE_KEY)
  } catch {
    // Storage unavailable (private mode, quota): the header simply falls back to the default.
  }
}

const storedId = ref(loadStored())

/** Case-insensitive lookup, so the id survives GUID casing differences. */
function findCollection(collections, id) {
  if (!id) return null
  const found = (collections ?? []).find((c) => c.id?.toLowerCase() === id.toLowerCase())
  return found ? found.id : null
}

const auth = useAuth()

// Drop a stored selection that has become stale instead of silently keeping it around.
watch(
  () => auth.collections.map((c) => c.id).join(','),
  () => {
    if (storedId.value && !findCollection(auth.collections, storedId.value)) {
      storedId.value = null
      persist(null)
    }
  },
)

/** Resolved selection, or null when nothing valid is stored. */
export function getActiveCollectionId() {
  return findCollection(auth.collections, storedId.value)
}

export function setActiveCollection(id) {
  const next = id || null
  storedId.value = next
  persist(next)
}

export function useActiveCollection() {
  const collections = computed(() => auth.collections)
  const activeCollectionId = computed(() => findCollection(auth.collections, storedId.value))
  // The header only makes sense when there is a real choice to make. With zero or one
  // collection the backend default is the only possible answer.
  const showSelector = computed(() => auth.collections.length > 1)
  // What the selector should display: the stored choice, or the default (first) collection.
  const effectiveCollectionId = computed(
    () => activeCollectionId.value ?? auth.collections[0]?.id ?? null,
  )

  return { collections, activeCollectionId, effectiveCollectionId, showSelector, setActiveCollection }
}
