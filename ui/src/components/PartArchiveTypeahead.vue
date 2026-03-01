<template>
  <div class="typeahead">
    <div class="search-row">
      <select
        v-if="categories.length"
        v-model="selectedCategoryId"
        class="category-filter"
      >
        <option :value="null">All categories</option>
        <option v-for="c in categories" :key="c.id" :value="c.id">{{ c.name }}</option>
      </select>
      <input
        v-model="query"
        type="text"
        placeholder="Search by part number or name…"
        autocomplete="off"
        required
        @input="onInput"
        @blur="onBlur"
        @focus="showDropdown = results.length > 0"
      />
    </div>
    <ul v-if="showDropdown && results.length" class="dropdown">
      <li
        v-for="p in results"
        :key="p.partNum"
        @mousedown.prevent="select(p)"
      >
        <strong>{{ p.partNum }}</strong>
        <span class="part-name">{{ p.name }}</span>
      </li>
      <li v-if="hasMore || loadingAll" class="overflow-hint" @mousedown.prevent="onLoadAll">
        <span v-if="loadingAll">Loading all results, please wait…</span>
        <span v-else>Showing 25 — click to load all (may be slow)</span>
      </li>
    </ul>
  </div>
</template>

<script setup>
import { ref, watch, onMounted } from 'vue'
import { searchArchiveParts, getPartCategoriesList } from '../api/archives.js'

const props = defineProps({ modelValue: { type: String, default: '' } })
const emit = defineEmits(['update:modelValue', 'select'])

const query = ref(props.modelValue)
const results = ref([])
const hasMore = ref(false)
const loadingAll = ref(false)
const showDropdown = ref(false)
const categories = ref([])
const selectedCategoryId = ref(null)
let debounceTimer = null

watch(() => props.modelValue, (val) => { query.value = val })

onMounted(async () => {
  try {
    const list = await getPartCategoriesList()
    categories.value = list.slice().sort((a, b) => a.name.localeCompare(b.name))
  } catch {
    // categories not imported — select stays hidden
  }
})

watch(selectedCategoryId, () => {
  if (query.value.trim().length >= 2) doSearch(query.value.trim())
})

function onInput() {
  emit('update:modelValue', query.value)
  clearTimeout(debounceTimer)
  const q = query.value.trim()
  if (q.length < 2) {
    results.value = []
    hasMore.value = false
    loadingAll.value = false
    showDropdown.value = false
    return
  }
  loadingAll.value = false
  debounceTimer = setTimeout(() => doSearch(q), 300)
}

async function doSearch(q) {
  try {
    const fetched = await searchArchiveParts(q, 26, selectedCategoryId.value)
    hasMore.value = fetched.length > 25
    results.value = fetched.slice(0, 25)
    showDropdown.value = results.value.length > 0
  } catch {
    results.value = []
    hasMore.value = false
    showDropdown.value = false
  }
}

async function onLoadAll() {
  const q = query.value.trim()
  if (q.length < 2 || loadingAll.value) return
  loadingAll.value = true
  try {
    const fetched = await searchArchiveParts(q, 0, selectedCategoryId.value)
    hasMore.value = false
    results.value = fetched
    showDropdown.value = results.value.length > 0
  } catch {
    // keep current results on error
  } finally {
    loadingAll.value = false
  }
}

function onBlur() {
  showDropdown.value = false
}

function select(p) {
  query.value = p.partNum
  emit('update:modelValue', p.partNum)
  emit('select', p)
  results.value = []
  hasMore.value = false
  showDropdown.value = false
}
</script>

<style scoped>
.typeahead {
  position: relative;
  width: 100%;
}

.search-row {
  display: flex;
  gap: 4px;
  align-items: stretch;
}

.category-filter {
  flex-shrink: 0;
  width: 200px;
  font-size: 0.82rem;
  padding: 0.2rem 0.3rem;
  color: #555;
  border: 1px solid #ccc;
  border-radius: 3px;
  background: #fafafa;
}

.typeahead input {
  flex: 1;
  min-width: 0;
}

.dropdown {
  position: absolute;
  z-index: 100;
  top: calc(100% + 2px);
  left: 0;
  right: 0;
  margin: 0;
  padding: 0;
  list-style: none;
  background: #fff;
  border: 1px solid #ccc;
  border-radius: 4px;
  max-height: 280px;
  overflow-y: auto;
  box-shadow: 0 4px 12px rgba(0,0,0,0.12);
}

.dropdown li {
  display: flex;
  align-items: baseline;
  gap: 0.5rem;
  padding: 0.4rem 0.6rem;
  cursor: pointer;
  font-size: 0.9rem;
}

.dropdown li:hover {
  background: #f0f4ff;
}

.part-name {
  color: #555;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.overflow-hint {
  color: #666;
  font-size: 0.8rem;
  cursor: pointer;
  justify-content: center;
  border-top: 1px solid #eee;
  font-style: italic;
}

.overflow-hint:hover {
  background: #fff8e1;
}
</style>
