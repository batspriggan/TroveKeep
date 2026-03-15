<!-- Redesigned: 2026-03-06 — Card stack on mobile, dense styled table on desktop -->
<template>
  <div class="page">
    <h1>Lego Sets</h1>

    <section class="add-section">
      <div class="add-tabs">
        <button type="button" class="add-tab" :class="{ active: !form.isMoc }" @click="onMocToggle(false)">Official Set</button>
        <button type="button" class="add-tab" :class="{ active: form.isMoc }" @click="onMocToggle(true)">MOC</button>
      </div>

      <form @submit.prevent="submit">
        <div class="add-row">
          <template v-if="!form.isMoc">
            <div class="form-field add-search-field">
              <label>Search Archive</label>
              <SetArchiveTypeahead @select="onArchiveSelect" />
            </div>

            <div v-if="selectedArchiveSet" class="selected-pill">
              <span><strong>{{ selectedArchiveSet.setNum }}</strong> — {{ selectedArchiveSet.name }}</span>
              <button type="button" class="clear-btn" @click="clearSelected" aria-label="Clear selection">✕</button>
            </div>

            <div v-if="!selectedArchiveSet" class="form-field add-num-field">
              <label>Set Number *</label>
              <input v-model="form.setNumber" required placeholder="e.g. 75313" />
            </div>
          </template>

          <template v-else>
            <div class="form-field add-search-field">
              <label>Description *</label>
              <input v-model="form.description" required placeholder="e.g. My custom spaceship" />
            </div>
            <div class="form-field add-num-field">
              <label>Custom ID</label>
              <input v-model="form.setNumber" placeholder="e.g. MOC-001 (optional)" />
            </div>
          </template>

          <div class="add-submit-row">
            <div class="form-field add-qty-field">
              <label>Qty *</label>
              <input v-model.number="form.quantity" type="number" min="1" required />
            </div>
            <button class="primary add-btn" type="submit" :disabled="form.isMoc ? !form.description : (!selectedArchiveSet && !form.setNumber)">
              {{ form.isMoc ? 'Add MOC' : 'Add Set' }}
            </button>
          </div>
        </div>

        <p v-if="error" class="error">{{ error }}</p>
      </form>
    </section>

    <p v-if="loading" class="loading-msg">Loading…</p>

    <template v-else>
      <div class="filter-bar">
        <input :value="query" @input="onQueryInput" type="search" placeholder="Filter by set number or description…" class="filter-input" />
        <div class="type-filter">
          <button type="button" class="type-btn" :class="{ active: typeFilter === 'all' }" @click="typeFilter = 'all'">All</button>
          <button type="button" class="type-btn" :class="{ active: typeFilter === 'official' }" @click="typeFilter = 'official'">Official</button>
          <button type="button" class="type-btn" :class="{ active: typeFilter === 'moc' }" @click="typeFilter = 'moc'">MOC</button>
        </div>
        <span class="total-count">{{ total }} set{{ total !== 1 ? 's' : '' }}</span>
      </div>

      <!-- Desktop table -->
      <table class="sets-table">
        <thead>
          <tr>
            <th class="th-thumb"></th>
            <th>Set Number</th>
            <th>Description</th>
            <th class="th-qty">Qty</th>
            <th>Storage</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="s in filteredSets" :key="s.id">
            <td class="td-thumb">
              <img v-if="s.imageCached" :src="`/api/sets/${s.id}/image`" class="list-thumb" alt="" />
              <div v-else class="thumb-placeholder"></div>
            </td>
            <td class="td-id">
              <RouterLink :to="`/sets/${s.id}`">{{ s.setNumber || '—' }}</RouterLink>
              <span v-if="s.isMoc" class="moc-badge">MOC</span>
            </td>
            <td class="td-desc">{{ s.description }}</td>
            <td class="td-qty">{{ s.quantity }}</td>
            <td class="td-storage">{{ s.storageAllocations?.length ? `${s.storageAllocations.length} location(s)` : '—' }}</td>
            <td class="td-action">
              <button class="btn-delete" @click="confirmDelete(s)" aria-label="Delete set">Delete</button>
            </td>
          </tr>
          <tr v-if="sets.length === 0">
            <td colspan="6" class="empty-cell">No sets yet.</td>
          </tr>
        </tbody>
      </table>

      <!-- Pagination -->
      <div v-if="totalPages > 1" class="pagination">
        <button class="page-btn" :disabled="page === 1" @click="page--; load()">Prev</button>
        <span class="page-info">Page {{ page }} of {{ totalPages }}</span>
        <button class="page-btn" :disabled="page >= totalPages" @click="page++; load()">Next</button>
      </div>

      <!-- Mobile card stack -->
      <div class="set-cards">
        <p v-if="sets.length === 0" class="empty-msg">No sets yet.</p>
        <RouterLink
          v-for="s in filteredSets"
          :key="s.id"
          :to="`/sets/${s.id}`"
          class="set-card"
        >
          <div class="card-thumb">
            <img v-if="s.imageCached" :src="`/api/sets/${s.id}/image`" alt="" />
            <div v-else class="thumb-placeholder"></div>
          </div>
          <div class="card-body">
            <div class="card-top">
              <span class="card-id">{{ s.setNumber || s.description }}</span>
              <span v-if="s.isMoc" class="moc-badge">MOC</span>
              <span class="card-qty">× {{ s.quantity }}</span>
            </div>
            <div class="card-desc">{{ s.description }}</div>
            <div class="card-meta">
              <span class="card-storage">{{ s.storageAllocations?.length ? `${s.storageAllocations.length} location(s)` : 'Not stored' }}</span>
            </div>
          </div>
          <button class="card-delete" @click.prevent="confirmDelete(s)" aria-label="Delete set">✕</button>
        </RouterLink>
        <div v-if="totalPages > 1" class="pagination pagination-mobile">
          <button class="page-btn" :disabled="page === 1" @click="page--; load()">Prev</button>
          <span class="page-info">Page {{ page }} of {{ totalPages }}</span>
          <button class="page-btn" :disabled="page >= totalPages" @click="page++; load()">Next</button>
        </div>
      </div>
    </template>

    <ConfirmDialog
      :open="!!deleteTarget"
      :message="`Delete set ${deleteTarget?.setNumber}?`"
      @confirm="doDelete"
      @cancel="deleteTarget = null"
    />
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getAllSets, createSet, deleteSet } from '../../api/sets.js'
import ConfirmDialog from '../../components/ConfirmDialog.vue'
import SetArchiveTypeahead from '../../components/SetArchiveTypeahead.vue'

