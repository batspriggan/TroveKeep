<template>
  <div>
    <RouterLink class="back-link" :to="drawer ? `/drawercontainers/${containerId}` : '/drawercontainers'">
      ← Back to Container
    </RouterLink>

    <p v-if="loading">Loading…</p>
    <p v-else-if="error" class="error">{{ error }}</p>

    <template v-else-if="drawer">
      <h1>Drawer — Position {{ drawer.position }}<span v-if="contents.length"> ({{ contents.map(p => p.legoId).join(', ') }})</span></h1>

      <div class="card">
        <h2>Edit Drawer</h2>
        <form class="form-row" @submit.prevent="submitEdit">
          <div class="form-field">
            <label>Label</label>
            <input v-model="editForm.label" placeholder="Optional label" />
          </div>
          <button class="primary" type="submit">Save</button>
        </form>
        <p v-if="editError" class="error">{{ editError }}</p>
      </div>

      <div class="card">
        <h2>Bulk Pieces in this Drawer</h2>
        <table v-if="contents && contents.length">
          <thead>
            <tr>
              <th></th>
              <th>Lego ID</th>
              <th>Color</th>
              <th>Description</th>
              <th>Qty in Drawer</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="p in contents" :key="p.id">
              <td>
                <img v-if="p.imageCached" :src="`/api/bulkpieces/${p.id}/image`" class="cell-thumb" alt="" />
              </td>
              <td><RouterLink :to="`/bulkpieces/${p.id}`">{{ p.legoId }}</RouterLink></td>
              <td>
                <span v-if="p.legoColorRgb" class="color-swatch" :style="{ background: '#' + p.legoColorRgb }"></span>
                {{ p.legoColorName ?? `#${p.legoColorId}` }}
              </td>
              <td>{{ p.description }}</td>
              <td class="td-qty">
                <input
                  v-model.number="qtyEdits[p.id]"
                  type="number"
                  min="1"
                  class="qty-edit"
                  :disabled="qtySaving"
                  @keyup.enter="updateAllocQty(p)"
                />
                <button class="btn-update" :disabled="qtySaving" @click="updateAllocQty(p)" title="Update quantity">✓</button>
              </td>
            </tr>
          </tbody>
        </table>
        <p v-if="qtyError" class="error">{{ qtyError }}</p>
        <p v-else>No bulk pieces stored here.</p>
      </div>

      <div class="card actions-card">
        <button class="secondary" :disabled="emptyLoading" @click="showEmptyConfirm = true">Empty Drawer</button>
        <button class="danger" @click="showConfirm = true">Delete Drawer</button>
      </div>
    </template>

    <ConfirmDialog
      :open="showEmptyConfirm"
      :message="`Empty drawer at position ${drawer?.position}? This removes all pieces stored here (the drawer stays).`"
      @confirm="doEmpty"
      @cancel="showEmptyConfirm = false"
    />

    <ConfirmDialog
      :open="showConfirm"
      :message="`Delete drawer at position ${drawer?.position}?`"
      @confirm="doDelete"
      @cancel="showConfirm = false"
    />
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getDrawerContents, updateDrawer, deleteDrawer, emptyDrawer } from '../../api/drawers.js'
import { setDrawerQuantity } from '../../api/bulkpieces.js'
import ConfirmDialog from '../../components/ConfirmDialog.vue'

const route = useRoute()
const router = useRouter()
const containerId = route.params.containerId
const position = parseInt(route.params.position)

const drawer = ref(null)
const contents = ref([])
const loading = ref(true)
const error = ref('')
const editError = ref('')
const showConfirm = ref(false)
const showEmptyConfirm = ref(false)
const emptyLoading = ref(false)
const editForm = ref({ label: '' })
const qtyEdits = ref({})
const qtySaving = ref(false)
const qtyError = ref('')

async function updateAllocQty(p) {
  const value = Number(qtyEdits.value[p.id])
  if (!value || value < 1) {
    qtyError.value = 'Quantity must be at least 1.'
    return
  }
  qtySaving.value = true
  qtyError.value = ''
  try {
    const updated = await setDrawerQuantity(p.id, containerId, position, value)
    // updated is the whole piece; reload contents so quantities refresh
    const detail = await getDrawerContents(containerId, position)
    contents.value = detail.bulkPieces ?? []
    syncQtyEdits()
  } catch (e) {
    qtyError.value = e.message
    qtyEdits.value[p.id] = qtyInDrawer(p)
  } finally {
    qtySaving.value = false
  }
}

function qtyInDrawer(p) {
  return p.storageAllocations?.find(a => a.storageId === containerId && a.storagePosition === position)?.quantity
}

function syncQtyEdits() {
  const next = {}
  for (const p of contents.value) next[p.id] = qtyInDrawer(p)
  qtyEdits.value = next
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const detail = await getDrawerContents(containerId, position)
    drawer.value = detail
    contents.value = detail.bulkPieces ?? []
    editForm.value = { label: detail.label ?? '' }
    syncQtyEdits()
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

async function submitEdit() {
  editError.value = ''
  try {
    const updated = await updateDrawer(containerId, position, { position, label: editForm.value.label || null })
    drawer.value = { ...drawer.value, ...updated }
  } catch (e) {
    editError.value = e.message
  }
}

async function doEmpty() {
  emptyLoading.value = true
  try {
    await emptyDrawer(containerId, position)
    showEmptyConfirm.value = false
    await load()
  } catch (e) {
    error.value = e.message
    showEmptyConfirm.value = false
  } finally {
    emptyLoading.value = false
  }
}

async function doDelete() {
  try {
    await deleteDrawer(containerId, position)
    router.push(`/drawercontainers/${containerId}`)
  } catch (e) {
    error.value = e.message
    showConfirm.value = false
  }
}

onMounted(load)
</script>

<style scoped>
.color-swatch {
  display: inline-block;
  width: 12px;
  height: 12px;
  border-radius: 2px;
  border: 1px solid #ccc;
  vertical-align: middle;
  margin-right: 4px;
}

.td-qty {
  text-align: right;
}

.qty-edit {
  width: 56px;
  font-family: var(--font-mono);
  font-size: var(--text-sm);
  padding: 2px 4px;
  border: 1px solid var(--color-border);
  border-radius: 4px;
  text-align: right;
}

.btn-update {
  font-size: var(--text-xs);
  padding: 0.2rem 0.5rem;
  margin-left: 4px;
  background: transparent;
  border: 1px solid var(--color-border);
  color: var(--color-text-secondary);
  border-radius: 4px;
  cursor: pointer;
}

.btn-update:hover {
  border-color: var(--color-accent);
  color: var(--color-accent);
}

.actions-card {
  display: flex;
  gap: var(--space-2);
  flex-wrap: wrap;
}

.cell-thumb {
  width: 34px;
  height: 34px;
  object-fit: cover;
  border-radius: 4px;
  border: 1px solid #ddd;
  display: block;
}
</style>
