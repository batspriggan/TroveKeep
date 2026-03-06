<!-- Redesigned: 2026-03-06 — Two-column desktop layout; better header and form styling -->
<template>
  <div class="page">
    <RouterLink class="back-link" to="/bulkpieces">← Back to Bulk Pieces</RouterLink>

    <p v-if="loading" class="loading-msg">Loading…</p>
    <p v-else-if="error" class="error">{{ error }}</p>

    <template v-else-if="piece">
      <header class="piece-header">
        <div class="piece-title">
          <span class="piece-id">{{ piece.legoId }}</span>
          <span class="piece-sep">—</span>
          <span class="piece-name">{{ piece.description }}</span>
        </div>
        <div class="piece-color-badge">
          <span v-if="piece.legoColorRgb" class="color-swatch" :style="{ background: '#' + piece.legoColorRgb }"></span>
          <span class="color-label">{{ piece.legoColorName ?? `Color #${piece.legoColorId}` }}</span>
        </div>
      </header>

      <div class="detail-layout">
        <!-- Left column: image + edit -->
        <div class="col-left">
          <div v-if="piece.imageCached" class="card piece-image-card">
            <img :src="`/api/bulkpieces/${id}/image`" class="piece-image" alt="" />
          </div>

          <div class="card">
            <h2>Edit Piece</h2>
            <div class="edit-grid">
              <div class="form-field">
                <label>Lego ID *</label>
                <input v-model="editForm.legoId" required />
              </div>
              <div class="form-field">
                <label>Color *</label>
                <ColorSelect v-model="editForm.legoColorUid" :colors="colors" />
              </div>
              <div class="form-field edit-desc-field">
                <label>Description *</label>
                <input v-model="editForm.description" required />
              </div>
              <div class="form-field edit-qty-field">
                <label>Qty *</label>
                <input v-model.number="editForm.quantity" type="number" min="1" required />
              </div>
            </div>
            <div class="edit-footer">
              <button class="primary" @click="submitEdit">Save</button>
              <p v-if="editError" class="error inline-error">{{ editError }}</p>
            </div>
          </div>

          <div class="card danger-zone">
            <button class="danger" @click="showConfirm = true">Delete Piece</button>
          </div>
        </div>

        <!-- Right column: storage -->
        <div class="col-right">
          <div class="card">
            <div class="storage-header">
              <h2>Storage</h2>
              <div class="alloc-summary">
                <span class="alloc-pill" :class="{ 'alloc-full': fullyAllocated }">
                  {{ unallocated }} unallocated of {{ piece.quantity }}
                </span>
              </div>
            </div>

            <table v-if="piece.storageAllocations?.length" class="alloc-table">
              <thead>
                <tr>
                  <th>Location</th>
                  <th>Type</th>
                  <th class="th-qty">Qty</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="a in piece.storageAllocations" :key="`${a.storageId}-${a.storagePosition ?? ''}`">
                  <td>
                    <RouterLink v-if="a.storageType === 'Box'" :to="`/boxes/${a.storageId}`">
                      {{ boxNameMap[a.storageId] ?? a.storageId }}
                    </RouterLink>
                    <RouterLink v-else :to="`/drawers/${a.storageId}/${a.storagePosition}`">
                      {{ containerNameMap[a.storageId] ? `${containerNameMap[a.storageId]} — Pos ${a.storagePosition}` : a.storageId }}
                    </RouterLink>
                  </td>
                  <td class="td-type">{{ a.storageType }}</td>
                  <td class="td-qty">{{ a.quantity }}</td>
                  <td class="td-action">
                    <button class="btn-remove" @click="deallocate(a)">Remove</button>
                  </td>
                </tr>
              </tbody>
            </table>
            <p v-else class="no-storage">Not stored anywhere yet.</p>

            <fieldset class="alloc-forms" :disabled="fullyAllocated">
              <div class="alloc-form-group">
                <p class="alloc-form-label">Assign to Box</p>
                <form class="alloc-row" @submit.prevent="submitBoxStorage">
                  <select v-model="selectedBoxId" class="alloc-select">
                    <option value="">— select box —</option>
                    <option v-for="b in boxes" :key="b.id" :value="b.id">{{ b.name }}</option>
                  </select>
                  <input v-model.number="boxAllocQty" type="number" min="1" required class="alloc-qty" />
                  <button class="primary" type="submit" :disabled="!selectedBoxId || boxAllocQty < 1">Assign</button>
                </form>
              </div>

              <div class="alloc-form-group">
                <p class="alloc-form-label">Assign to Drawer</p>
                <form class="alloc-row" @submit.prevent="submitDrawerStorage">
                  <select v-model="selectedDrawer" class="alloc-select">
                    <option :value="null">— select drawer —</option>
                    <option v-for="d in drawers" :key="`${d.drawerContainerId}-${d.position}`" :value="d">
                      {{ d.label || `Position ${d.position}` }} ({{ containerNameMap[d.drawerContainerId] ?? d.drawerContainerId }})
                    </option>
                  </select>
                  <input v-model.number="drawerAllocQty" type="number" min="1" required class="alloc-qty" />
                  <button class="primary" type="submit" :disabled="!selectedDrawer || drawerAllocQty < 1">Assign</button>
                </form>
              </div>
            </fieldset>

            <p v-if="fullyAllocated" class="alloc-full-msg">All quantity allocated — remove a location to free up space.</p>

            <div v-if="piece.storageAllocations?.length" class="clear-row">
              <button type="button" class="danger" @click="clearStorage">Clear All Storage</button>
            </div>

            <p v-if="storageError" class="error">{{ storageError }}</p>
          </div>
        </div>
      </div>
    </template>

    <ConfirmDialog
      :open="showConfirm"
      :message="`Delete ${piece?.legoId} (${piece?.legoColorName ?? piece?.legoColorId})?`"
      @confirm="doDelete"
      @cancel="showConfirm = false"
    />
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  getBulkPiece, updateBulkPiece, deleteBulkPiece,
  allocatePieceToBox, allocatePieceToDrawer, deallocatePieceFromBox, deallocatePieceFromDrawer, clearPieceStorage,
} from '../../api/bulkpieces.js'
import { getAllBoxes } from '../../api/boxes.js'
import { getAllDrawerContainers, getDrawerContainerDrawers } from '../../api/drawercontainers.js'
import { getColorsList } from '../../api/archives.js'
import ConfirmDialog from '../../components/ConfirmDialog.vue'
import ColorSelect from '../../components/ColorSelect.vue'

