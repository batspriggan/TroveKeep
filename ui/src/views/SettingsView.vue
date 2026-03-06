<template>
  <div>
    <h1>Settings</h1>

    <section class="card">
      <h2>Features</h2>
      <div class="feature-row">
        <label class="toggle-label">
          <input type="checkbox" v-model="settings.bulkPiecesEnabled" />
          Bulk Pieces Management
        </label>
        <span class="muted">Show Bulk Pieces and Drawer Containers sections.<br>
        Note: if any Bulk Piece is already present it will not be removed</span>
      </div>
      <div class="feature-row">
        <label class="toggle-label">
          <input type="checkbox" v-model="settings.tablePlannerEnabled" />
          Table Planner
        </label>
        <span class="muted">Enable the Table Planner, including rooms, table templates and plates configuration.</span>
      </div>
    </section>

    <section class="card">
      <h2>Backup</h2>
      <p class="muted">Download a compressed snapshot of all data including Rebrickable catalog and cached images.</p>
      <p v-if="backupError" class="error">{{ backupError }}</p>
      <p v-if="backupSuccess" class="success">{{ backupSuccess }}</p>
      <button class="primary" :disabled="backupLoading" @click="handleBackup">
        {{ backupLoading ? 'Preparing…' : 'Download Backup' }}
      </button>
    </section>

    <section class="card">
      <h2>Restore</h2>
      <p class="muted">Upload a previously downloaded backup file. <strong>This will replace all current inventory data.</strong></p>
      <p v-if="restoreError" class="error">{{ restoreError }}</p>
      <p v-if="restoreSuccess" class="success">{{ restoreSuccess }}</p>
      <div class="restore-row">
        <input ref="fileInput" type="file" accept=".gz" @change="onFileChange" />
        <button class="danger" :disabled="!selectedFile || restoreLoading" @click="handleRestore">
          {{ restoreLoading ? 'Restoring…' : 'Restore' }}
        </button>
      </div>
    </section>

    <ConfirmDialog
      :open="confirmOpen"
      message="This will replace ALL current inventory data with the selected backup — are you sure?"
      @confirm="doRestore"
      @cancel="confirmOpen = false"
    />
  </div>
</template>

<script setup>
import { ref } from 'vue'
import ConfirmDialog from '../components/ConfirmDialog.vue'
import { downloadBackup, uploadRestore } from '../api/settings.js'
import { useSettings } from '../composables/useSettings.js'

const settings = useSettings()

const backupLoading = ref(false)
const backupError = ref('')
const backupSuccess = ref('')

async function handleBackup() {
  backupLoading.value = true
  backupError.value = ''
  backupSuccess.value = ''
  try {
    await downloadBackup()
    backupSuccess.value = 'Backup downloaded successfully.'
  } catch (e) {
    backupError.value = e.message
  } finally {
    backupLoading.value = false
  }
}

const fileInput = ref(null)
const selectedFile = ref(null)
const restoreLoading = ref(false)
const restoreError = ref('')
const restoreSuccess = ref('')
const confirmOpen = ref(false)

function onFileChange(e) {
  selectedFile.value = e.target.files[0] ?? null
  restoreError.value = ''
  restoreSuccess.value = ''
}

function handleRestore() {
  if (!selectedFile.value) return
  confirmOpen.value = true
}

async function doRestore() {
  confirmOpen.value = false
  restoreLoading.value = true
  restoreError.value = ''
  restoreSuccess.value = ''
  try {
    await uploadRestore(selectedFile.value)
    restoreSuccess.value = 'Restore complete. Reloading…'
    setTimeout(() => window.location.reload(), 1000)
  } catch (e) {
    restoreError.value = e.message
  } finally {
    restoreLoading.value = false
  }
}
</script>

<style scoped>
.card {
  background: #fff;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 1.5rem;
  margin-bottom: 1.5rem;
  max-width: 600px;
}

h2 {
  margin-top: 0;
}

.muted {
  color: #64748b;
  font-size: 0.875rem;
  margin-bottom: 1rem;
}

.feature-row {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.toggle-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.9rem;
  cursor: pointer;
}

.toggle-label input[type="checkbox"] {
  width: auto;
  cursor: pointer;
}

.restore-row {
  display: flex;
  align-items: center;
  gap: 1rem;
  flex-wrap: wrap;
}

.success {
  color: #16a34a;
  font-size: 0.875rem;
}

button.danger {
  background: #dc2626;
  color: #fff;
  border: none;
}

button.danger:hover:not(:disabled) {
  background: #b91c1c;
}

button.danger:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
