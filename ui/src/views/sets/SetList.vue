<template>
  <div>
    <h1>Lego Sets</h1>

    <form class="form-row" @submit.prevent="submit">
      <div class="form-field" style="flex: 1; min-width: 220px">
        <label>Search Archive</label>
        <SetArchiveTypeahead @select="onArchiveSelect" />
      </div>

      <template v-if="selectedArchiveSet">
        <div class="selected-pill">
          <span><strong>{{ selectedArchiveSet.setNum }}</strong> — {{ selectedArchiveSet.name }}</span>
          <button type="button" class="clear-btn" @click="clearSelected">✕</button>
        </div>
      </template>

      <div v-if="!selectedArchiveSet" class="form-field">
        <label>Set Number *</label>
        <input v-model="form.setNumber" required placeholder="e.g. 75313" />
      </div>

      <div class="form-field" style="max-width: 80px">
        <label>Qty *</label>
        <input v-model.number="form.quantity" type="number" min="1" required />
      </div>
      <button class="primary" type="submit" :disabled="!selectedArchiveSet && !form.setNumber">Add Set</button>
    </form>

    <p v-if="error" class="error">{{ error }}</p>

    <p v-if="loading">Loading…</p>

    <template v-else>
    <input v-model="filterText" type="search" placeholder="Filter by set number or description…" style="margin: 0.5rem 0; width: 100%; max-width: 400px;" />

    <table>
      <thead>
        <tr>
          <th></th>
          <th>Set Number</th>
          <th>Description</th>
          <th>Qty</th>
          <th class="mobile-hide">Box</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="s in filteredSets" :key="s.id">
          <td class="thumb-cell">
            <img v-if="s.imageCached" :src="`/api/sets/${s.id}/image`" class="list-thumb" alt="" />
          </td>
          <td>
            <RouterLink :to="`/sets/${s.id}`">{{ s.setNumber }}</RouterLink>
          </td>
          <td>{{ s.description }}</td>
          <td>{{ s.quantity }}</td>
          <td class="mobile-hide">{{ (s.storageAllocations && s.storageAllocations.length) ? `${s.storageAllocations.length} location(s)` : '—' }}</td>
          <td>
            <button class="danger" @click="confirmDelete(s)">Delete</button>
          </td>
        </tr>
        <tr v-if="sets.length === 0">
          <td colspan="6">No sets yet.</td>
        </tr>
        <tr v-else-if="filteredSets.length === 0">
          <td colspan="6">No results match your filter.</td>
        </tr>
      </tbody>
    </table>
    </template>

    <ConfirmDialog
      :open="!!deleteTarget"
      :message="`Delete set ${deleteTarget?.setNumber}?`"
      @confirm="doDelete"
      @cancel="deleteTarget = null"
    />
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { getAllSets, createSet, deleteSet } from '../../api/sets.js'
import ConfirmDialog from '../../components/ConfirmDialog.vue'
import SetArchiveTypeahead from '../../components/SetArchiveTypeahead.vue'

const sets = ref([])
const filterText = ref('')
const filteredSets = computed(() => {
  const q = filterText.value.trim().toLowerCase()
  if (!q) return sets.value
  return sets.value.filter(s =>
    s.setNumber.toLowerCase().includes(q) ||
    s.description.toLowerCase().includes(q)
  )
})
const loading = ref(true)
const error = ref('')
const deleteTarget = ref(null)
const form = ref({ setNumber: '', description: '', photoUrl: '', quantity: 1 })
const selectedArchiveSet = ref(null)

function onArchiveSelect(s) {
  selectedArchiveSet.value = s
  form.value.setNumber = s.setNum
  form.value.description = s.name
  form.value.photoUrl = s.imgUrl ?? ''
}

function clearSelected() {
  selectedArchiveSet.value = null
  form.value = { setNumber: '', description: '', photoUrl: '', quantity: 1 }
}

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
    selectedArchiveSet.value = null
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

<style scoped>
.selected-pill {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  background: #eef2ff;
  border: 1px solid #c7d2fe;
  border-radius: 6px;
  padding: 0.3rem 0.6rem;
  font-size: 0.9rem;
  flex-shrink: 0;
}

.clear-btn {
  background: none;
  border: none;
  cursor: pointer;
  color: #6366f1;
  font-size: 0.85rem;
  padding: 0 0.2rem;
  line-height: 1;
}

.thumb-cell {
  width: 40px;
  text-align: center;
}

.list-thumb {
  width: 36px;
  height: 36px;
  object-fit: contain;
}

@media (max-width: 640px) {
  .mobile-hide { display: none; }
}
</style>