const route = useRoute()
const router = useRouter()

const sets = ref([])
const total = ref(0)
const page = ref(Number(route.query.page) || 1)
const totalPages = ref(1)
const query = ref(route.query.q ?? '')
const typeFilter = ref('all')
const filteredSets = computed(() => {
  if (typeFilter.value === 'moc') return sets.value.filter(s => s.isMoc)
  if (typeFilter.value === 'official') return sets.value.filter(s => !s.isMoc)
  return sets.value
})
const loading = ref(true)
const error = ref('')
const deleteTarget = ref(null)
const form = ref({ setNumber: '', description: '', quantity: 1, isMoc: false })
const selectedArchiveSet = ref(null)

let debounceTimer = null
function onQueryInput(e) {
  query.value = e.target.value
  clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => {
    page.value = 1
    load()
  }, 300)
}

function onMocToggle(isMoc) {
  selectedArchiveSet.value = null
  form.value = { setNumber: '', description: '', quantity: form.value.quantity, isMoc }
}

function onArchiveSelect(s) {
  selectedArchiveSet.value = s
  form.value.setNumber = s.setNum
  form.value.description = s.name
  form.value.photoUrl = s.imgUrl ?? ''
}

function clearSelected() {
  selectedArchiveSet.value = null
  form.value = { setNumber: '', description: '', quantity: 1, isMoc: false }
}

