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
          <button class="primary" type="submit">Save</button>
        </form>
        <p v-if="editError" class="error">{{ editError }}</p>
      </div>

      <div class="card">
        <h2>Storage</h2>
        <p v-if="set.boxId">
          Stored in box:
          <RouterLink :to="`/boxes/${set.boxId}`">{{ set.boxId }}</RouterLink>
        </p>
        <p v-else>Not stored anywhere.</p>

        <form class="form-row" style="margin-top: 0.75rem" @submit.prevent="submitStorage">
          <div class="form-field">
            <label>Assign to Box</label>
            <select v-model="selectedBoxId">
              <option value="">— select box —</option>
              <option v-for="b in boxes" :key="b.id" :value="b.id">{{ b.name }}</option>
            </select>
          </div>
          <button class="primary" type="submit" :disabled="!selectedBoxId">Assign</button>
          <button v-if="set.boxId" type="button" class="danger" @click="clearStorage">Clear Storage</button>
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
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getSet, updateSet, deleteSet, assignSetToBox, removeSetStorage } from '../../api/sets.js'
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
const editForm = ref({ setNumber: '', description: '', photoUrl: '' })

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [s, b] = await Promise.all([getSet(id), getAllBoxes()])
    set.value = s
    boxes.value = b
    editForm.value = { setNumber: s.setNumber, description: s.description, photoUrl: s.photoUrl ?? '' }
    selectedBoxId.value = s.boxId ?? ''
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
    })
    set.value = updated
  } catch (e) {
    editError.value = e.message
  }
}

async function submitStorage() {
  storageError.value = ''
  try {
    const updated = await assignSetToBox(id, selectedBoxId.value)
    set.value = updated
  } catch (e) {
    storageError.value = e.message
  }
}

async function clearStorage() {
  storageError.value = ''
  try {
    await removeSetStorage(id)
    set.value = { ...set.value, boxId: null }
    selectedBoxId.value = ''
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
