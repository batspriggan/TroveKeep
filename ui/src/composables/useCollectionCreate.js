import { computed, ref } from 'vue'
import { useActiveCollection } from './useActiveCollection.js'
import { useAuth } from './useAuth.js'

/**
 * Helper for the create forms when the user belongs to several collections.
 *
 * The create endpoints carry no `collectionId` in their payload: the backend derives the target
 * collection from the `X-TroveKeep-Collection` header (or from the single membership). So once
 * the user picks a collection in the form, `withCollection()` temporarily sets that collection
 * as the active one (which flips the header) around the POST and restores the previous filter
 * afterwards. Not elegant, but it keeps the backend untouched.
 *
 * With a single collection there is nothing to choose: no field is rendered and the POST runs
 * with the current state.
 *
 * The picker only offers collections the user may write into, and `canWrite` tells the host view
 * whether the create form makes sense at all: a Viewer gets a read-only view with no add form
 * (the backend would answer 403 anyway).
 */
export function useCollectionCreate() {
  const auth = useAuth()
  const { activeCollectionId, setActiveCollection, writableCollections } = useActiveCollection()

  // The choice is only needed when the backend cannot infer the collection on its own, i.e. when
  // the user has more than one membership — even if only one of them is writable.
  const needsCollectionChoice = computed(() => auth.collections.length > 1)

  // The pick defaults to the current filter (or the first writable collection when the filter is
  // "All collections"), and follows the filter until the user picks one explicitly.
  const picked = ref(null)
  const collectionId = computed({
    get: () =>
      picked.value ?? activeCollectionId.value ?? writableCollections.value[0]?.id ?? auth.collections[0]?.id ?? null,
    set: (value) => { picked.value = value },
  })

  /** True when the collection the form would create into is writable. */
  const canWrite = computed(() => auth.canWriteIn(collectionId.value))

  /** Runs `run` with the chosen collection active, then restores the previous filter. */
  async function withCollection(run) {
    if (!needsCollectionChoice.value) return run()
    const previous = activeCollectionId.value
    setActiveCollection(collectionId.value)
    try {
      return await run()
    } finally {
      setActiveCollection(previous)
    }
  }

  return {
    collections: writableCollections, needsCollectionChoice, collectionId, canWrite, withCollection,
  }
}
