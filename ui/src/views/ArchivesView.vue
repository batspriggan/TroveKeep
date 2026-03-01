<template>
  <div>
    <h1>Archives</h1>

    <!-- Import from Folder (collapsible) -->
    <div class="collapsible-header" @click="folderExpanded = !folderExpanded">
      <span class="chevron">{{ folderExpanded ? '▴' : '▾' }}</span>
      Import from Folder
    </div>
    <div v-if="folderExpanded" class="folder-body">
      <p class="muted">Select a folder containing Rebrickable CSV archives. Known files will be detected and imported in parallel.</p>
      <div class="upload-row">
        <input type="file" webkitdirectory multiple @change="onFolderSelected" />
      </div>
      <template v-if="folderFiles.length > 0">
        <p class="detected-label">Detected files ({{ folderFiles.length }} of 5):</p>
        <ul class="folder-file-list">
          <li v-for="entry in folderFiles" :key="entry.name" class="folder-file-row">
            <span class="folder-filename">{{ entry.name }}</span>
            <span v-if="entry.status === 'idle'" class="status-idle">● Ready</span>
            <span v-else-if="entry.status === 'importing'" class="status-importing">⟳ Importing…</span>
            <span v-else-if="entry.status === 'done'" class="status-done">✓ {{ entry.result?.count ?? 0 }} imported</span>
            <span v-else-if="entry.status === 'error'" class="status-error">✗ {{ entry.error }}</span>
          </li>
        </ul>
        <button class="primary" :disabled="bulkImporting" @click="importAll">
          {{ bulkImporting ? 'Importing…' : 'Import All' }}
        </button>
      </template>
      <p v-else class="muted">No recognized archive files detected.</p>
    </div>

    <!-- Archives status table -->
    <table class="archives-table">
      <thead>
        <tr>
          <th>Archive</th>
          <th class="num-col">Count</th>
          <th class="date-col">Last Import</th>
          <th class="action-col"></th>
        </tr>
      </thead>
      <tbody>

        <!-- Colors -->
        <tr class="archive-row" :class="{ 'row-open': expandedImport === 'colors' }">
          <td>
            <span class="archive-label">Colors</span>
            <code class="archive-filename">colors.csv.zip</code>
          </td>
          <td class="num-col">
            <span v-if="statusLoading" class="muted">…</span>
            <strong v-else>{{ status.count.toLocaleString() }}</strong>
          </td>
          <td class="date-col muted">
            <span v-if="statusLoading">…</span>
            <span v-else>{{ status.lastImportedAt ? formatDate(status.lastImportedAt) : 'Never' }}</span>
          </td>
          <td class="action-col">
            <button class="import-btn" @click="toggleImport('colors')">
              Import {{ expandedImport === 'colors' ? '▴' : '▾' }}
            </button>
          </td>
        </tr>
        <tr v-if="expandedImport === 'colors'" class="upload-tr">
          <td colspan="4">
            <div class="inline-upload">
              <input type="file" accept=".zip" @change="onColorsFile" />
              <button class="primary small" :disabled="!colorsFile || reloading" @click="reload">
                {{ reloading ? 'Importing…' : 'Import' }}
              </button>
              <span v-if="error" class="error">{{ error }}</span>
            </div>
          </td>
        </tr>

        <!-- Sets -->
        <tr class="archive-row" :class="{ 'row-open': expandedImport === 'sets' }">
          <td>
            <span class="archive-label">Sets</span>
            <code class="archive-filename">sets.csv.zip</code>
          </td>
          <td class="num-col">
            <span v-if="setsStatusLoading" class="muted">…</span>
            <strong v-else>{{ setsStatus.count.toLocaleString() }}</strong>
          </td>
          <td class="date-col muted">
            <span v-if="setsStatusLoading">…</span>
            <span v-else>{{ setsStatus.lastImportedAt ? formatDate(setsStatus.lastImportedAt) : 'Never' }}</span>
          </td>
          <td class="action-col">
            <button class="import-btn" @click="toggleImport('sets')">
              Import {{ expandedImport === 'sets' ? '▴' : '▾' }}
            </button>
          </td>
        </tr>
        <tr v-if="expandedImport === 'sets'" class="upload-tr">
          <td colspan="4">
            <div class="inline-upload">
              <input type="file" accept=".zip" @change="onSetsFile" />
              <button class="primary small" :disabled="!setsFile || setsReloading" @click="reloadSetsData">
                {{ setsReloading ? 'Importing…' : 'Import' }}
              </button>
              <span v-if="setsError" class="error">{{ setsError }}</span>
            </div>
          </td>
        </tr>

        <!-- Parts -->
        <tr class="archive-row" :class="{ 'row-open': expandedImport === 'parts' }">
          <td>
            <span class="archive-label">Parts</span>
            <code class="archive-filename">parts.csv.zip</code>
          </td>
          <td class="num-col">
            <span v-if="partsStatusLoading" class="muted">…</span>
            <strong v-else>{{ partsStatus.count.toLocaleString() }}</strong>
          </td>
          <td class="date-col muted">
            <span v-if="partsStatusLoading">…</span>
            <span v-else>{{ partsStatus.lastImportedAt ? formatDate(partsStatus.lastImportedAt) : 'Never' }}</span>
          </td>
          <td class="action-col">
            <button class="import-btn" @click="toggleImport('parts')">
              Import {{ expandedImport === 'parts' ? '▴' : '▾' }}
            </button>
          </td>
        </tr>
        <tr v-if="expandedImport === 'parts'" class="upload-tr">
          <td colspan="4">
            <div class="inline-upload">
              <input type="file" accept=".zip" @change="onPartsFile" />
              <button class="primary small" :disabled="!partsFile || partsReloading" @click="reloadPartsData">
                {{ partsReloading ? 'Importing…' : 'Import' }}
              </button>
              <span v-if="partsError" class="error">{{ partsError }}</span>
            </div>
          </td>
        </tr>

        <!-- Part Categories -->
        <tr class="archive-row" :class="{ 'row-open': expandedImport === 'partcategories' }">
          <td>
            <span class="archive-label">Part Categories</span>
            <code class="archive-filename">part_categories.csv.zip</code>
          </td>
          <td class="num-col">
            <span v-if="partCategoriesStatusLoading" class="muted">…</span>
            <strong v-else>{{ partCategoriesStatus.count.toLocaleString() }}</strong>
          </td>
          <td class="date-col muted">
            <span v-if="partCategoriesStatusLoading">…</span>
            <span v-else>{{ partCategoriesStatus.lastImportedAt ? formatDate(partCategoriesStatus.lastImportedAt) : 'Never' }}</span>
          </td>
          <td class="action-col">
            <button class="import-btn" @click="toggleImport('partcategories')">
              Import {{ expandedImport === 'partcategories' ? '▴' : '▾' }}
            </button>
          </td>
        </tr>
        <tr v-if="expandedImport === 'partcategories'" class="upload-tr">
          <td colspan="4">
            <div class="inline-upload">
              <input type="file" accept=".zip" @change="onPartCategoriesFile" />
              <button class="primary small" :disabled="!partCategoriesFile || partCategoriesReloading" @click="reloadPartCategoriesData">
                {{ partCategoriesReloading ? 'Importing…' : 'Import' }}
              </button>
              <span v-if="partCategoriesError" class="error">{{ partCategoriesError }}</span>
            </div>
          </td>
        </tr>

        <!-- Inventory Parts -->
        <tr class="archive-row" :class="{ 'row-open': expandedImport === 'inventoryparts' }">
          <td>
            <span class="archive-label">Inventory Parts</span>
            <code class="archive-filename">inventory_parts.csv.zip</code>
          </td>
          <td class="num-col">
            <span v-if="partsInventoryStatusLoading" class="muted">…</span>
            <strong v-else>{{ partsInventoryStatus.count.toLocaleString() }}</strong>
          </td>
          <td class="date-col muted">
            <span v-if="partsInventoryStatusLoading">…</span>
            <span v-else>{{ partsInventoryStatus.lastImportedAt ? formatDate(partsInventoryStatus.lastImportedAt) : 'Never' }}</span>
          </td>
          <td class="action-col">
            <button class="import-btn" @click="toggleImport('inventoryparts')">
              Import {{ expandedImport === 'inventoryparts' ? '▴' : '▾' }}
            </button>
          </td>
        </tr>
        <tr v-if="expandedImport === 'inventoryparts'" class="upload-tr">
          <td colspan="4">
            <div class="inline-upload">
              <input type="file" accept=".zip" @change="onPartsInventoryFile" />
              <button class="primary small" :disabled="!partsInventoryFile || partsInventoryReloading" @click="reloadPartsInventoryData">
                {{ partsInventoryReloading ? 'Importing…' : 'Import' }}
              </button>
              <span v-if="partsInventoryError" class="error">{{ partsInventoryError }}</span>
            </div>
          </td>
        </tr>

      </tbody>
    </table>

    <!-- Colors list -->
    <div v-if="colors.length" class="list-section">
      <button class="toggle-link" @click="colorsExpanded = !colorsExpanded">
        {{ colorsExpanded ? '▴ Hide colors' : '▾ Show colors' }} ({{ colors.length }})
      </button>
      <table v-if="colorsExpanded" class="data-table">
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
    </div>

    <!-- Part Categories list -->
    <div v-if="partCategories.length" class="list-section">
      <button class="toggle-link" @click="partCategoriesExpanded = !partCategoriesExpanded">
        {{ partCategoriesExpanded ? '▴ Hide part categories' : '▾ Show part categories' }} ({{ partCategories.length }})
      </button>
      <table v-if="partCategoriesExpanded" class="data-table">
        <thead>
          <tr>
            <th>ID</th>
            <th>Name</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="c in partCategories" :key="c.id">
            <td class="id-col">{{ c.id }}</td>
            <td>{{ c.name }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import {
  getColorsStatus, uploadColors, getColorsList,
  getSetsStatus, uploadSets,
  getPartsStatus, uploadParts,
  getPartsInventoryStatus, uploadPartsInventory,
  getPartCategoriesStatus, uploadPartCategories, getPartCategoriesList
} from '../api/archives.js'

// --- Folder import ---
const KNOWN_ARCHIVES = {
  'colors.csv.zip':          uploadColors,
  'sets.csv.zip':            uploadSets,
  'parts.csv.zip':           uploadParts,
  'part_categories.csv.zip': uploadPartCategories,
  'inventory_parts.csv.zip': uploadPartsInventory,
}

const folderExpanded = ref(false)
const folderFiles = ref([])
const bulkImporting = ref(false)

function onFolderSelected(e) {
  const files = Array.from(e.target.files)
  folderFiles.value = files
    .filter(f => KNOWN_ARCHIVES[f.name.toLowerCase()])
    .map(f => ({ name: f.name, file: f, status: 'idle', result: null, error: null }))
}

async function importAll() {
  if (!folderFiles.value.length || bulkImporting.value) return
  bulkImporting.value = true
  folderFiles.value.forEach(f => { f.status = 'importing'; f.result = null; f.error = null })

  await Promise.allSettled(
    folderFiles.value.map(async (entry) => {
      try {
        const fn = KNOWN_ARCHIVES[entry.name.toLowerCase()]
        entry.result = await fn(entry.file)
        entry.status = 'done'
      } catch (e) {
        entry.error = e.message
        entry.status = 'error'
      }
    })
  )

  await Promise.all([
    loadStatus(),
    loadSetsStatus(),
    loadPartsStatus(),
    loadPartsInventoryStatus(),
    loadPartCategoriesStatus(),
  ])
  if (status.value.count > 0) await loadColors()
  if (partCategoriesStatus.value.count > 0) await loadPartCategories()

  bulkImporting.value = false
}

// --- Table accordion ---
const expandedImport = ref(null)

function toggleImport(key) {
  expandedImport.value = expandedImport.value === key ? null : key
}

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

// --- Inventory Parts ---
const partsInventoryStatus = ref({ count: 0, lastImportedAt: null })
const partsInventoryStatusLoading = ref(true)
const partsInventoryReloading = ref(false)
const partsInventoryError = ref('')
const partsInventoryFile = ref(null)

function onPartsInventoryFile(e) {
  partsInventoryFile.value = e.target.files[0] ?? null
  partsInventoryError.value = ''
}

async function loadPartsInventoryStatus() {
  try {
    partsInventoryStatus.value = await getPartsInventoryStatus()
  } catch (e) {
    partsInventoryError.value = e.message
  } finally {
    partsInventoryStatusLoading.value = false
  }
}

async function reloadPartsInventoryData() {
  if (!partsInventoryFile.value) return
  partsInventoryReloading.value = true
  partsInventoryError.value = ''
  try {
    partsInventoryStatus.value = await uploadPartsInventory(partsInventoryFile.value)
  } catch (e) {
    partsInventoryError.value = e.message
  } finally {
    partsInventoryReloading.value = false
  }
}

// --- Part Categories ---
const partCategoriesStatus = ref({ count: 0, lastImportedAt: null })
const partCategories = ref([])
const partCategoriesStatusLoading = ref(true)
const partCategoriesReloading = ref(false)
const partCategoriesError = ref('')
const partCategoriesFile = ref(null)
const partCategoriesExpanded = ref(false)

function onPartCategoriesFile(e) {
  partCategoriesFile.value = e.target.files[0] ?? null
  partCategoriesError.value = ''
}

async function loadPartCategoriesStatus() {
  try {
    partCategoriesStatus.value = await getPartCategoriesStatus()
  } catch (e) {
    partCategoriesError.value = e.message
  } finally {
    partCategoriesStatusLoading.value = false
  }
}

async function loadPartCategories() {
  try {
    partCategories.value = await getPartCategoriesList()
  } catch {
    // silently ignore if not yet imported
  }
}

async function reloadPartCategoriesData() {
  if (!partCategoriesFile.value) return
  partCategoriesReloading.value = true
  partCategoriesError.value = ''
  try {
    partCategoriesStatus.value = await uploadPartCategories(partCategoriesFile.value)
    await loadPartCategories()
  } catch (e) {
    partCategoriesError.value = e.message
  } finally {
    partCategoriesReloading.value = false
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
  await loadPartsInventoryStatus()
  await loadPartCategoriesStatus()
  if (partCategoriesStatus.value.count > 0) await loadPartCategories()
})
</script>

<style scoped>
/* ── Collapsible folder section ── */
.collapsible-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  cursor: pointer;
  padding: 0.6rem 0.75rem;
  margin-bottom: 0.25rem;
  border-radius: 6px;
  font-weight: 600;
  font-size: 0.95rem;
  color: #334155;
  background: #f1f5f9;
  border: 1px solid #e2e8f0;
  user-select: none;
}

