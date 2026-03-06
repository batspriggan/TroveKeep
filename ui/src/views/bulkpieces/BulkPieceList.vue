<!-- Redesigned: 2026-03-06 — Card stack on mobile, dense styled table on desktop -->
<template>
  <div class="page">
    <h1>Bulk Pieces</h1>

    <section class="add-section">
      <form @submit.prevent="submit">
        <div class="add-row">
          <div class="form-field add-id-field">
            <label>Lego ID *</label>
            <PartArchiveTypeahead v-model="form.legoId" @select="onPartSelect" />
          </div>
          <div class="form-field add-color-field">
            <label>Color *</label>
            <ColorSelect v-model="form.legoColorUid" :colors="colors" />
          </div>
          <div class="form-field add-desc-field">
            <label>Description *</label>
            <input v-model="form.description" required placeholder="Description" />
          </div>
          <div class="add-submit-row">
            <div class="form-field add-qty-field">
              <label>Qty *</label>
              <input v-model.number="form.quantity" type="number" min="1" required />
            </div>
            <button class="primary add-btn" type="submit">Add Piece</button>
          </div>
        </div>
        <p v-if="error" class="error">{{ error }}</p>
      </form>
    </section>

    <p v-if="loading" class="loading-msg">Loading…</p>

    <template v-else>
      <div class="filter-bar">
        <input v-model="filterText" type="search" placeholder="Filter by ID or description…" class="filter-input" />
      </div>

      <!-- Desktop table -->
      <table class="pieces-table">
        <thead>
          <tr>
            <th class="th-thumb"></th>
            <th>Lego ID</th>
            <th>Color</th>
            <th>Description</th>
            <th class="th-qty">Qty</th>
            <th>Storage</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="p in filteredPieces" :key="p.id">
            <td class="td-thumb">
              <img v-if="p.imageCached" :src="`/api/bulkpieces/${p.id}/image`" class="list-thumb" alt="" />
              <div v-else class="thumb-placeholder"></div>
            </td>
            <td class="td-id">
              <RouterLink :to="`/bulkpieces/${p.id}`">{{ p.legoId }}</RouterLink>
            </td>
            <td class="td-color">
              <span v-if="p.legoColorRgb" class="swatch" :style="{ background: '#' + p.legoColorRgb }"></span>
              <span class="color-name">{{ p.legoColorName ?? `#${p.legoColorId}` }}</span>
            </td>
            <td class="td-desc">{{ p.description }}</td>
            <td class="td-qty">{{ p.quantity }}</td>
            <td class="td-storage">{{ (p.storageAllocations?.length) ? `${p.storageAllocations.length} location(s)` : '—' }}</td>
            <td class="td-action">
              <button class="btn-delete" @click="confirmDelete(p)" aria-label="Delete piece">Delete</button>
            </td>
          </tr>
          <tr v-if="pieces.length === 0">
            <td colspan="7" class="empty-cell">No bulk pieces yet.</td>
          </tr>
          <tr v-else-if="filteredPieces.length === 0">
            <td colspan="7" class="empty-cell">No results match your filter.</td>
          </tr>
        </tbody>
      </table>

      <!-- Mobile card stack -->
      <div class="piece-cards">
        <p v-if="pieces.length === 0" class="empty-msg">No bulk pieces yet.</p>
        <p v-else-if="filteredPieces.length === 0" class="empty-msg">No results match your filter.</p>
        <RouterLink
          v-for="p in filteredPieces"
          :key="p.id"
          :to="`/bulkpieces/${p.id}`"
          class="piece-card"
        >
          <div class="card-thumb">
            <img v-if="p.imageCached" :src="`/api/bulkpieces/${p.id}/image`" alt="" />
            <div v-else class="thumb-placeholder"></div>
          </div>
          <div class="card-body">
            <div class="card-top">
              <span class="card-id">{{ p.legoId }}</span>
              <span class="card-qty">× {{ p.quantity }}</span>
            </div>
            <div class="card-desc">{{ p.description }}</div>
            <div class="card-meta">
              <span class="card-color">
                <span v-if="p.legoColorRgb" class="swatch" :style="{ background: '#' + p.legoColorRgb }"></span>
                {{ p.legoColorName ?? `#${p.legoColorId}` }}
              </span>
              <span class="card-storage">{{ (p.storageAllocations?.length) ? `${p.storageAllocations.length} location(s)` : 'Not stored' }}</span>
            </div>
          </div>
          <button
            class="card-delete"
            @click.prevent="confirmDelete(p)"
            aria-label="Delete piece"
          >✕</button>
        </RouterLink>
      </div>
    </template>

    <ConfirmDialog
      :open="!!deleteTarget"
      :message="`Delete ${deleteTarget?.legoId} (${deleteTarget?.legoColorName ?? deleteTarget?.legoColorId})?`"
      @confirm="doDelete"
      @cancel="deleteTarget = null"
    />
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { getAllBulkPieces, createBulkPiece, deleteBulkPiece } from '../../api/bulkpieces.js'
import { getColorsList } from '../../api/archives.js'
import ConfirmDialog from '../../components/ConfirmDialog.vue'
import ColorSelect from '../../components/ColorSelect.vue'
import PartArchiveTypeahead from '../../components/PartArchiveTypeahead.vue'

