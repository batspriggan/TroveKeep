<template>
  <div>
    <h1>Drawer Containers</h1>

    <form class="form-row" @submit.prevent="submit">
      <div class="form-field">
        <label>Name *</label>
        <input v-model="form.name" required placeholder="Container name" />
      </div>
      <div class="form-field">
        <label>Description</label>
        <input v-model="form.description" placeholder="Optional description" />
      </div>
      <button class="primary" type="submit">Add Container</button>
    </form>

    <p v-if="error" class="error">{{ error }}</p>

    <p v-if="loading">Loading…</p>

    <table v-else>
      <thead>
        <tr>
          <th>Name</th>
          <th>Description</th>
          <th>Drawers</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="c in containers" :key="c.id">
          <td>
            <RouterLink :to="`/drawercontainers/${c.id}`">{{ c.name }}</RouterLink>
          </td>
          <td>{{ c.description || '—' }}</td>
          <td>{{ c.drawerCount }}</td>
          <td>
            <button class="danger" @click="confirmDelete(c)">Delete</button>
          </td>
        </tr>
        <tr v-if="containers.length === 0">
          <td colspan="4">No drawer containers yet.</td>
        </tr>
      </tbody>
    </table>

    <ConfirmDialog
      :open="!!deleteTarget"
      :message="`Delete container '${deleteTarget?.name}'?`"
      @confirm="doDelete"
      @cancel="deleteTarget = null"
    />
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { getAllDrawerContainers, createDrawerContainer, deleteDrawerContainer } from '../../api/drawercontainers.js'
import ConfirmDialog from '../../components/ConfirmDialog.vue'

const containers = ref([])
const loading = ref(true)
const error = ref('')
const deleteTarget = ref(null)
const form = ref({ name: '', description: '' })

async function load() {
  loading.value = true
  error.value = ''
  try {
    containers.value = await getAllDrawerContainers()
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

async function submit() {
  error.value = ''
  try {
    await createDrawerContainer({ name: form.value.name, description: form.value.description || null })
    form.value = { name: '', description: '' }
    await load()
  } catch (e) {
    error.value = e.message
  }
}

function confirmDelete(c) {
  deleteTarget.value = c
}

async function doDelete() {
  error.value = ''
  try {
    await deleteDrawerContainer(deleteTarget.value.id)
    deleteTarget.value = null
    await load()
  } catch (e) {
    error.value = e.message
    deleteTarget.value = null
  }
}

onMounted(load)
</script>
