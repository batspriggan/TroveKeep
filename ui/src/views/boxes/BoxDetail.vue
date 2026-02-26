<template>
  <div>
    <RouterLink class="back-link" to="/boxes">← Back to Boxes</RouterLink>

    <p v-if="loading">Loading…</p>
    <p v-else-if="error" class="error">{{ error }}</p>

    <template v-else-if="box">
      <h1>{{ box.name }}</h1>

      <div class="card">
        <h2>Edit Box</h2>
        <form class="form-row" @submit.prevent="submitEdit">
          <div class="form-field">
            <label>Name *</label>
            <input v-model="editForm.name" required />
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
        <h2>Sets in this Box</h2>
        <table v-if="box.sets && box.sets.length">
          <thead>
            <tr>
              <th>Set Number</th>
              <th>Description</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="s in box.sets" :key="s.id">
              <td><RouterLink :to="`/sets/${s.id}`">{{ s.setNumber }}</RouterLink></td>
              <td>{{ s.description }}</td>
            </tr>
          </tbody>
        </table>
        <p v-else>No sets stored here.</p>
      </div>

      <div class="card">
        <h2>Bulk Pieces in this Box</h2>
        <table v-if="box.bulkPieces && box.bulkPieces.length">
          <thead>
            <tr>
              <th>Lego ID</th>
              <th>Color</th>
              <th>Description</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="p in box.bulkPieces" :key="p.id">
              <td><RouterLink :to="`/bulkpieces/${p.id}`">{{ p.legoId }}</RouterLink></td>
              <td>{{ p.legoColor }}</td>
              <td>{{ p.description }}</td>
            </tr>
          </tbody>
        </table>
        <p v-else>No bulk pieces stored here.</p>
      </div>

      <div class="card">
        <button class="danger" @click="showConfirm = true">Delete Box</button>
      </div>
    </template>

    <ConfirmDialog
      :open="showConfirm"
      :message="`Delete box '${box?.name}'?`"
      @confirm="doDelete"
      @cancel="showConfirm = false"
    />
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getBox, getBoxContents, updateBox, deleteBox } from '../../api/boxes.js'
import ConfirmDialog from '../../components/ConfirmDialog.vue'

const route = useRoute()
const router = useRouter()
const id = route.params.id

const box = ref(null)
const loading = ref(true)
const error = ref('')
const editError = ref('')
const showConfirm = ref(false)
const editForm = ref({ name: '', photoUrl: '' })

async function load() {
  loading.value = true
  error.value = ''
  try {
    // getBoxContents returns the BoxDetailResponse which includes sets + bulkPieces
    const detail = await getBoxContents(id)
    box.value = detail
    editForm.value = { name: detail.name, photoUrl: detail.photoUrl ?? '' }
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

async function submitEdit() {
  editError.value = ''
  try {
    const updated = await updateBox(id, { name: editForm.value.name, photoUrl: editForm.value.photoUrl || null })
    box.value = { ...box.value, ...updated }
  } catch (e) {
    editError.value = e.message
  }
}

async function doDelete() {
  try {
    await deleteBox(id)
    router.push('/boxes')
  } catch (e) {
    error.value = e.message
    showConfirm.value = false
  }
}

onMounted(load)
</script>