const pieces = ref([])
const colors = ref([])
const filterText = ref('')
const filteredPieces = computed(() => {
  const q = filterText.value.trim().toLowerCase()
  if (!q) return pieces.value
  return pieces.value.filter(p =>
    p.legoId.toLowerCase().includes(q) ||
    p.description.toLowerCase().includes(q)
  )
})
const loading = ref(true)
const error = ref('')
const deleteTarget = ref(null)
const form = ref({ legoId: '', legoColorUid: '', description: '', quantity: 1 })

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [allPieces, allColors] = await Promise.all([getAllBulkPieces(), getColorsList()])
    pieces.value = allPieces
    colors.value = allColors
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

function onPartSelect(part) {
  form.value.description = part.name
}

async function submit() {
  error.value = ''
  const missing = []
  if (!form.value.legoId.trim()) missing.push('Lego ID')
  if (!form.value.legoColorUid) missing.push('Color (if the list is empty, import colors in the Archives)')
  if (!form.value.description.trim()) missing.push('Description')
  if (missing.length) {
    error.value = `Required fields missing: ${missing.join(', ')}.`
    return
  }
  const legoColorId = colors.value.find(c => c.uniqueId === form.value.legoColorUid)?.id
  try {
    await createBulkPiece({ legoId: form.value.legoId, legoColorId, description: form.value.description, quantity: form.value.quantity })
    form.value = { legoId: '', legoColorUid: '', description: '', quantity: 1 }
    await load()
  } catch (e) {
    error.value = e.message
  }
}

function confirmDelete(p) {
  deleteTarget.value = p
}

async function doDelete() {
  error.value = ''
  try {
    await deleteBulkPiece(deleteTarget.value.id)
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

.add-row .form-field {
  display: flex;
  flex-direction: column;
}

.add-row .form-field label {
  font-size: var(--text-sm);
  font-weight: 500;
  color: var(--color-text-secondary);
  margin-bottom: var(--space-1);
}

/* qty + button share a row */
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

/* Desktop: switch to horizontal layout */
@media (min-width: 640px) {
  .add-row {
    flex-direction: row;
    flex-wrap: wrap;
    align-items: flex-end;
  }

  .add-id-field    { flex: 2.5; min-width: 260px; }
  .add-color-field { flex: 1; min-width: 140px; }
  .add-desc-field  { flex: 2; min-width: 160px; }

  .add-submit-row {
    flex: 0 0 auto;
    align-items: flex-end;
  }

  .add-qty-field { flex: 0 0 72px; }
  .add-btn { flex: none; }
}

/* ── Filter ── */
.filter-bar {
  margin-bottom: var(--space-3);
}

.filter-input {
  width: 100%;
  max-width: 360px;
  font-size: var(--text-sm);
  border-color: var(--color-border);
  background: var(--color-surface);
}

/* ── Desktop table ── */
.pieces-table {
  font-size: var(--text-sm);
}

.pieces-table th {
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
  text-align: center;
  padding: var(--space-2) var(--space-2);
}

.list-thumb {
  width: 32px;
  height: 32px;
  object-fit: contain;
  display: block;
  margin: 0 auto;
}

.thumb-placeholder {
  width: 32px;
  height: 32px;
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

.td-color {
  white-space: nowrap;
}

.color-name {
  font-size: var(--text-sm);
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

.td-action {
  text-align: right;
}

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

.swatch {
  display: inline-block;
  width: 10px;
  height: 10px;
  border-radius: 2px;
  border: 1px solid rgba(0, 0, 0, 0.15);
  vertical-align: middle;
  margin-right: var(--space-1);
  flex-shrink: 0;
}

.empty-cell {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  padding: var(--space-6);
  text-align: center;
}

/* ── Mobile card stack ── */
.piece-cards {
  display: none;
}

.loading-msg {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  margin: var(--space-4) 0;
}

.empty-msg {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  margin: var(--space-4) 0;
}

@media (max-width: 640px) {
  .pieces-table {
    display: none;
  }

  .filter-input {
    max-width: 100%;
  }

  .add-row .desc-field {
    flex: 1 1 100%;
  }

  /* Card stack */
  .piece-cards {
    display: flex;
    flex-direction: column;
    gap: 1px;
    background: var(--color-border);
    border: 1px solid var(--color-border);
    border-radius: 8px;
    overflow: hidden;
  }

  .piece-card {
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

  .piece-card:hover {
    background: var(--color-surface-alt);
    text-decoration: none;
  }

  .card-thumb {
    flex-shrink: 0;
    width: 40px;
    height: 40px;
  }

  .card-thumb img {
    width: 40px;
    height: 40px;
    object-fit: contain;
    border-radius: 4px;
  }

  .card-thumb .thumb-placeholder {
    width: 40px;
    height: 40px;
    background: var(--color-surface-alt);
    border-radius: 4px;
    margin: 0;
  }

  .card-body {
    flex: 1;
    min-width: 0;
  }

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
    display: flex;
    align-items: center;
    gap: var(--space-3);
    font-size: var(--text-xs);
    color: var(--color-text-muted);
  }

  .card-color {
    display: flex;
    align-items: center;
    gap: 4px;
  }

  .card-storage {
    margin-left: auto;
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
