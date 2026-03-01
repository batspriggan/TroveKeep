<template>
  <div class="cs-wrap" ref="wrapRef">
    <!-- Trigger -->
    <button type="button" class="cs-trigger" @click="open = !open">
      <span
        class="cs-swatch"
        :style="selectedColor ? { background: '#' + selectedColor.rgb } : {}"
        :class="{ 'cs-swatch--empty': !selectedColor }"
      ></span>
      <span class="cs-label">{{ selectedColor ? selectedColor.name : '— select color —' }}</span>
      <span class="cs-arrow">▾</span>
    </button>

    <!-- Dropdown -->
    <div v-if="open" class="cs-dropdown">
      <div class="cs-filter-wrap">
        <input ref="filterRef" v-model="filterText" class="cs-filter" placeholder="Filter colors…" @click.stop />
      </div>
      <ul class="cs-list">
        <li class="cs-option" @mousedown.prevent="select('')">
          <span class="cs-swatch cs-swatch--empty"></span>
          <span>— select color —</span>
        </li>
        <li
          v-for="c in filteredColors"
          :key="c.uniqueId"
          class="cs-option"
          :class="{ 'cs-option--active': c.uniqueId === modelValue }"
          @mousedown.prevent="select(c.uniqueId)"
        >
          <span class="cs-swatch" :style="{ background: '#' + c.rgb }"></span>
          <span>{{ c.name }}</span>
        </li>
      </ul>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted, onUnmounted, nextTick } from 'vue'

const props = defineProps({
  modelValue: { type: String, default: '' },
  colors: { type: Array, default: () => [] },
})
const emit = defineEmits(['update:modelValue'])

const open = ref(false)
const filterText = ref('')
const wrapRef = ref(null)
const filterRef = ref(null)

const selectedColor = computed(() =>
  props.colors.find(c => c.uniqueId === props.modelValue) ?? null
)

const filteredColors = computed(() => {
  const q = filterText.value.trim().toLowerCase()
  return q ? props.colors.filter(c => c.name.toLowerCase().includes(q)) : props.colors
})

function select(uid) {
  emit('update:modelValue', uid)
  open.value = false
  filterText.value = ''
}

watch(open, async (val) => {
  if (val) {
    await nextTick()
    filterRef.value?.focus()
  } else {
    filterText.value = ''
  }
})

function onClickOutside(e) {
  if (wrapRef.value && !wrapRef.value.contains(e.target)) open.value = false
}

onMounted(() => document.addEventListener('mousedown', onClickOutside))
onUnmounted(() => document.removeEventListener('mousedown', onClickOutside))
</script>

<style scoped>
.cs-wrap {
  position: relative;
  display: inline-block;
}

.cs-trigger {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  min-width: 180px;
  padding: 0.3rem 0.5rem;
  background: #fff;
  border: 1px solid #ccc;
  border-radius: 4px;
  cursor: pointer;
  font-size: inherit;
  font-family: inherit;
  text-align: left;
}

.cs-trigger:hover {
  border-color: #999;
}

.cs-label {
  flex: 1;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.cs-arrow {
  color: #666;
  font-size: 0.8rem;
}

.cs-dropdown {
  position: absolute;
  z-index: 200;
  top: calc(100% + 2px);
  left: 0;
  background: #fff;
  border: 1px solid #ccc;
  border-radius: 4px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  min-width: 220px;
}

.cs-filter-wrap {
  padding: 0.4rem;
  border-bottom: 1px solid #eee;
}

.cs-filter {
  width: 100%;
  box-sizing: border-box;
  padding: 0.25rem 0.4rem;
  border: 1px solid #ccc;
  border-radius: 3px;
  font-size: inherit;
  font-family: inherit;
}

.cs-list {
  max-height: 220px;
  overflow-y: auto;
  margin: 0;
  padding: 0;
  list-style: none;
}

.cs-option {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.3rem 0.6rem;
  cursor: pointer;
}

.cs-option:hover {
  background: #f0f0f0;
}

.cs-option--active {
  background: #e8f0fe;
}

.cs-swatch {
  width: 1rem;
  height: 1rem;
  border-radius: 2px;
  border: 1px solid rgba(0, 0, 0, 0.15);
  flex-shrink: 0;
}

.cs-swatch--empty {
  background: #e0e0e0;
}
</style>
