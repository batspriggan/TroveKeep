<template>
  <div>
    <h1>Settings</h1>

    <section class="card">
      <h2>Profile</h2>
      <div class="profile-row">
        <span class="profile-label">Name</span>
        <span>{{ auth.state.user?.displayName }}</span>
      </div>
      <div class="profile-row">
        <span class="profile-label">Email</span>
        <span>{{ auth.state.user?.email }}</span>
      </div>
      <div class="profile-row">
        <span class="profile-label">Role</span>
        <span>{{ auth.state.user?.role }}</span>
      </div>
    </section>

    <section class="card">
      <h2>User Mode</h2>
      <template v-if="appMode.multiUserEnabled">
        <p class="muted">Multi-user mode is active: accounts, roles and per-collection permissions apply.</p>
      </template>
      <template v-else>
        <p class="muted">
          Single-user mode: there is no login and everyone operates as the technical administrator.
          Accounts, roles and collection permissions are ignored until multi-user is enabled.
        </p>
        <button class="primary" type="button" @click="router.push('/admin/users')">
          Gestisci modalità utenti
        </button>
      </template>
    </section>

    <section class="card">
      <h2>Change Password</h2>
      <form @submit.prevent="changePassword">
        <div class="field-row">
          <label>Current password</label>
          <input v-model="pwd.current" type="password" autocomplete="current-password" required />
        </div>
        <div class="field-row">
          <label>New password (min 8 characters)</label>
          <input v-model="pwd.next" type="password" autocomplete="new-password" minlength="8" required />
        </div>
        <div class="field-row">
          <label>Confirm new password</label>
          <input v-model="pwd.confirm" type="password" autocomplete="new-password" minlength="8" required />
        </div>
        <p v-if="pwdError" class="error">{{ pwdError }}</p>
        <p v-if="pwdSuccess" class="success">{{ pwdSuccess }}</p>
        <button class="primary" type="submit" :disabled="pwdSaving">
          {{ pwdSaving ? 'Saving…' : 'Change password' }}
        </button>
      </form>
    </section>

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
        <span class="muted">Enable the Table Planner, including rooms and table templates.</span>
      </div>
    </section>

    <section class="card">
      <h2>Label Printing</h2>
      <p class="muted">
        Choose how labels reach the printer. In <strong>client</strong> mode the label files are
        downloaded and the user manages them (label-tool watch folder). In <strong>server</strong>
        mode labels are sent to a remote label-tool server over HTTP and printed there.
      </p>

      <div class="radio-row">
        <label class="toggle-label">
          <input type="radio" value="client" v-model="form.mode" />
          Client (download files)
        </label>
        <label class="toggle-label">
          <input type="radio" value="server" v-model="form.mode" />
          Server (print remotely)
        </label>
      </div>

      <template v-if="form.mode === 'server'">
        <div class="field-row">
          <label>Server URL</label>
          <input v-model="form.serverUrl" placeholder="http://192.168.10.14:9898" />
        </div>
        <div class="field-row">
          <label>Token (optional)</label>
          <input v-model="form.serverToken" type="password" :placeholder="config.hasServerToken ? '•••••••• (unchanged)' : 'no token'" />
        </div>
        <div class="server-test">
          <button class="secondary" :disabled="testing" @click="testConnection">
            {{ testing ? 'Testing…' : 'Test Connection' }}
          </button>
          <span v-if="health" class="health">
            <template v-if="health.printer">
              <span class="ok">● stampante: {{ health.printer }}</span>
              <span class="muted-inline">— coda: {{ health.pending }} pending, {{ health.printing }} in stampa, {{ health.failed }} falliti</span>
            </template>
            <span v-else class="warn">● server OK ma nessuna stampante collegata</span>
          </span>
          <span v-else-if="healthError" class="error">{{ healthError }}</span>
        </div>
      </template>

      <p v-if="saveError" class="error">{{ saveError }}</p>
      <p v-if="saveSuccess" class="success">{{ saveSuccess }}</p>
      <button class="primary" :disabled="saving || !configLoaded" @click="save">
        {{ saving ? 'Saving…' : 'Save Label Settings' }}
      </button>
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

    <!-- A restore rewrites every collection at once: admin only (SettingsController enforces it). -->
    <section v-if="auth.isAdmin" class="card">
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
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import ConfirmDialog from '../components/ConfirmDialog.vue'
import { downloadBackup, uploadRestore } from '../api/settings.js'
import { changePassword as changePasswordApi } from '../api/account.js'
import { useSettings } from '../composables/useSettings.js'
import { useAuth } from '../composables/useAuth.js'
import { useAppMode } from '../composables/useAppMode.js'
import { getLabelConfig, updateLabelConfig, getServerHealth } from '../api/labels.js'