const route = useRoute()
const router = useRouter()
const id = route.params.id

const piece = ref(null)
const colors = ref([])
const boxes = ref([])
const containers = ref([])
const drawers = ref([])
const loading = ref(true)
const error = ref('')
const editError = ref('')
const storageError = ref('')
const showConfirm = ref(false)
const selectedBoxId = ref('')
const selectedDrawer = ref(null)
const boxAllocQty = ref(1)
const drawerAllocQty = ref(1)
const editForm = ref({ legoId: '', legoColorUid: '', description: '', quantity: 1 })

const boxNameMap = computed(() => Object.fromEntries(boxes.value.map(b => [b.id, b.name])))
const containerNameMap = computed(() => Object.fromEntries(containers.value.map(c => [c.id, c.name])))

const unallocated = computed(() => {
  if (!piece.value) return 0
  const allocated = (piece.value.storageAllocations ?? []).reduce((sum, a) => sum + a.quantity, 0)
  return piece.value.quantity - allocated
})
const fullyAllocated = computed(() => unallocated.value === 0)

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [p, allBoxes, allContainers, allColors] = await Promise.all([
      getBulkPiece(id),
      getAllBoxes(),
      getAllDrawerContainers(),
      getColorsList(),
    ])
    piece.value = p
    boxes.value = allBoxes
    containers.value = allContainers
    colors.value = allColors
    const legoColorUid = allColors.find(c => c.id === p.legoColorId)?.uniqueId ?? ''
    editForm.value = { legoId: p.legoId, legoColorUid, description: p.description, quantity: p.quantity }

    const containerDetails = await Promise.all(allContainers.map((c) => getDrawerContainerDrawers(c.id)))
    drawers.value = containerDetails.flatMap((c) => c.drawers ?? [])
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

async function submitEdit() {
  editError.value = ''
  const legoColorId = colors.value.find(c => c.uniqueId === editForm.value.legoColorUid)?.id
  if (!legoColorId && legoColorId !== 0) {
    editError.value = 'Required fields missing: Color.'
    return
  }
  try {
    const updated = await updateBulkPiece(id, {
      legoId: editForm.value.legoId,
      legoColorId,
      description: editForm.value.description,
      quantity: editForm.value.quantity,
    })
    piece.value = updated
  } catch (e) {
    editError.value = e.message
  }
}

async function submitBoxStorage() {
  storageError.value = ''
  try {
    const updated = await allocatePieceToBox(id, selectedBoxId.value, boxAllocQty.value)
    piece.value = updated
    selectedBoxId.value = ''
    boxAllocQty.value = 1
  } catch (e) {
    storageError.value = e.message
  }
}

async function submitDrawerStorage() {
  storageError.value = ''
  try {
    const updated = await allocatePieceToDrawer(
      id, selectedDrawer.value.drawerContainerId, selectedDrawer.value.position, drawerAllocQty.value)
    piece.value = updated
    selectedDrawer.value = null
    drawerAllocQty.value = 1
  } catch (e) {
    storageError.value = e.message
  }
}

async function deallocate(a) {
  storageError.value = ''
  try {
    const updated = a.storageType === 'Box'
      ? await deallocatePieceFromBox(id, a.storageId)
      : await deallocatePieceFromDrawer(id, a.storageId, a.storagePosition)
    piece.value = updated
  } catch (e) {
    storageError.value = e.message
  }
}

async function clearStorage() {
  storageError.value = ''
  try {
    const updated = await clearPieceStorage(id)
    piece.value = updated
  } catch (e) {
    storageError.value = e.message
  }
}

