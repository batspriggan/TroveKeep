<template>
  <div class="typeahead">
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
    <ul v-if="showDropdown && results.length" class="dropdown">
      <li
        v-for="p in results"
        :key="p.partNum"
        @mousedown.prevent="select(p)"
      >
        <strong>{{ p.partNum }}</strong>
        <span class="part-name">{{ p.name }}</span>
      </li>
    </ul>
  </div>
</template>

<script setup>
import { ref, watch } from 'vue'
import { searchArchiveParts } from '../api/archives.js'

const props = defineProps({ modelValue: { type: String, default: '' } })
const emit = defineEmits(['update:modelValue', 'select'])

const query = ref(props.modelValue)
const results = ref([])
const showDropdown = ref(false)
let debounceTimer = null

watch(() => props.modelValue, (val) => { query.value = val })

function onInput() {
  emit('update:modelValue', query.value)
  clearTimeout(debounceTimer)
  const q = query.value.trim()
  if (q.length < 2) {
    results.value = []
    showDropdown.value = false
    return
  }
  debounceTimer = setTimeout(async () => {
    try {
      results.value = await searchArchiveParts(q, 10)
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

function select(p) {
  query.value = p.partNum
  emit('update:modelValue', p.partNum)
  emit('select', p)
  results.value = []
  showDropdown.value = false
}
</script>

<style scoped>
.typeahead {
  position: relative;
  width: 100%;
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
</style>
