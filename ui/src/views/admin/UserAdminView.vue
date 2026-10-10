<template>
  <div class="admin-page">
    <h1>Users</h1>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-if="notice" class="success">{{ notice }}</p>

    <!-- Create -->
    <section class="card">
      <h2>New user</h2>
      <form class="form-row" @submit.prevent="create">
        <div class="form-field">
          <label>Email *</label>
          <input v-model.trim="form.email" type="email" required placeholder="user@example.com" />
        </div>
        <div class="form-field">
          <label>Display name *</label>
          <input v-model.trim="form.displayName" required placeholder="Mario Rossi" />
        </div>
        <div class="form-field">
          <label>Password *</label>
          <input v-model="form.password" type="password" required minlength="8" placeholder="min 8 chars" />
        </div>
        <div class="form-field field-role">
          <label>Role</label>
          <select v-model="form.role">
            <option v-for="r in roles" :key="r" :value="r">{{ r }}</option>
          </select>
        </div>
        <button class="primary" type="submit" :disabled="creating">
          {{ creating ? 'Creating…' : 'Create user' }}
        </button>
      </form>
    </section>

    <!-- Edit -->
    <section v-if="editing" class="card">
      <h2>Edit {{ editing.email }}</h2>
      <form class="form-row" @submit.prevent="saveEdit">
        <div class="form-field">
          <label>Display name *</label>
          <input v-model.trim="editForm.displayName" required />
        </div>
        <div class="form-field field-role">
          <label>Role</label>
          <select v-model="editForm.role">
            <option v-for="r in roles" :key="r" :value="r">{{ r }}</option>
          </select>
        </div>
        <div class="form-field field-check">
          <label class="check-label">
            <input type="checkbox" v-model="editForm.active" />
            Active
          </label>
        </div>
        <div class="actions">
          <button class="primary" type="submit" :disabled="savingEdit">
            {{ savingEdit ? 'Saving…' : 'Save' }}
          </button>
          <button type="button" class="secondary" @click="editing = null">Cancel</button>
        </div>
      </form>
    </section>

    <!-- Reset password -->
    <section v-if="passwordTarget" class="card">
      <h2>Reset password for {{ passwordTarget.email }}</h2>
      <form class="form-row" @submit.prevent="savePassword">
        <div class="form-field">
          <label>New password *</label>
          <input v-model="newPassword" type="password" required minlength="8" placeholder="min 8 chars" />
        </div>
        <div class="actions">
          <button class="primary" type="submit" :disabled="savingPassword">
            {{ savingPassword ? 'Saving…' : 'Set password' }}
          </button>
          <button type="button" class="secondary" @click="passwordTarget = null">Cancel</button>
        </div>
      </form>
    </section>

    <p v-if="loading" class="loading-msg">Loading…</p>

    <template v-else>
      <table class="users-table">
        <thead>
          <tr>
            <th>Email</th>
            <th>Display name</th>
            <th>Role</th>
            <th>Status</th>
            <th>Auth</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="u in users" :key="u.id" :class="{ inactive: !u.active }">
            <td class="td-email">{{ u.email }}</td>
            <td>{{ u.displayName }}</td>
            <td><span class="role-badge" :class="u.role.toLowerCase()">{{ u.role }}</span></td>
            <td>{{ u.active ? 'Active' : 'Disabled' }}</td>
            <td class="td-auth">{{ authLabel(u) }}</td>
            <td class="td-action">
              <button class="link-btn" @click="startEdit(u)">Edit</button>
              <button class="link-btn" @click="startPassword(u)">Password</button>
              <button class="link-btn danger-text" @click="deleteTarget = u">Delete</button>
            </td>
          </tr>
          <tr v-if="users.length === 0">
            <td colspan="6" class="empty-cell">No users yet.</td>
          </tr>
        </tbody>
      </table>
    </template>

    <ConfirmDialog
      :open="!!deleteTarget"
      :message="`Delete user ${deleteTarget?.email}? This cannot be undone.`"
      @confirm="doDelete"
      @cancel="deleteTarget = null"
    />
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import ConfirmDialog from '../../components/ConfirmDialog.vue'
import {
  getAllUsers,
  createUser,
  updateUser,
  setUserPassword,
  deleteUser,
} from '../../api/users.js'

const roles = ['Admin', 'User', 'Guest']

const users = ref([])
const loading = ref(true)
const error = ref('')
const notice = ref('')

const form = reactive({ email: '', displayName: '', password: '', role: 'User' })
const creating = ref(false)

