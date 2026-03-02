<template>
  <div>
    <RouterLink class="back-link" to="/drawercontainers">← Back to Drawer Containers</RouterLink>

    <p v-if="loading">Loading…</p>
    <p v-else-if="error" class="error">{{ error }}</p>

    <template v-else-if="container">
      <h1>{{ container.name }}</h1>

      <div class="card">
        <h2>Edit Container</h2>
        <form class="form-row" @submit.prevent="submitEdit">
          <div class="form-field">
            <label>Name *</label>
            <input v-model="editForm.name" required />
          </div>
          <div class="form-field">
            <label>Description</label>
            <input v-model="editForm.description" />
          </div>
          <button class="primary" type="submit">Save</button>
        </form>
        <p v-if="editError" class="error">{{ editError }}</p>
      </div>

      <div class="card">
        <h2>Drawers</h2>
        <table v-if="drawers.length">
          <thead>
            <tr>
              <th>Position</th>
              <th>Contents</th>
              <th>Bulk Pieces</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="d in drawers" :key="d.id">
              <td>{{ d.position }}</td>
              <td>
                <RouterLink :to="`/drawers/${d.id}`">
                  {{ d.contentSummary?.join(', ') || '(empty)' }}
                </RouterLink>
              </td>
              <td>{{ d.bulkPieceCount }}</td>
              <td>
                <button class="danger" @click="confirmDeleteDrawer(d)">Delete</button>
              </td>
            </tr>
          </tbody>
        </table>
        <p v-else>No drawers yet.</p>

        <h2 style="margin-top: 1rem">Add Drawer</h2>
        <form class="form-row" @submit.prevent="submitDrawer">
          <div class="form-field">
            <label>Position *</label>
            <input v-model.number="drawerForm.position" type="number" min="1" required />
          </div>
          <button class="primary" type="submit">Add Drawer</button>
        </form>
        <p v-if="drawerError" class="error">{{ drawerError }}</p>

        <h2 style="margin-top: 1rem">Add Drawers in Bulk</h2>
        <form class="form-row" @submit.prevent="submitBulk">
          <div class="form-field">
            <label>Number of drawers *</label>
            <input v-model.number="bulkCount" type="number" min="1" max="100" required />
          </div>
          <button class="primary" type="submit" :disabled="bulkAdding">
            {{ bulkAdding ? 'Adding…' : 'Add Drawers' }}
          </button>
        </form>
        <p v-if="bulkError" class="error">{{ bulkError }}</p>
      </div>

      <div class="card">
        <button class="danger" @click="showConfirm = true">Delete Container</button>
      </div>
    </template>

    <ConfirmDialog
      :open="showConfirm"
      :message="`Delete container '${container?.name}'?`"
      @confirm="doDelete"
      @cancel="showConfirm = false"
    />

    <ConfirmDialog
      :open="!!deleteDrawerTarget"
      :message="`Delete drawer at position ${deleteDrawerTarget?.position}?`"
      @confirm="doDeleteDrawer"
      @cancel="deleteDrawerTarget = null"
    />
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  getDrawerContainer, updateDrawerContainer, deleteDrawerContainer,
  getDrawerContainerDrawers, addDrawer,
} from '../../api/drawercontainers.js'
import { deleteDrawer } from '../../api/drawers.js'
import ConfirmDialog from '../../components/ConfirmDialog.vue'

const route = useRoute()
const router = useRouter()
const id = route.params.id

const container = ref(null)
const drawers = ref([])
const loading = ref(true)
const error = ref('')
const editError = ref('')
const drawerError = ref('')
const showConfirm = ref(false)
const deleteDrawerTarget = ref(null)
const editForm = ref({ name: '', description: '' })
const drawerForm = ref({ position: 1 })
const bulkCount = ref(1)
const bulkError = ref('')
const bulkAdding = ref(false)

function nextPosition(drawerList) {
  if (!drawerList.length) return 1
  return Math.max(...drawerList.map(d => d.position)) + 1
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [c, d] = await Promise.all([
      getDrawerContainer(id),
      getDrawerContainerDrawers(id),
    ])
    container.value = c
    drawers.value = d.drawers
    drawerForm.value = { position: nextPosition(d.drawers) }
    editForm.value = { name: c.name, description: c.description ?? '' }
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

async function submitEdit() {
  editError.value = ''
  try {
    const updated = await updateDrawerContainer(id, {
      name: editForm.value.name,
      description: editForm.value.description || null,
    })
    container.value = { ...container.value, ...updated }
  } catch (e) {
    editError.value = e.message
  }
}

async function submitDrawer() {
  drawerError.value = ''
  try {
    await addDrawer(id, { position: drawerForm.value.position, label: null })
    drawers.value = (await getDrawerContainerDrawers(id)).drawers
    drawerForm.value = { position: nextPosition(drawers.value) }
  } catch (e) {
    drawerError.value = e.message
  }
}

async function submitBulk() {
  bulkError.value = ''
  bulkAdding.value = true
  try {
    let start = nextPosition(drawers.value)
    for (let i = 0; i < bulkCount.value; i++) {
      await addDrawer(id, { position: start + i, label: null })
    }
    drawers.value = (await getDrawerContainerDrawers(id)).drawers
    drawerForm.value = { position: nextPosition(drawers.value), label: '' }
    bulkCount.value = 1
  } catch (e) {
    bulkError.value = e.message
  } finally {
    bulkAdding.value = false
  }
}

function confirmDeleteDrawer(d) {
  deleteDrawerTarget.value = d
}

async function doDeleteDrawer() {
  error.value = ''
  try {
    await deleteDrawer(deleteDrawerTarget.value.id)
    deleteDrawerTarget.value = null
    drawers.value = (await getDrawerContainerDrawers(id)).drawers
  } catch (e) {
    error.value = e.message
    deleteDrawerTarget.value = null
  }
}

async function doDelete() {
  try {
    await deleteDrawerContainer(id)
    router.push('/drawercontainers')
  } catch (e) {
    error.value = e.message
    showConfirm.value = false
  }
}

onMounted(load)
</script>
