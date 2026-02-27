<template>
  <div>
    <RouterLink class="back-link" to="/bulkpieces">← Back to Bulk Pieces</RouterLink>

    <p v-if="loading">Loading…</p>
    <p v-else-if="error" class="error">{{ error }}</p>

    <template v-else-if="piece">
      <h1>{{ piece.legoId }} — {{ piece.legoColor }}</h1>

      <div class="card">
        <h2>Edit Piece</h2>
        <form class="form-row" @submit.prevent="submitEdit">
          <div class="form-field">
            <label>Lego ID *</label>
            <input v-model="editForm.legoId" required />
          </div>
          <div class="form-field">
            <label>Color *</label>
            <input v-model="editForm.legoColor" required />
          </div>
          <div class="form-field">
            <label>Description *</label>
            <input v-model="editForm.description" required />
          </div>
          <div class="form-field" style="max-width: 80px">
            <label>Qty *</label>
            <input v-model.number="editForm.quantity" type="number" min="1" required />
          </div>
          <button class="primary" type="submit">Save</button>
        </form>
        <p v-if="editError" class="error">{{ editError }}</p>
      </div>

      <div class="card">
        <h2>Storage</h2>

        <table v-if="piece.storageAllocations && piece.storageAllocations.length">
          <thead>
            <tr>
              <th>Location</th>
              <th>Type</th>
              <th>Qty</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="a in piece.storageAllocations" :key="a.storageId">
              <td>
                <RouterLink v-if="a.storageType === 'Box'" :to="`/boxes/${a.storageId}`">
                  {{ boxNameMap[a.storageId] ?? a.storageId }}
                </RouterLink>
                <RouterLink v-else :to="`/drawers/${a.storageId}`">
                  {{ drawerLabelMap[a.storageId] ?? a.storageId }}
                </RouterLink>
              </td>
              <td>{{ a.storageType }}</td>
              <td>{{ a.quantity }}</td>
              <td><button class="danger" @click="deallocate(a.storageId)">Remove</button></td>
            </tr>
          </tbody>
        </table>
        <p v-else>Not stored anywhere.</p>

        <p style="margin-top: 0.5rem">Unallocated: {{ unallocated }} of {{ piece.quantity }}</p>

        <div class="form-row" style="margin-top: 0.75rem; align-items: flex-start; flex-direction: column; gap: 0.75rem">
          <form class="form-row" style="width:100%" @submit.prevent="submitBoxStorage">
            <div class="form-field">
              <label>Box</label>
              <select v-model="selectedBoxId">
                <option value="">— select box —</option>
                <option v-for="b in boxes" :key="b.id" :value="b.id">{{ b.name }}</option>
              </select>
            </div>
            <div class="form-field" style="max-width: 80px">
              <label>Qty</label>
              <input v-model.number="boxAllocQty" type="number" min="1" required />
            </div>
            <button class="primary" type="submit" :disabled="!selectedBoxId || boxAllocQty < 1">Allocate to Box</button>
          </form>

          <form class="form-row" style="width:100%" @submit.prevent="submitDrawerStorage">
            <div class="form-field">
              <label>Drawer</label>
              <select v-model="selectedDrawerId">
                <option value="">— select drawer —</option>
                <option v-for="d in drawers" :key="d.id" :value="d.id">
                  {{ d.label || `Position ${d.position}` }} ({{ d.drawerContainerId }})
                </option>
              </select>
            </div>
            <div class="form-field" style="max-width: 80px">
              <label>Qty</label>
              <input v-model.number="drawerAllocQty" type="number" min="1" required />
            </div>
            <button class="primary" type="submit" :disabled="!selectedDrawerId || drawerAllocQty < 1">Allocate to Drawer</button>
          </form>

          <button
            v-if="piece.storageAllocations && piece.storageAllocations.length"
            type="button"
            class="danger"
            @click="clearStorage"
          >Clear All</button>
        </div>
        <p v-if="storageError" class="error">{{ storageError }}</p>
      </div>

      <div class="card">
        <button class="danger" @click="showConfirm = true">Delete Piece</button>
      </div>
    </template>

    <ConfirmDialog
      :open="showConfirm"
      :message="`Delete ${piece?.legoId} (${piece?.legoColor})?`"
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
  allocatePieceToBox, allocatePieceToDrawer, deallocatePieceStorage, clearPieceStorage,
} from '../../api/bulkpieces.js'
import { getAllBoxes } from '../../api/boxes.js'
import { getAllDrawerContainers, getDrawerContainerDrawers } from '../../api/drawercontainers.js'
import ConfirmDialog from '../../components/ConfirmDialog.vue'

const route = useRoute()
const router = useRouter()
const id = route.params.id

const piece = ref(null)
const boxes = ref([])
const drawers = ref([])
const loading = ref(true)
const error = ref('')
const editError = ref('')
const storageError = ref('')
const showConfirm = ref(false)
const selectedBoxId = ref('')
const selectedDrawerId = ref('')
const boxAllocQty = ref(1)
const drawerAllocQty = ref(1)
const editForm = ref({ legoId: '', legoColor: '', description: '', quantity: 1 })

const boxNameMap = computed(() => Object.fromEntries(boxes.value.map(b => [b.id, b.name])))
const drawerLabelMap = computed(() =>
  Object.fromEntries(drawers.value.map(d => [d.id, d.label || `Position ${d.position}`])))

const unallocated = computed(() => {
  if (!piece.value) return 0
  const allocated = (piece.value.storageAllocations ?? []).reduce((sum, a) => sum + a.quantity, 0)
  return piece.value.quantity - allocated
})

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [p, allBoxes, allContainers] = await Promise.all([
      getBulkPiece(id),
      getAllBoxes(),
      getAllDrawerContainers(),
    ])
    piece.value = p
    boxes.value = allBoxes
    editForm.value = { legoId: p.legoId, legoColor: p.legoColor, description: p.description, quantity: p.quantity }

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
  try {
    const updated = await updateBulkPiece(id, { legoId: editForm.value.legoId, legoColor: editForm.value.legoColor, description: editForm.value.description, quantity: editForm.value.quantity })
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
    const updated = await allocatePieceToDrawer(id, selectedDrawerId.value, drawerAllocQty.value)
    piece.value = updated
    selectedDrawerId.value = ''
    drawerAllocQty.value = 1
  } catch (e) {
    storageError.value = e.message
  }
}

async function deallocate(storageId) {
  storageError.value = ''
  try {
    const updated = await deallocatePieceStorage(id, storageId)
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
