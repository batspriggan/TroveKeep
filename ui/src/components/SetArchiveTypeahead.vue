<template>
  <div class="typeahead">
    <input
      v-model="query"
      type="text"
      placeholder="Search by set number or name…"
      autocomplete="off"
      @input="onInput"
      @blur="onBlur"
      @focus="showDropdown = results.length > 0"
    />
    <ul v-if="showDropdown && results.length" class="dropdown">
      <li
        v-for="s in results"
        :key="s.setNum"
        @mousedown.prevent="select(s)"
      >
        <span class="set-info">
          <strong>{{ s.setNum }}</strong> — {{ s.name }}
          <span class="year">({{ s.year }})</span>
        </span>
      </li>
    </ul>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { searchArchiveSets } from '../api/archives.js'

const emit = defineEmits(['select'])

const query = ref('')
const results = ref([])
const showDropdown = ref(false)
let debounceTimer = null

function onInput() {
  clearTimeout(debounceTimer)
  const q = query.value.trim()
  if (q.length < 2) {
    results.value = []
    showDropdown.value = false
    return
  }
  debounceTimer = setTimeout(async () => {
    try {
      results.value = await searchArchiveSets(q, 10)
      showDropdown.value = results.value.length > 0
    } catch {
      results.value = []
      showDropdown.value = false
    }
  }, 300)
}

function onBlur() {
  showDropdown.value = false
}

function select(s) {
  emit('select', s)
  query.value = ''
  results.value = []
  showDropdown.value = false
}
</script>

<style scoped>
.typeahead {
  position: relative;
  width: 100%;
  max-width: 400px;
}

.typeahead input {
  width: 100%;
  box-sizing: border-box;
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
  align-items: center;
  gap: 0.5rem;
  padding: 0.4rem 0.6rem;
  cursor: pointer;
}

.dropdown li:hover {
  background: #f0f4ff;
}

.set-info {
  font-size: 0.9rem;
}

.year {
  color: #666;
}
</style>
