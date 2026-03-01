<template>
  <div>
    <h1>Bulk Pieces</h1>

    <form @submit.prevent="submit">
      <div class="form-row">
        <div class="form-field">
          <label>Lego ID *</label>
          <PartArchiveTypeahead v-model="form.legoId" @select="onPartSelect" />
        </div>
      </div>
      <div class="form-row">
        <div class="form-field">
          <label>Color *</label>
          <ColorSelect v-model="form.legoColorUid" :colors="colors" />
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
      </div>
    </form>

    <p v-if="error" class="error">{{ error }}</p>

    <p v-if="loading">Loading…</p>

    <template v-else>
    <input v-model="filterText" type="search" placeholder="Filter by Lego ID or description…" style="margin: 0.5rem 0; width: 100%; max-width: 400px;" />

    <table>
      <thead>
        <tr>
          <th class="thumb-col"></th>
          <th>Lego ID</th>
          <th>Color</th>
          <th>Description</th>
          <th>Qty</th>
          <th>Storage</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="p in filteredPieces" :key="p.id">
          <td class="thumb-col">
            <img v-if="p.imageCached" :src="`/api/bulkpieces/${p.id}/image`" class="list-thumb" alt="" />
          </td>
          <td>
            <RouterLink :to="`/bulkpieces/${p.id}`">{{ p.legoId }}</RouterLink>
          </td>
          <td>
            <span v-if="p.legoColorRgb" class="swatch" :style="{ background: '#' + p.legoColorRgb }"></span>
            <span>{{ p.legoColorName ?? `#${p.legoColorId}` }}</span>
          </td>
          <td>{{ p.description }}</td>
          <td>{{ p.quantity }}</td>
          <td>{{ (p.storageAllocations && p.storageAllocations.length) ? `${p.storageAllocations.length} location(s)` : '—' }}</td>
          <td>
            <button class="danger" @click="confirmDelete(p)">Delete</button>
          </td>
        </tr>
        <tr v-if="pieces.length === 0">
          <td colspan="6">No bulk pieces yet.</td>
        </tr>
        <tr v-else-if="filteredPieces.length === 0">
          <td colspan="6">No results match your filter.</td>
        </tr>
      </tbody>
    </table>
    </template>

    <ConfirmDialog
      :open="!!deleteTarget"
      :message="`Delete ${deleteTarget?.legoId} (${deleteTarget?.legoColorName ?? deleteTarget?.legoColorId})?`"
      @confirm="doDelete"
      @cancel="deleteTarget = null"
    />
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { getAllBulkPieces, createBulkPiece, deleteBulkPiece } from '../../api/bulkpieces.js'
import { getColorsList } from '../../api/archives.js'
import ConfirmDialog from '../../components/ConfirmDialog.vue'
import ColorSelect from '../../components/ColorSelect.vue'
import PartArchiveTypeahead from '../../components/PartArchiveTypeahead.vue'

const pieces = ref([])
const colors = ref([])
const filterText = ref('')
const filteredPieces = computed(() => {
  const q = filterText.value.trim().toLowerCase()
  if (!q) return pieces.value
  return pieces.value.filter(p =>
    p.legoId.toLowerCase().includes(q) ||
    p.description.toLowerCase().includes(q)
  )
})
const loading = ref(true)
const error = ref('')
const deleteTarget = ref(null)
const form = ref({ legoId: '', legoColorUid: '', description: '', quantity: 1 })

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [allPieces, allColors] = await Promise.all([getAllBulkPieces(), getColorsList()])
    pieces.value = allPieces
    colors.value = allColors
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

function onPartSelect(part) {
  form.value.description = part.name
}

async function submit() {
  error.value = ''
  const missing = []
  if (!form.value.legoId.trim()) missing.push('Lego ID')
  if (!form.value.legoColorUid) missing.push('Color (if the list is empty, import colors in the Archives)')
  if (!form.value.description.trim()) missing.push('Description')
  if (missing.length) {
    error.value = `Required fields missing: ${missing.join(', ')}.`
    return
  }
  const legoColorId = colors.value.find(c => c.uniqueId === form.value.legoColorUid)?.id
  try {
    await createBulkPiece({ legoId: form.value.legoId, legoColorId, description: form.value.description, quantity: form.value.quantity })
    form.value = { legoId: '', legoColorUid: '', description: '', quantity: 1 }
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

<style scoped>
.thumb-col {
  width: 40px;
  text-align: center;
}

.list-thumb {
  width: 36px;
  height: 36px;
  object-fit: contain;
}

.swatch {
  display: inline-block;
  width: 0.9rem;
  height: 0.9rem;
  border-radius: 2px;
  border: 1px solid rgba(0, 0, 0, 0.15);
  vertical-align: middle;
  margin-right: 0.35rem;
}
</style>
