<template>
  <div>
    <RouterLink class="back-link" to="/bulkpieces">← Back to Bulk Pieces</RouterLink>

    <p v-if="loading">Loading…</p>
    <p v-else-if="error" class="error">{{ error }}</p>

    <template v-else-if="piece">
      <h1>
        {{ piece.legoId }} —
        <span v-if="piece.legoColorRgb" class="swatch-title" :style="{ background: '#' + piece.legoColorRgb }"></span>
        {{ piece.legoColorName ?? `Color #${piece.legoColorId}` }}
      </h1>

      <div v-if="piece.imageCached" class="card piece-image-card">
        <img :src="`/api/bulkpieces/${id}/image`" class="piece-image" alt="" />
      </div>

      <div class="card">
        <h2>Edit Piece</h2>
        <form class="form-row" @submit.prevent="submitEdit">
          <div class="form-field">
            <label>Lego ID *</label>
            <input v-model="editForm.legoId" required />
          </div>
          <div class="form-field">
            <label>Color *</label>
            <ColorSelect v-model="editForm.legoColorUid" :colors="colors" />
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

        <fieldset class="alloc-section" :disabled="fullyAllocated" style="margin-top: 0.75rem">
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

          <form class="form-row" style="width:100%; margin-top: 0.75rem" @submit.prevent="submitDrawerStorage">
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
        </fieldset>

        <p v-if="fullyAllocated" class="alloc-full-msg">All quantity is allocated — remove a location to free up space.</p>

        <button
          v-if="piece.storageAllocations && piece.storageAllocations.length"
          type="button"
          class="danger"
          style="margin-top: 0.75rem"
          @click="clearStorage"
        >Clear All</button>
        <p v-if="storageError" class="error">{{ storageError }}</p>
      </div>

      <div class="card">
        <button class="danger" @click="showConfirm = true">Delete Piece</button>
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
  allocatePieceToBox, allocatePieceToDrawer, deallocatePieceStorage, clearPieceStorage,
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
const editForm = ref({ legoId: '', legoColorUid: '', description: '', quantity: 1 })

const boxNameMap = computed(() => Object.fromEntries(boxes.value.map(b => [b.id, b.name])))
const drawerLabelMap = computed(() =>
  Object.fromEntries(drawers.value.map(d => [d.id, d.label || `Position ${d.position}`])))

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

<style scoped>
.piece-image-card {
  display: flex;
  justify-content: center;
  padding: 0.75rem;
}

.piece-image {
  max-height: 200px;
  object-fit: contain;
}

.alloc-section {
  border: none;
  padding: 0;
  margin: 0;
}

.alloc-section:disabled {
  opacity: 0.45;
}

.alloc-full-msg {
  margin-top: 0.4rem;
  font-size: 0.85rem;
  color: #6b7280;
}

.swatch-title {
  display: inline-block;
  width: 1rem;
  height: 1rem;
  border-radius: 3px;
  border: 1px solid rgba(0, 0, 0, 0.2);
  vertical-align: middle;
  margin-right: 0.2rem;
}
</style>
