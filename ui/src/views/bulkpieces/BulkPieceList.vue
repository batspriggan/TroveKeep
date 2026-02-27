<template>
  <div>
    <h1>Bulk Pieces</h1>

    <form class="form-row" @submit.prevent="submit">
      <div class="form-field">
        <label>Lego ID *</label>
        <input v-model="form.legoId" required placeholder="e.g. 3001" />
      </div>
      <div class="form-field">
        <label>Color *</label>
        <input v-model="form.legoColor" required placeholder="e.g. Red" />
      </div>
      <div class="form-field">
        <label>Description *</label>
        <input v-model="form.description" required placeholder="Description" />
      </div>
      <div class="form-field" style="max-width: 80px">
        <label>Qty *</label>
        <input v-model.number="form.quantity" type="number" min="1" required />
      </div>
      <button class="primary" type="submit">Add Piece</button>
    </form>

    <p v-if="error" class="error">{{ error }}</p>

    <p v-if="loading">Loading…</p>

    <table v-else>
      <thead>
        <tr>
          <th>Lego ID</th>
          <th>Color</th>
          <th>Description</th>
          <th>Qty</th>
          <th>Storage</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="p in pieces" :key="p.id">
          <td>
            <RouterLink :to="`/bulkpieces/${p.id}`">{{ p.legoId }}</RouterLink>
          </td>
          <td>{{ p.legoColor }}</td>
          <td>{{ p.description }}</td>
          <td>{{ p.quantity }}</td>
          <td>{{ p.boxId ? 'Box' : p.drawerId ? 'Drawer' : '—' }}</td>
          <td>
            <button class="danger" @click="confirmDelete(p)">Delete</button>
          </td>
        </tr>
        <tr v-if="pieces.length === 0">
          <td colspan="6">No bulk pieces yet.</td>
        </tr>
      </tbody>
    </table>

    <ConfirmDialog
      :open="!!deleteTarget"
      :message="`Delete ${deleteTarget?.legoId} (${deleteTarget?.legoColor})?`"
      @confirm="doDelete"
      @cancel="deleteTarget = null"
    />
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { getAllBulkPieces, createBulkPiece, deleteBulkPiece } from '../../api/bulkpieces.js'
import ConfirmDialog from '../../components/ConfirmDialog.vue'

const pieces = ref([])
const loading = ref(true)
const error = ref('')
const deleteTarget = ref(null)
const form = ref({ legoId: '', legoColor: '', description: '', quantity: 1 })

async function load() {
  loading.value = true
  error.value = ''
  try {
    pieces.value = await getAllBulkPieces()
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

async function submit() {
  error.value = ''
  try {
    await createBulkPiece({ ...form.value })
    form.value = { legoId: '', legoColor: '', description: '', quantity: 1 }
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
