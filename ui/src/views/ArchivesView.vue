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
      </div>

      <div class="upload-row">
        <input ref="colorsInput" type="file" accept=".zip" @change="onColorsFile" />
        <button class="primary" :disabled="!colorsFile || reloading" @click="reload">
          {{ reloading ? 'Importing…' : 'Import' }}
        </button>
      </div>

      <template v-if="colors.length">
        <button class="toggle-link" @click="colorsExpanded = !colorsExpanded">
          {{ colorsExpanded ? '▴ Hide list' : '▾ Show list' }}
        </button>
        <table v-if="colorsExpanded" class="colors-table">
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
      <p v-else-if="!statusLoading" class="muted">No colors imported yet.</p>
    </section>

    <section>
      <h2>Sets <span class="filename">sets.csv.zip</span></h2>

      <p v-if="setsError" class="error">{{ setsError }}</p>

      <div class="status-row">
        <span v-if="setsStatusLoading" class="muted">Loading…</span>
        <template v-else>
          <span>
            <strong>{{ setsStatus.count }}</strong> set{{ setsStatus.count !== 1 ? 's' : '' }} imported
          </span>
          <span class="sep">·</span>
          <span class="muted">
            Last import:
            {{ setsStatus.lastImportedAt ? formatDate(setsStatus.lastImportedAt) : 'Never imported' }}
          </span>
        </template>
      </div>

      <div class="upload-row">
        <input ref="setsInput" type="file" accept=".zip" @change="onSetsFile" />
        <button class="primary" :disabled="!setsFile || setsReloading" @click="reloadSetsData">
          {{ setsReloading ? 'Importing…' : 'Import' }}
        </button>
      </div>
    </section>

    <section>
      <h2>Parts <span class="filename">parts.csv.zip</span></h2>

      <p v-if="partsError" class="error">{{ partsError }}</p>

      <div class="status-row">
        <span v-if="partsStatusLoading" class="muted">Loading…</span>
        <template v-else>
          <span>
            <strong>{{ partsStatus.count }}</strong> part{{ partsStatus.count !== 1 ? 's' : '' }} imported
          </span>
          <span class="sep">·</span>
          <span class="muted">
            Last import:
            {{ partsStatus.lastImportedAt ? formatDate(partsStatus.lastImportedAt) : 'Never imported' }}
          </span>
        </template>
      </div>

      <div class="upload-row">
        <input ref="partsInput" type="file" accept=".csv.zip" @change="onPartsFile" />
        <button class="primary" :disabled="!partsFile || partsReloading" @click="reloadPartsData">
          {{ partsReloading ? 'Importing…' : 'Import' }}
        </button>
      </div>
    </section>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import {
  getColorsStatus, uploadColors, getColorsList,
  getSetsStatus, uploadSets,
  getPartsStatus, uploadParts,
} from '../api/archives.js'

// --- Colors ---
const status = ref({ count: 0, lastImportedAt: null })
const colors = ref([])
const statusLoading = ref(true)
const reloading = ref(false)
const error = ref('')
const colorsFile = ref(null)
const colorsExpanded = ref(false)

function onColorsFile(e) {
  colorsFile.value = e.target.files[0] ?? null
  error.value = ''
}

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
    // silently ignore if not yet imported
  }
}

async function reload() {
  if (!colorsFile.value) return
  reloading.value = true
  error.value = ''
  try {
    status.value = await uploadColors(colorsFile.value)
    await loadColors()
  } catch (e) {
    error.value = e.message
  } finally {
    reloading.value = false
  }
}

// --- Sets ---
const setsStatus = ref({ count: 0, lastImportedAt: null })
const setsStatusLoading = ref(true)
const setsReloading = ref(false)
const setsError = ref('')
const setsFile = ref(null)

function onSetsFile(e) {
  setsFile.value = e.target.files[0] ?? null
  setsError.value = ''
}

async function loadSetsStatus() {
  try {
    setsStatus.value = await getSetsStatus()
  } catch (e) {
    setsError.value = e.message
  } finally {
    setsStatusLoading.value = false
  }
}

async function reloadSetsData() {
  if (!setsFile.value) return
  setsReloading.value = true
  setsError.value = ''
  try {
    setsStatus.value = await uploadSets(setsFile.value)
  } catch (e) {
    setsError.value = e.message
  } finally {
    setsReloading.value = false
  }
}

// --- Parts ---
const partsStatus = ref({ count: 0, lastImportedAt: null })
const partsStatusLoading = ref(true)
const partsReloading = ref(false)
const partsError = ref('')
const partsFile = ref(null)

function onPartsFile(e) {
  partsFile.value = e.target.files[0] ?? null
  partsError.value = ''
}

async function loadPartsStatus() {
  try {
    partsStatus.value = await getPartsStatus()
  } catch (e) {
    partsError.value = e.message
  } finally {
    partsStatusLoading.value = false
  }
}

async function reloadPartsData() {
  if (!partsFile.value) return
  partsReloading.value = true
  partsError.value = ''
  try {
    partsStatus.value = await uploadParts(partsFile.value)
  } catch (e) {
    partsError.value = e.message
  } finally {
    partsReloading.value = false
  }
}

// --- Shared ---
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
  await loadSetsStatus()
  await loadPartsStatus()
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
  margin-bottom: 0.75rem;
  flex-wrap: wrap;
}

.upload-row {
  display: flex;
  align-items: center;
  gap: 1rem;
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

.toggle-link {
  background: none;
  border: none;
  padding: 0;
  color: #3b82f6;
  font-size: 0.875rem;
  cursor: pointer;
  margin-bottom: 0.75rem;
  display: block;
}

.toggle-link:hover {
  text-decoration: underline;
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
