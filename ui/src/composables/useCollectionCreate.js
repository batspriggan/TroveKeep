import { computed, ref } from 'vue'
import { useActiveCollection } from './useActiveCollection.js'

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
 */
export function useCollectionCreate() {
  const { collections, activeCollectionId, setActiveCollection } = useActiveCollection()

  const needsCollectionChoice = computed(() => collections.value.length > 1)

  // The pick defaults to the current filter (or the first collection when the filter is
  // "All collections"), and follows the filter until the user picks one explicitly.
  const picked = ref(null)
  const collectionId = computed({
    get: () => picked.value ?? activeCollectionId.value ?? collections.value[0]?.id ?? null,
    set: (value) => { picked.value = value },
  })

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

  return { collections, needsCollectionChoice, collectionId, withCollection }
}
