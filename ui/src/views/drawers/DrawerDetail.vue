<template>
  <div>
    <RouterLink class="back-link" :to="drawer ? `/drawercontainers/${drawer.drawerContainerId}` : '/drawercontainers'">
      ← Back to Container
    </RouterLink>

    <p v-if="loading">Loading…</p>
    <p v-else-if="error" class="error">{{ error }}</p>

    <template v-else-if="drawer">
      <h1>Drawer — Position {{ drawer.position }}<span v-if="drawer.label"> ({{ drawer.label }})</span></h1>

      <div class="card">
        <h2>Edit Drawer</h2>
        <form class="form-row" @submit.prevent="submitEdit">
          <div class="form-field">
            <label>Position *</label>
            <input v-model.number="editForm.position" type="number" min="1" required />
          </div>
          <div class="form-field">
            <label>Label</label>
            <input v-model="editForm.label" />
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
              <th>Lego ID</th>
              <th>Color</th>
              <th>Description</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="p in contents" :key="p.id">
              <td><RouterLink :to="`/bulkpieces/${p.id}`">{{ p.legoId }}</RouterLink></td>
              <td>{{ p.legoColor }}</td>
              <td>{{ p.description }}</td>
            </tr>
          </tbody>
        </table>
        <p v-else>No bulk pieces stored here.</p>
      </div>

      <div class="card">
        <button class="danger" @click="showConfirm = true">Delete Drawer</button>
      </div>
    </template>

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
import { getDrawer, getDrawerContents, updateDrawer, deleteDrawer } from '../../api/drawers.js'
import ConfirmDialog from '../../components/ConfirmDialog.vue'

const route = useRoute()
const router = useRouter()
const id = route.params.id

const drawer = ref(null)
const contents = ref([])
const loading = ref(true)
const error = ref('')
const editError = ref('')
const showConfirm = ref(false)
const editForm = ref({ position: 1, label: '' })

async function load() {
  loading.value = true
  error.value = ''
  try {
    const detail = await getDrawerContents(id)
    drawer.value = detail
    contents.value = detail.bulkPieces ?? []
    editForm.value = { position: detail.position, label: detail.label ?? '' }
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

async function submitEdit() {
  editError.value = ''
  try {
    const updated = await updateDrawer(id, {
      position: editForm.value.position,
      label: editForm.value.label || null,
    })
    drawer.value = { ...drawer.value, ...updated }
  } catch (e) {
    editError.value = e.message
  }
}

async function doDelete() {
  try {
    await deleteDrawer(id)
    router.push(`/drawercontainers/${drawer.value.drawerContainerId}`)
  } catch (e) {
    error.value = e.message
    showConfirm.value = false
  }
}

onMounted(load)
</script>