async function doDelete() {
  try {
    await deleteBulkPiece(id)
    router.push('/bulkpieces')
  } catch (e) {
    error.value = e.message
    showConfirm.value = false
  }
}

onMounted(load)
</script>

<style scoped>
.page {
  font-family: var(--font-body);
}

.loading-msg {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  margin: var(--space-4) 0;
}

/* ── Header ── */
.piece-header {
  margin-bottom: var(--space-4);
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: var(--space-3);
}

.piece-title {
  display: flex;
  align-items: baseline;
  gap: var(--space-2);
  flex-wrap: wrap;
}

.piece-id {
  font-family: var(--font-mono);
  font-size: var(--text-2xl);
  font-weight: 500;
  color: var(--color-text-primary);
}

.piece-sep {
  color: var(--color-text-muted);
  font-size: var(--text-xl);
}

.piece-name {
  font-size: var(--text-xl);
  color: var(--color-text-secondary);
}

.piece-color-badge {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  background: var(--color-surface-alt);
  border: 1px solid var(--color-border);
  border-radius: 20px;
  padding: var(--space-1) var(--space-3);
}

.color-swatch {
  width: 14px;
  height: 14px;
  border-radius: 3px;
  border: 1px solid rgba(0,0,0,0.15);
  flex-shrink: 0;
}

.color-label {
  font-size: var(--text-sm);
  color: var(--color-text-secondary);
}

/* ── Two-column layout ── */
.detail-layout {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

@media (min-width: 640px) {
  .detail-layout {
    display: grid;
    grid-template-columns: 1fr 1.6fr;
    align-items: start;
  }
}

/* ── Image card ── */
.piece-image-card {
  display: flex;
  justify-content: center;
  padding: var(--space-4);
}

.piece-image {
  max-height: 180px;
  object-fit: contain;
}

/* ── Edit card ── */
.edit-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--space-3);
  margin-bottom: var(--space-3);
}

.edit-desc-field {
  grid-column: 1 / -1;
}

.edit-qty-field {
  grid-column: 1;
}

.edit-grid .form-field label {
  display: block;
  font-size: var(--text-sm);
  font-weight: 500;
  color: var(--color-text-secondary);
  margin-bottom: var(--space-1);
}

.edit-footer {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}

.inline-error {
  margin: 0;
}

/* ── Danger zone ── */
.danger-zone {
  border-color: #fee2e2;
}

/* ── Storage card ── */
.storage-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: var(--space-3);
}

.storage-header h2 {
  margin: 0;
}

.alloc-pill {
  font-size: var(--text-xs);
  font-family: var(--font-mono);
  background: var(--color-surface-alt);
  border: 1px solid var(--color-border);
  border-radius: 20px;
  padding: 2px var(--space-3);
  color: var(--color-text-secondary);
}

.alloc-pill.alloc-full {
  background: var(--color-accent-soft);
  border-color: #fca5a5;
  color: var(--color-accent);
}

.alloc-table {
  font-size: var(--text-sm);
  margin-bottom: var(--space-4);
}

.alloc-table th {
  font-size: var(--text-xs);
  font-weight: 600;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  background: var(--color-surface-alt);
}

.th-qty { width: 50px; text-align: right; }
.td-type { color: var(--color-text-muted); font-size: var(--text-xs); }
.td-qty { font-family: var(--font-mono); text-align: right; }
.td-action { text-align: right; }

.btn-remove {
  font-size: var(--text-xs);
  padding: 0.2rem 0.5rem;
  background: transparent;
  border: 1px solid var(--color-border);
  color: var(--color-text-secondary);
  border-radius: 4px;
  cursor: pointer;
  transition: background var(--transition-fast), color var(--transition-fast);
}

.btn-remove:hover {
  background: #fee2e2;
  border-color: #fca5a5;
  color: #b91c1c;
}

.no-storage {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  margin-bottom: var(--space-4);
}

/* ── Allocation forms ── */
.alloc-forms {
  border: none;
  padding: 0;
  margin: 0;
}

.alloc-forms:disabled {
  opacity: 0.4;
  pointer-events: none;
}

.alloc-form-group {
  margin-bottom: var(--space-3);
}

.alloc-form-label {
  font-size: var(--text-xs);
  font-weight: 600;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  margin-bottom: var(--space-2);
}

.alloc-row {
  display: flex;
  gap: var(--space-2);
  align-items: center;
}

.alloc-select {
  flex: 1;
  font-size: var(--text-sm);
  min-width: 0;
}

.alloc-qty {
  width: 64px;
  flex-shrink: 0;
  font-family: var(--font-mono);
  font-size: var(--text-sm);
  text-align: right;
}

.alloc-full-msg {
  font-size: var(--text-sm);
  color: var(--color-text-muted);
  margin: var(--space-2) 0;
  font-style: italic;
}

.clear-row {
  margin-top: var(--space-3);
  padding-top: var(--space-3);
  border-top: 1px solid var(--color-border);
}
</style>
