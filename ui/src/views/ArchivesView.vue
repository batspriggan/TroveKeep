<template>
  <div>
    <h1>Archives</h1>

    <section>
      <h2>Colors <span class="filename">colors.csv.zip</span></h2>

      <p v-if="error" class="error">{{ error }}</p>

      <div class="status-row">
        <span v-if="statusLoading" class="muted">Loading…</span>
        <template v-else>
          <span>
            <strong>{{ status.count }}</strong> color{{ status.count !== 1 ? 's' : '' }} imported
          </span>
          <span class="sep">·</span>
          <span class="muted">
            Last import:
            {{ status.lastImportedAt ? formatDate(status.lastImportedAt) : 'Never imported' }}
          </span>
        </template>
        <button class="primary" :disabled="reloading" @click="reload">
          {{ reloading ? 'Importing…' : 'Reload' }}
        </button>
      </div>

      <template v-if="colors.length">
        <table class="colors-table">
          <thead>
            <tr>
              <th>ID</th>
              <th>Name</th>
              <th>Color</th>
              <th>Transparent</th>
              <th>Years</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="c in colors" :key="c.id">
              <td class="id-col">{{ c.id }}</td>
              <td>{{ c.name }}</td>
              <td>
                <span class="swatch" :style="{ background: '#' + c.rgb }"></span>
                <span class="hex">#{{ c.rgb }}</span>
              </td>
              <td>{{ c.isTrans ? 'Yes' : 'No' }}</td>
              <td>{{ formatYears(c.startYear, c.endYear) }}</td>
            </tr>
          </tbody>
        </table>
      </template>
      <p v-else-if="!statusLoading" class="muted">No colors imported yet. Click Reload to import.</p>
    </section>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { getColorsStatus, reloadColors, getColorsList } from '../api/archives.js'

const status = ref({ count: 0, lastImportedAt: null })
const colors = ref([])
const statusLoading = ref(true)
const reloading = ref(false)
const error = ref('')

async function loadStatus() {
  try {
    status.value = await getColorsStatus()
  } catch (e) {
    error.value = e.message
  } finally {
    statusLoading.value = false
  }
}

async function loadColors() {
  try {
    colors.value = await getColorsList()
  } catch {
    // colors list may be empty if not yet imported — silently ignore
  }
}

async function reload() {
  reloading.value = true
  error.value = ''
  try {
    status.value = await reloadColors()
    await loadColors()
  } catch (e) {
    error.value = e.message
  } finally {
    reloading.value = false
  }
}

function formatDate(iso) {
  return new Date(iso).toLocaleString()
}

function formatYears(y1, y2) {
  if (!y1 && !y2) return '—'
  if (y1 && y2) return `${y1}–${y2}`
  if (y1) return `${y1}–`
  return `–${y2}`
}

onMounted(async () => {
  await loadStatus()
  if (status.value.count > 0) await loadColors()
})
</script>

<style scoped>
.filename {
  font-weight: 400;
  font-size: 0.85rem;
  color: #64748b;
  font-family: monospace;
  margin-left: 0.5rem;
}

.status-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 1.25rem;
  flex-wrap: wrap;
}

.sep {
  color: #94a3b8;
}

.muted {
  color: #64748b;
  font-size: 0.875rem;
}

.colors-table {
  width: 100%;
}

.id-col {
  color: #64748b;
  font-size: 0.85rem;
  width: 4rem;
}

.swatch {
  display: inline-block;
  width: 1rem;
  height: 1rem;
  border-radius: 2px;
  border: 1px solid rgba(0, 0, 0, 0.15);
  vertical-align: middle;
  margin-right: 0.4rem;
}

.hex {
  font-family: monospace;
  font-size: 0.85rem;
}
</style>