async function load() {
  loading.value = true
  error.value = ''
  router.replace({ query: { ...(query.value ? { q: query.value } : {}), ...(page.value !== 1 ? { page: page.value } : {}) } })
  try {
    const data = await getAllSets(page.value, 50, query.value)
    sets.value = data.items
    total.value = data.total
    totalPages.value = data.totalPages
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

async function submit() {
  error.value = ''
  try {
    await createSet({
      setNumber: form.value.setNumber || null,
      description: form.value.description,
      photoUrl: form.value.photoUrl || null, // still passed for auto-download trigger
      quantity: form.value.quantity,
      isMoc: form.value.isMoc,
    })
    form.value = { setNumber: '', description: '', quantity: 1, isMoc: false }
    selectedArchiveSet.value = null
    await load()
  } catch (e) {
    error.value = e.message
  }
}

function confirmDelete(s) {
  deleteTarget.value = s
}

async function doDelete() {
  error.value = ''
  try {
    await deleteSet(deleteTarget.value.id)
    deleteTarget.value = null
    await load()
  } catch (e) {
    error.value = e.message
    deleteTarget.value = null
  }
}

onMounted(load)
</script>

<style scoped>
.page {
  font-family: var(--font-body);
}

/* ── Add form ── */
.add-section {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 6px;
  padding: var(--space-4);
  margin-bottom: var(--space-4);
}

.add-row {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
}

.add-row .form-field label {
  display: block;
  font-size: var(--text-sm);
  font-weight: 500;
  color: var(--color-text-secondary);
  margin-bottom: var(--space-1);
}

.add-submit-row {
  display: flex;
  gap: var(--space-3);
  align-items: flex-end;
}

.add-qty-field {
  flex: 0 0 80px;
}

.add-btn {
  flex: 1;
  white-space: nowrap;
}

.selected-pill {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  background: var(--color-surface-alt);
  border: 1px solid var(--color-border);
  border-radius: 6px;
  padding: var(--space-2) var(--space-3);
  font-size: var(--text-sm);
}

.clear-btn {
  background: none;
  border: none;
  cursor: pointer;
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  padding: 0 var(--space-1);
  line-height: 1;
  margin-left: auto;
}

.clear-btn:hover {
  color: var(--color-text-secondary);
}

@media (min-width: 640px) {
  .add-row {
    flex-direction: row;
    flex-wrap: wrap;
    align-items: flex-end;
  }

  .add-search-field { flex: 2; min-width: 200px; }
  .add-num-field    { flex: 1.5; min-width: 140px; }

  .selected-pill { flex: 1.5; min-width: 140px; align-self: flex-end; }

  .add-submit-row { flex: 0 0 auto; align-items: flex-end; }
  .add-qty-field  { flex: 0 0 72px; }
  .add-btn        { flex: none; }
}

/* ── Filter ── */
.filter-bar {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  flex-wrap: wrap;
  margin-bottom: var(--space-3);
}

.filter-input {
  flex: 1;
  min-width: 180px;
  max-width: 360px;
  font-size: var(--text-sm);
  border-color: var(--color-border);
  background: var(--color-surface);
}

.type-filter {
  display: flex;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  overflow: hidden;
}

.type-btn {
  padding: 0.3rem var(--space-3);
  font-size: var(--text-sm);
  background: var(--color-surface);
  border: none;
  border-right: 1px solid var(--color-border);
  color: var(--color-text-muted);
  cursor: pointer;
  transition: background var(--transition-fast), color var(--transition-fast);
}

.type-btn:last-child { border-right: none; }

.type-btn:hover { background: var(--color-surface-alt); color: var(--color-text-secondary); }

.type-btn.active {
  background: var(--color-surface-alt);
  color: var(--color-text-primary);
  font-weight: 500;
}

.add-tabs {
  display: flex;
  border-bottom: 1px solid var(--color-border);
  margin-bottom: var(--space-4);
}

.add-tab {
  padding: var(--space-2) var(--space-4);
  font-size: var(--text-sm);
  font-weight: 500;
  background: none;
  border: none;
  border-bottom: 2px solid transparent;
  margin-bottom: -1px;
  color: var(--color-text-muted);
  cursor: pointer;
  transition: color var(--transition-fast), border-color var(--transition-fast);
}

.add-tab:hover { color: var(--color-text-secondary); }

.add-tab.active {
  color: var(--color-accent);
  border-bottom-color: var(--color-accent);
}

.moc-badge {
  display: inline-block;
  font-size: 0.65rem;
  font-weight: 700;
  letter-spacing: 0.04em;
  background: #dbeafe;
  color: #1d4ed8;
  border: 1px solid #93c5fd;
  border-radius: 3px;
  padding: 0 4px;
  vertical-align: middle;
  margin-left: var(--space-1);
}

.loading-msg {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  margin: var(--space-4) 0;
}

.total-count {
  font-size: var(--text-sm);
  color: var(--color-text-muted);
  margin-left: auto;
}

.pagination {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  margin-top: var(--space-3);
  justify-content: center;
}

.pagination-mobile {
  display: none;
}

.page-btn {
  padding: 0.3rem var(--space-3);
  font-size: var(--text-sm);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 4px;
  cursor: pointer;
  color: var(--color-text-secondary);
  transition: background var(--transition-fast);
}

.page-btn:disabled {
  opacity: 0.4;
  cursor: default;
}

.page-btn:not(:disabled):hover {
  background: var(--color-surface-alt);
}

.page-info {
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

/* ── Desktop table ── */
.sets-table {
  font-size: var(--text-sm);
}

.sets-table th {
  font-family: var(--font-display);
  font-size: var(--text-xs);
  font-weight: 600;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  background: var(--color-surface-alt);
}

.th-thumb { width: 44px; }
.th-qty   { width: 60px; text-align: right; }

.td-thumb {
  width: 44px;
  padding: var(--space-2);
  text-align: center;
}

.list-thumb {
  width: 36px;
  height: 36px;
  object-fit: contain;
  display: block;
  margin: 0 auto;
}

.thumb-placeholder {
  width: 36px;
  height: 36px;
  background: var(--color-surface-alt);
  border-radius: 4px;
  margin: 0 auto;
}

.td-id a {
  font-family: var(--font-mono);
  font-size: var(--text-sm);
  font-weight: 500;
  color: var(--color-text-primary);
}

.td-desc {
  color: var(--color-text-secondary);
  font-size: var(--text-sm);
}

.td-qty {
  font-family: var(--font-mono);
  font-size: var(--text-sm);
  text-align: right;
  font-weight: 500;
}

.td-storage {
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.td-action { text-align: right; }

.btn-delete {
  font-size: var(--text-xs);
  padding: 0.2rem 0.55rem;
  background: transparent;
  border: 1px solid var(--color-border);
  color: var(--color-text-secondary);
  border-radius: 4px;
  cursor: pointer;
  transition: background var(--transition-fast), color var(--transition-fast);
}

.btn-delete:hover {
  background: #fee2e2;
  border-color: #fca5a5;
  color: #b91c1c;
}

.empty-cell {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  padding: var(--space-6);
  text-align: center;
}

/* ── Mobile card stack ── */
.set-cards { display: none; }

.empty-msg {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  margin: var(--space-4) 0;
}

@media (max-width: 640px) {
  .sets-table { display: none; }
  .pagination:not(.pagination-mobile) { display: none; }
  .pagination-mobile { display: flex; padding: var(--space-3); background: var(--color-surface); }

  .filter-input { max-width: none; }

  .set-cards {
    display: flex;
    flex-direction: column;
    gap: 1px;
    background: var(--color-border);
    border: 1px solid var(--color-border);
    border-radius: 8px;
    overflow: hidden;
  }

  .set-card {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    background: var(--color-surface);
    padding: var(--space-3);
    text-decoration: none;
    color: inherit;
    min-height: 64px;
    transition: background var(--transition-fast);
  }

  .set-card:hover {
    background: var(--color-surface-alt);
    text-decoration: none;
  }

  .card-thumb {
    flex-shrink: 0;
    width: 44px;
    height: 44px;
  }

  .card-thumb img {
    width: 44px;
    height: 44px;
    object-fit: contain;
    border-radius: 4px;
  }

  .card-thumb .thumb-placeholder {
    width: 44px;
    height: 44px;
    background: var(--color-surface-alt);
    border-radius: 4px;
  }

  .card-body { flex: 1; min-width: 0; }

  .card-top {
    display: flex;
    align-items: baseline;
    gap: var(--space-2);
    margin-bottom: 2px;
  }

  .card-id {
    font-family: var(--font-mono);
    font-size: var(--text-base);
    font-weight: 500;
    color: var(--color-text-primary);
  }

  .card-qty {
    font-family: var(--font-mono);
    font-size: var(--text-sm);
    color: var(--color-text-secondary);
    margin-left: auto;
  }

  .card-desc {
    font-size: var(--text-sm);
    color: var(--color-text-secondary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    margin-bottom: 2px;
  }

  .card-meta {
    font-size: var(--text-xs);
    color: var(--color-text-muted);
  }

  .card-delete {
    flex-shrink: 0;
    width: 32px;
    height: 32px;
    border-radius: 50%;
    border: none;
    background: transparent;
    color: var(--color-text-muted);
    font-size: var(--text-base);
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    transition: background var(--transition-fast), color var(--transition-fast);
    padding: 0;
  }

  .card-delete:hover {
    background: #fee2e2;
    color: #b91c1c;
  }
}
</style>
