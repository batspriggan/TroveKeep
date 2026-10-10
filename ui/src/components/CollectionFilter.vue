<!--
  Per-view collection filter.

  The active collection is persisted by `useActiveCollection` and replayed on every API request
  as the `X-TroveKeep-Collection` header (see `api/client.js`), so selecting one here simply
  narrows the next `load()` of the host view. The "All collections" option clears the selection:
  no header is sent and the backend returns every membership again.

  The component renders nothing when the user has a single collection (nothing to filter).
  It emits `change` after updating the state, so the host view can reload its list.
-->
<template>
  <select
    v-if="showSelector"
    class="collection-filter"
    aria-label="Filter by collection"
    :value="activeCollectionId ?? ''"
    @change="onChange"
  >
    <option value="">All collections</option>
    <option v-for="c in collections" :key="c.id" :value="c.id">{{ c.name }}</option>
  </select>
</template>

<script setup>
import { useActiveCollection } from '../composables/useActiveCollection.js'

const emit = defineEmits(['change'])
const { collections, activeCollectionId, showSelector, setActiveCollection, clearActiveCollection } =
  useActiveCollection()

function onChange(e) {
  if (e.target.value) setActiveCollection(e.target.value)
  else clearActiveCollection()
  emit('change')
}
</script>

<style scoped>
.collection-filter {
  width: auto;
  max-width: 220px;
  font-size: var(--text-sm);
  border-color: var(--color-border);
  background: var(--color-surface);
}
</style>