.collapsible-header:hover {
  background: #e2e8f0;
}

.chevron {
  font-size: 0.75rem;
  color: #64748b;
}

.folder-body {
  padding: 1rem 1rem 1.25rem;
  margin-bottom: 1.5rem;
  border: 1px solid #e2e8f0;
  border-top: none;
  border-radius: 0 0 6px 6px;
  background: #f8fafc;
}

.upload-row {
  display: flex;
  align-items: center;
  gap: 1rem;
  margin-bottom: 1rem;
  flex-wrap: wrap;
}

.detected-label {
  font-size: 0.875rem;
  color: #475569;
  margin: 0 0 0.5rem;
}

.folder-file-list {
  list-style: none;
  padding: 0;
  margin: 0 0 1rem;
  border: 1px solid #e2e8f0;
  border-radius: 4px;
  background: #fff;
}

.folder-file-row {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 0.4rem 0.75rem;
  border-bottom: 1px solid #f1f5f9;
}

.folder-file-row:last-child {
  border-bottom: none;
}

.folder-filename {
  font-family: monospace;
  font-size: 0.85rem;
  flex: 1;
}

.status-idle      { color: #94a3b8; font-size: 0.85rem; }
.status-importing { color: #3b82f6; font-size: 0.85rem; }
.status-done      { color: #16a34a; font-size: 0.85rem; }
.status-error     { color: #dc2626; font-size: 0.85rem; }

/* ── Archives table ── */
.archives-table {
  width: 100%;
  border-collapse: collapse;
  margin-bottom: 1.5rem;
  font-size: 0.9rem;
}

.archives-table thead th {
  text-align: left;
  padding: 0.5rem 0.75rem;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: #64748b;
  border-bottom: 2px solid #e2e8f0;
}

.archive-row td {
  padding: 0.7rem 0.75rem;
  border-bottom: 1px solid #f1f5f9;
  vertical-align: middle;
}

.archive-row:last-child td {
  border-bottom: none;
}

.archive-row.row-open td {
  border-bottom: none;
  background: #f8fafc;
}

.archive-label {
  display: block;
  font-weight: 500;
  color: #1e293b;
}

.archive-filename {
  display: block;
  font-size: 0.78rem;
  color: #94a3b8;
  margin-top: 1px;
}

.num-col {
  width: 7rem;
  text-align: right;
}

.date-col {
  width: 12rem;
}

.action-col {
  width: 7rem;
  text-align: right;
}

.import-btn {
  background: none;
  border: 1px solid #cbd5e1;
  border-radius: 4px;
  padding: 0.3rem 0.6rem;
  font-size: 0.8rem;
  color: #475569;
  cursor: pointer;
  white-space: nowrap;
}

.import-btn:hover {
  background: #f1f5f9;
  border-color: #94a3b8;
  color: #1e293b;
}

/* ── Inline upload row ── */
.upload-tr td {
  background: #f8fafc;
  border-bottom: 1px solid #e2e8f0;
  padding: 0.75rem 0.75rem 1rem;
}

.inline-upload {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.primary.small {
  padding: 0.3rem 0.75rem;
  font-size: 0.85rem;
}

/* ── Data lists below table ── */
.list-section {
  margin-bottom: 1.5rem;
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

.data-table {
  width: 100%;
}

/* ── Shared ── */
.muted {
  color: #64748b;
  font-size: 0.875rem;
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