const editing = ref(null)
const editForm = reactive({ displayName: '', role: 'User', active: true })
const savingEdit = ref(false)

const passwordTarget = ref(null)
const newPassword = ref('')
const savingPassword = ref(false)

const deleteTarget = ref(null)

function clearMessages() {
  error.value = ''
  notice.value = ''
}

function authLabel(u) {
  if (u.hasPassword && u.externalProvider) return `Password + ${u.externalProvider}`
  if (u.hasPassword) return 'Password'
  if (u.externalProvider) return u.externalProvider
  return '—'
}

async function load() {
  loading.value = true
  clearMessages()
  try {
    users.value = await getAllUsers()
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

async function create() {
  clearMessages()
  creating.value = true
  try {
    await createUser({ ...form })
    form.email = ''
    form.displayName = ''
    form.password = ''
    form.role = 'User'
    notice.value = 'User created.'
    await load()
  } catch (e) {
    error.value = e.message
  } finally {
    creating.value = false
  }
}

function startEdit(u) {
  clearMessages()
  editing.value = u
  editForm.displayName = u.displayName
  editForm.role = u.role
  editForm.active = u.active
}

async function saveEdit() {
  clearMessages()
  savingEdit.value = true
  try {
    await updateUser(editing.value.id, {
      displayName: editForm.displayName,
      role: editForm.role,
      active: editForm.active,
    })
    notice.value = 'User updated.'
    editing.value = null
    await load()
  } catch (e) {
    error.value = e.message
  } finally {
    savingEdit.value = false
  }
}

function startPassword(u) {
  clearMessages()
  passwordTarget.value = u
  newPassword.value = ''
}

async function savePassword() {
  clearMessages()
  savingPassword.value = true
  try {
    await setUserPassword(passwordTarget.value.id, newPassword.value)
    notice.value = `Password updated for ${passwordTarget.value.email}.`
    passwordTarget.value = null
    newPassword.value = ''
  } catch (e) {
    error.value = e.message
  } finally {
    savingPassword.value = false
  }
}

async function doDelete() {
  clearMessages()
  const target = deleteTarget.value
  deleteTarget.value = null
  try {
    await deleteUser(target.id)
    if (editing.value?.id === target.id) editing.value = null
    if (passwordTarget.value?.id === target.id) passwordTarget.value = null
    notice.value = 'User deleted.'
    await load()
  } catch (e) {
    error.value = e.message
  }
}

onMounted(load)
</script>

<style scoped>
.admin-page {
  font-family: var(--font-body);
}

.success {
  color: var(--color-success);
  font-size: var(--text-sm);
  margin-bottom: var(--space-2);
}

.loading-msg {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  margin: var(--space-4) 0;
}

.form-field label {
  display: block;
  font-size: var(--text-sm);
  font-weight: 500;
  color: var(--color-text-secondary);
  margin-bottom: var(--space-1);
}

.field-role {
  flex: 0 0 120px;
}

.field-check {
  align-self: flex-end;
  padding-bottom: 0.4rem;
}

.check-label {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  font-size: var(--text-sm);
}

.check-label input {
  width: auto;
}

.actions {
  align-self: flex-end;
}

.users-table {
  font-size: var(--text-sm);
}

.users-table th {
  font-family: var(--font-display);
  font-size: var(--text-xs);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
}

.users-table tr.inactive td {
  color: var(--color-text-muted);
}

.td-email {
  font-family: var(--font-mono);
  font-size: var(--text-sm);
}

.td-auth {
  color: var(--color-text-secondary);
}

.td-action {
  text-align: right;
  white-space: nowrap;
}

.link-btn {
  background: none;
  border: none;
  color: var(--color-info);
  cursor: pointer;
  padding: 0 0.4rem;
  font-size: var(--text-sm);
  min-height: unset;
}

.link-btn:hover {
  text-decoration: underline;
  opacity: 1;
}

.danger-text {
  color: #dc2626;
}

.role-badge {
  display: inline-block;
  font-size: var(--text-xs);
  font-weight: 600;
  border-radius: 3px;
  padding: 1px 6px;
  background: var(--color-surface-alt);
  color: var(--color-text-secondary);
}

.role-badge.admin {
  background: #fff0f0;
  color: #b91c1c;
}

.role-badge.guest {
  background: #eff6ff;
  color: #1d4ed8;
}

.empty-cell {
  text-align: center;
  color: var(--color-text-muted);
  padding: var(--space-6);
}
</style>
