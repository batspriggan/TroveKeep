<template>
  <div>
    <h1>Boxes</h1>

    <form class="form-row" @submit.prevent="submit">
      <div class="form-field">
        <label>Name *</label>
        <input v-model="form.name" required placeholder="Box name" />
      </div>
      <button class="primary" type="submit">Add Box</button>
    </form>

    <p v-if="error" class="error">{{ error }}</p>

    <p v-if="loading">Loading…</p>

    <template v-else>
    <input v-model="filterText" type="search" placeholder="Filter by box name…" style="margin: 0.5rem 0; width: 100%; max-width: 400px;" />

    <table>
      <thead>
        <tr>
          <th>Name</th>
          <th>Sets</th>
          <th>Bulk Pieces</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="b in filteredBoxes" :key="b.id">
          <td>
            <RouterLink :to="`/boxes/${b.id}`">{{ b.name }}</RouterLink>
          </td>
          <td>{{ b.setCount }}</td>
          <td>{{ b.bulkPieceCount }}</td>
          <td>
            <button class="danger" @click="confirmDelete(b)">Delete</button>
          </td>
        </tr>
        <tr v-if="boxes.length === 0">
          <td colspan="4">No boxes yet.</td>
        </tr>
        <tr v-else-if="filteredBoxes.length === 0">
          <td colspan="4">No results match your filter.</td>
        </tr>
      </tbody>
    </table>
    </template>

    <ConfirmDialog
      :open="!!deleteTarget"
      :message="`Delete box '${deleteTarget?.name}'?`"
      @confirm="doDelete"
      @cancel="deleteTarget = null"
    />
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { getAllBoxes, createBox, deleteBox } from '../../api/boxes.js'
import ConfirmDialog from '../../components/ConfirmDialog.vue'

const boxes = ref([])
const filterText = ref('')
const filteredBoxes = computed(() => {
  const q = filterText.value.trim().toLowerCase()
  if (!q) return boxes.value
  return boxes.value.filter(b => b.name.toLowerCase().includes(q))
})
const loading = ref(true)
const error = ref('')
const deleteTarget = ref(null)
const form = ref({ name: '' })

async function load() {
  loading.value = true
  error.value = ''
  try {
    boxes.value = await getAllBoxes()
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

async function submit() {
  error.value = ''
  try {
    await createBox({ name: form.value.name })
    form.value = { name: '' }
    await load()
  } catch (e) {
    error.value = e.message
  }
}

function confirmDelete(b) {
  deleteTarget.value = b
}

async function doDelete() {
  error.value = ''
  try {
    await deleteBox(deleteTarget.value.id)
    deleteTarget.value = null
    await load()
  } catch (e) {
    error.value = e.message
    deleteTarget.value = null
  }
}

onMounted(load)
</script>
