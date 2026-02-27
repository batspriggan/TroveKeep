<template>
  <div>
    <h1>Lego Sets</h1>

    <form class="form-row" @submit.prevent="submit">
      <div class="form-field">
        <label>Set Number *</label>
        <input v-model="form.setNumber" required placeholder="e.g. 75313" />
      </div>
      <div class="form-field">
        <label>Description *</label>
        <input v-model="form.description" required placeholder="Description" />
      </div>
      <div class="form-field">
        <label>Photo URL</label>
        <input v-model="form.photoUrl" placeholder="https://..." />
      </div>
      <div class="form-field" style="max-width: 80px">
        <label>Qty *</label>
        <input v-model.number="form.quantity" type="number" min="1" required />
      </div>
      <button class="primary" type="submit">Add Set</button>
    </form>

    <p v-if="error" class="error">{{ error }}</p>

    <p v-if="loading">Loading…</p>

    <table v-else>
      <thead>
        <tr>
          <th>Set Number</th>
          <th>Description</th>
          <th>Qty</th>
          <th>Box</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="s in sets" :key="s.id">
          <td>
            <RouterLink :to="`/sets/${s.id}`">{{ s.setNumber }}</RouterLink>
          </td>
          <td>{{ s.description }}</td>
          <td>{{ s.quantity }}</td>
          <td>{{ (s.storageAllocations && s.storageAllocations.length) ? `${s.storageAllocations.length} location(s)` : '—' }}</td>
          <td>
            <button class="danger" @click="confirmDelete(s)">Delete</button>
          </td>
        </tr>
        <tr v-if="sets.length === 0">
          <td colspan="5">No sets yet.</td>
        </tr>
      </tbody>
    </table>

    <ConfirmDialog
      :open="!!deleteTarget"
      :message="`Delete set ${deleteTarget?.setNumber}?`"
      @confirm="doDelete"
      @cancel="deleteTarget = null"
    />
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { getAllSets, createSet, deleteSet } from '../../api/sets.js'
import ConfirmDialog from '../../components/ConfirmDialog.vue'

const sets = ref([])
const loading = ref(true)
const error = ref('')
const deleteTarget = ref(null)
const form = ref({ setNumber: '', description: '', photoUrl: '', quantity: 1 })

async function load() {
  loading.value = true
  error.value = ''
  try {
    sets.value = await getAllSets()
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
      setNumber: form.value.setNumber,
      description: form.value.description,
      photoUrl: form.value.photoUrl || null,
      quantity: form.value.quantity,
    })
    form.value = { setNumber: '', description: '', photoUrl: '', quantity: 1 }
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