const settings = useSettings()
const auth = useAuth()
const appMode = useAppMode()
const router = useRouter()

// ---- Profile / password ----
const pwd = reactive({ current: '', next: '', confirm: '' })
const pwdSaving = ref(false)
const pwdError = ref('')
const pwdSuccess = ref('')

async function changePassword() {
  pwdError.value = ''
  pwdSuccess.value = ''
  if (pwd.next !== pwd.confirm) {
    pwdError.value = 'The new passwords do not match.'
    return
  }
  pwdSaving.value = true
  try {
    await changePasswordApi(pwd.current, pwd.next)
    pwd.current = ''
    pwd.next = ''
    pwd.confirm = ''
    pwdSuccess.value = 'Password changed.'
  } catch (e) {
    pwdError.value = e.message
  } finally {
    pwdSaving.value = false
  }
}

// ---- Label printing ----
const config = reactive({ mode: 'client', serverUrl: null, hasServerToken: false })
const configLoaded = ref(false)
const form = reactive({ mode: 'client', serverUrl: '', serverToken: '' })
const saving = ref(false)
const saveError = ref('')
const saveSuccess = ref('')
const testing = ref(false)
const health = ref(null)
const healthError = ref('')

async function loadLabelConfig() {
  try {
    const cfg = await getLabelConfig()
    config.mode = cfg.mode
    config.serverUrl = cfg.serverUrl
    config.hasServerToken = cfg.hasServerToken
    form.mode = cfg.mode
    form.serverUrl = cfg.serverUrl ?? ''
    configLoaded.value = true
  } catch (e) {
    saveError.value = `Impossibile leggere la configurazione: ${e.message}`
  }
}

async function save() {
  saving.value = true
  saveError.value = ''
  saveSuccess.value = ''
  try {
    const cfg = await updateLabelConfig({
      mode: form.mode,
      serverUrl: form.mode === 'server' ? form.serverUrl : null,
      serverToken: form.serverToken || null,
    })
    config.mode = cfg.mode
    config.serverUrl = cfg.serverUrl
    config.hasServerToken = cfg.hasServerToken
    form.serverToken = ''
    saveSuccess.value = 'Label settings saved.'
  } catch (e) {
    saveError.value = e.message
  } finally {
    saving.value = false
  }
}

async function testConnection() {
  testing.value = true
  health.value = null
  healthError.value = ''
  try {
    health.value = await getServerHealth()
  } catch (e) {
    healthError.value = 'Server non raggiungibile (salva la configurazione prima di testare).'
  } finally {
    testing.value = false
  }
}

onMounted(loadLabelConfig)

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

.profile-row {
  display: flex;
  gap: 0.75rem;
  font-size: 0.9rem;
  padding: 0.2rem 0;
}

.profile-label {
  flex: 0 0 90px;
  color: #64748b;
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

.radio-row {
  display: flex;
  gap: 1.5rem;
  margin-bottom: 1rem;
}

.field-row {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  margin-bottom: 0.75rem;
}

.field-row label {
  font-size: 0.875rem;
  color: #475569;
}

.field-row input {
  padding: 0.5rem;
  border: 1px solid #cbd5e1;
  border-radius: 4px;
  font-size: 0.9rem;
}

.server-test {
  display: flex;
  align-items: center;
  gap: 1rem;
  margin-bottom: 1rem;
  flex-wrap: wrap;
}

.health {
  font-size: 0.875rem;
  display: flex;
  gap: 0.5rem;
  align-items: center;
  flex-wrap: wrap;
}

.health .ok {
  color: #16a34a;
}

.health .warn {
  color: #d97706;
}

.muted-inline {
  color: #64748b;
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
