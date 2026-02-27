<template>
  <div>
    <RouterLink class="back-link" to="/sets">← Back to Sets</RouterLink>

    <p v-if="loading">Loading…</p>
    <p v-else-if="error" class="error">{{ error }}</p>

    <template v-else-if="set">
      <h1>{{ set.setNumber }}</h1>

      <div class="card">
        <h2>Edit Set</h2>
        <form class="form-row" @submit.prevent="submitEdit">
          <div class="form-field">
            <label>Set Number *</label>
            <input v-model="editForm.setNumber" required />
          </div>
          <div class="form-field">
            <label>Description *</label>
            <input v-model="editForm.description" required />
          </div>
          <div class="form-field">
            <label>Photo URL</label>
            <input v-model="editForm.photoUrl" />
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

        <table v-if="set.storageAllocations && set.storageAllocations.length">
          <thead>
            <tr>
              <th>Box</th>
              <th>Qty</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="a in set.storageAllocations" :key="a.storageId">
              <td>
                <RouterLink :to="`/boxes/${a.storageId}`">{{ boxNameMap[a.storageId] ?? a.storageId }}</RouterLink>
              </td>
              <td>{{ a.quantity }}</td>
              <td><button class="danger" @click="deallocate(a.storageId)">Remove</button></td>
            </tr>
          </tbody>
        </table>
        <p v-else>Not stored anywhere.</p>

        <p style="margin-top: 0.5rem">Unallocated: {{ unallocated }} of {{ set.quantity }}</p>

        <form class="form-row" style="margin-top: 0.75rem" @submit.prevent="submitStorage">
          <div class="form-field">
            <label>Box</label>
            <select v-model="selectedBoxId">
              <option value="">— select box —</option>
              <option v-for="b in boxes" :key="b.id" :value="b.id">{{ b.name }}</option>
            </select>
          </div>
          <div class="form-field" style="max-width: 80px">
            <label>Qty</label>
            <input v-model.number="allocQty" type="number" min="1" required />
          </div>
          <button class="primary" type="submit" :disabled="!selectedBoxId || allocQty < 1">Allocate</button>
          <button
            v-if="set.storageAllocations && set.storageAllocations.length"
            type="button"
            class="danger"
            @click="clearStorage"
          >Clear All</button>
        </form>
        <p v-if="storageError" class="error">{{ storageError }}</p>
      </div>

      <div class="card">
        <button class="danger" @click="showConfirm = true">Delete Set</button>
      </div>
    </template>

    <ConfirmDialog
      :open="showConfirm"
      :message="`Delete set ${set?.setNumber}?`"
      @confirm="doDelete"
      @cancel="showConfirm = false"
    />
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getSet, updateSet, deleteSet, allocateSetToBox, deallocateSetStorage, clearSetStorage } from '../../api/sets.js'
import { getAllBoxes } from '../../api/boxes.js'
import ConfirmDialog from '../../components/ConfirmDialog.vue'

const route = useRoute()
const router = useRouter()
const id = route.params.id

const set = ref(null)
const boxes = ref([])
const loading = ref(true)
const error = ref('')
const editError = ref('')
const storageError = ref('')
const showConfirm = ref(false)
const selectedBoxId = ref('')
const allocQty = ref(1)
const editForm = ref({ setNumber: '', description: '', photoUrl: '', quantity: 1 })

const boxNameMap = computed(() => Object.fromEntries(boxes.value.map(b => [b.id, b.name])))

const unallocated = computed(() => {
  if (!set.value) return 0
  const allocated = (set.value.storageAllocations ?? []).reduce((sum, a) => sum + a.quantity, 0)
  return set.value.quantity - allocated
})

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [s, b] = await Promise.all([getSet(id), getAllBoxes()])
    set.value = s
    boxes.value = b
    editForm.value = { setNumber: s.setNumber, description: s.description, photoUrl: s.photoUrl ?? '', quantity: s.quantity }
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

async function submitEdit() {
  editError.value = ''
  try {
    const updated = await updateSet(id, {
      setNumber: editForm.value.setNumber,
      description: editForm.value.description,
      photoUrl: editForm.value.photoUrl || null,
      quantity: editForm.value.quantity,
    })
    set.value = updated
  } catch (e) {
    editError.value = e.message
  }
}

async function submitStorage() {
  storageError.value = ''
  try {
    const updated = await allocateSetToBox(id, selectedBoxId.value, allocQty.value)
    set.value = updated
    selectedBoxId.value = ''
    allocQty.value = 1
  } catch (e) {
    storageError.value = e.message
  }
}

async function deallocate(storageId) {
  storageError.value = ''
  try {
    const updated = await deallocateSetStorage(id, storageId)
    set.value = updated
  } catch (e) {
    storageError.value = e.message
  }
}

async function clearStorage() {
  storageError.value = ''
  try {
    const updated = await clearSetStorage(id)
    set.value = updated
  } catch (e) {
    storageError.value = e.message
  }
}

async function doDelete() {
  try {
    await deleteSet(id)
    router.push('/sets')
  } catch (e) {
    error.value = e.message
    showConfirm.value = false
  }
}

onMounted(load)
</script>
