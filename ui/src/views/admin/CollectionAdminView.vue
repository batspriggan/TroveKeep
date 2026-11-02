<template>
  <div class="admin-page">
    <h1>Collections</h1>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-if="notice" class="success">{{ notice }}</p>

    <!-- Create (admin only: an Owner manages the members of the collections it owns) -->
    <section v-if="isAdmin" class="card">
      <h2>New collection</h2>
      <form class="form-row" @submit.prevent="create">
        <div class="form-field">
          <label>Name *</label>
          <input v-model.trim="form.name" required placeholder="e.g. City Collection" />
        </div>
        <div class="form-field">
          <label>Description</label>
          <input v-model.trim="form.description" placeholder="optional" />
        </div>
        <button class="primary" type="submit" :disabled="creating">
          {{ creating ? 'Creating…' : 'Create collection' }}
        </button>
      </form>
    </section>

    <!-- Edit (admin only) -->
    <section v-if="editing && isAdmin" class="card">
      <h2>Edit {{ editing.name }}</h2>
      <form class="form-row" @submit.prevent="saveEdit">
        <div class="form-field">
          <label>Name *</label>
          <input v-model.trim="editForm.name" required />
        </div>
        <div class="form-field">
          <label>Description</label>
          <input v-model.trim="editForm.description" />
        </div>
        <div class="actions">
          <button class="primary" type="submit" :disabled="savingEdit">
            {{ savingEdit ? 'Saving…' : 'Save' }}
          </button>
          <button type="button" class="secondary" @click="editing = null">Cancel</button>
        </div>
      </form>
    </section>

    <p v-if="loading" class="loading-msg">Loading…</p>

    <template v-else>
      <table class="collections-table">
        <thead>
          <tr>
            <th>Name</th>
            <th>Description</th>
            <th>Created</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <template v-for="c in collections" :key="c.id">
            <tr>
              <td class="td-name">{{ c.name }}</td>
              <td class="td-desc">{{ c.description || '—' }}</td>
              <td class="td-date">{{ formatDate(c.createdAt) }}</td>
              <td class="td-action">
                <!-- Members: admin, or the Owner of this collection. An Editor sees nothing. -->
                <button
                  v-if="auth.canManageMembersIn(c.id)"
                  class="link-btn"
                  @click="toggleMembers(c)"
                >
                  {{ openId === c.id ? 'Hide members' : 'Members' }}
                </button>
                <button v-if="isAdmin" class="link-btn" @click="startEdit(c)">Edit</button>
                <button v-if="isAdmin" class="link-btn danger-text" @click="deleteTarget = c">Delete</button>
              </td>
            </tr>

            <tr v-if="openId === c.id" class="members-row">
              <td colspan="4">
                <div class="members-panel">
                  <p v-if="membersError" class="error">{{ membersError }}</p>
                  <p v-if="membersLoading" class="muted-inline">Loading members…</p>

                  <template v-else>
                    <div v-if="members.length === 0" class="muted-inline">No members yet.</div>
                    <ul v-else class="member-list">
                      <li v-for="m in members" :key="m.userId" class="member-item">
                        <span class="member-email">{{ m.email }}</span>
                        <span class="member-name">{{ m.displayName }}</span>
                        <span class="access-badge">{{ m.access }}</span>
                        <button class="link-btn danger-text" @click="removeMember(m)">Remove</button>
                      </li>
                    </ul>

                    <form class="add-member" @submit.prevent="addMember">
                      <select v-model="memberForm.userId" required>
                        <option value="" disabled>Select user…</option>
                        <option v-for="u in addableUsers" :key="u.id" :value="u.id">
                          {{ u.displayName }} ({{ u.email }})
                        </option>
                      </select>
                      <select v-model="memberForm.access" class="access-select">
                        <option v-for="a in accesses" :key="a" :value="a">{{ a }}</option>
                      </select>
                      <button class="primary" type="submit" :disabled="addingMember || !memberForm.userId">
                        {{ addingMember ? 'Adding…' : 'Add member' }}
                      </button>
                    </form>
                    <p v-if="addableUsers.length === 0" class="muted-inline">
                      Every user is already a member.
                    </p>
                  </template>
                </div>
              </td>
            </tr>
          </template>

          <tr v-if="collections.length === 0">
            <td colspan="4" class="empty-cell">No collections yet.</td>
          </tr>
        </tbody>
      </table>
    </template>

    <ConfirmDialog
      :open="!!deleteTarget"
      :message="`Delete collection ${deleteTarget?.name}? Items assigned to it may become inaccessible.`"
      @confirm="doDelete"
      @cancel="deleteTarget = null"
    />
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import ConfirmDialog from '../../components/ConfirmDialog.vue'
import {
  getAllCollections,
  createCollection,
  updateCollection,
  deleteCollection,
  getCollectionMembers,
  getCollectionMemberCandidates,
  addCollectionMember,
  removeCollectionMember,
} from '../../api/collections.js'
import { useAuth } from '../../composables/useAuth.js'

const auth = useAuth()
// Creating, renaming and deleting a collection is an admin operation; an Owner only manages the
// members of the collections it owns. The backend enforces both, this only shapes the page.
const isAdmin = computed(() => auth.isAdmin)

const accesses = ['Viewer', 'Editor', 'Owner']

const collections = ref([])
const loading = ref(true)
const error = ref('')
const notice = ref('')

const form = reactive({ name: '', description: '' })
const creating = ref(false)

const editing = ref(null)
const editForm = reactive({ name: '', description: '' })
const savingEdit = ref(false)

const deleteTarget = ref(null)

// Members of the single expanded collection, plus the accounts that could be added to it (fetched
// per collection: GET /api/users is admin-only, Owners get the candidates endpoint instead).
const openId = ref(null)
const members = ref([])
const candidates = ref([])
const membersLoading = ref(false)
const membersError = ref('')
const memberForm = reactive({ userId: '', access: 'Editor' })
const addingMember = ref(false)

const addableUsers = computed(() =>
  candidates.value.map((c) => ({ id: c.userId, email: c.email, displayName: c.displayName })),
)

function clearMessages() {
  error.value = ''
  notice.value = ''
}

function formatDate(value) {
  if (!value) return '—'
  return new Date(value).toLocaleDateString()
}

async function load() {
  loading.value = true
  clearMessages()
  try {
    collections.value = await getAllCollections()
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
    await createCollection({ name: form.name, description: form.description || null })
    form.name = ''
    form.description = ''
    notice.value = 'Collection created.'
    // The create forms in the other views decide whether to ask for a collection from
    // auth.collections, which is only refreshed by /api/auth/me: without this the new collection
    // would not be offered until the next login.
    await auth.fetchMe()
    await load()
  } catch (e) {
    error.value = e.message
  } finally {
    creating.value = false
  }
}

function startEdit(c) {
  clearMessages()
  editing.value = c
  editForm.name = c.name
  editForm.description = c.description ?? ''
}

async function saveEdit() {
  clearMessages()
  savingEdit.value = true
  try {
    await updateCollection(editing.value.id, {
      name: editForm.name,
      description: editForm.description || null,
    })
    notice.value = 'Collection updated.'
    editing.value = null
    await load()
  } catch (e) {
    error.value = e.message
  } finally {
    savingEdit.value = false
  }
}

async function doDelete() {
  clearMessages()
  const target = deleteTarget.value
  deleteTarget.value = null
  try {
    await deleteCollection(target.id)
    if (editing.value?.id === target.id) editing.value = null
    if (openId.value === target.id) openId.value = null
    notice.value = 'Collection deleted.'
    await auth.fetchMe()
    await load()
  } catch (e) {
    error.value = e.message
  }
}

async function toggleMembers(c) {
  clearMessages()
  if (openId.value === c.id) {
    openId.value = null
    return
  }
  openId.value = c.id
  memberForm.userId = ''
  memberForm.access = 'Editor'
  await loadMembers(c.id)
}

async function loadMembers(id) {
  membersLoading.value = true
  membersError.value = ''
  members.value = []
  candidates.value = []
  try {
    members.value = await getCollectionMembers(id)
    candidates.value = await getCollectionMemberCandidates(id)
  } catch (e) {
    membersError.value = e.message
  } finally {
    membersLoading.value = false
  }
}

async function addMember() {
  membersError.value = ''
  addingMember.value = true
  try {
    await addCollectionMember(openId.value, memberForm.userId, memberForm.access)
    memberForm.userId = ''
    memberForm.access = 'Editor'
    await loadMembers(openId.value)
  } catch (e) {
    membersError.value = e.message
  } finally {
    addingMember.value = false
  }
}

async function removeMember(m) {
  membersError.value = ''
  try {
    await removeCollectionMember(openId.value, m.userId)
    await loadMembers(openId.value)
  } catch (e) {
    membersError.value = e.message
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

.actions {
  align-self: flex-end;
}

.collections-table {
  font-size: var(--text-sm);
}

.collections-table th {
  font-family: var(--font-display);
  font-size: var(--text-xs);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
}

.td-name {
  font-weight: 500;
}

.td-desc,
.td-date {
  color: var(--color-text-secondary);
}

.td-date {
  white-space: nowrap;
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

.members-row td {
  background: var(--color-surface-alt);
  padding: var(--space-3) var(--space-4);
}

.member-list {
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  margin-bottom: var(--space-3);
}

.member-item {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  font-size: var(--text-sm);
}

.member-email {
  font-family: var(--font-mono);
}

.member-name {
  color: var(--color-text-secondary);
}

.access-badge {
  font-size: var(--text-xs);
  font-weight: 600;
  border-radius: 3px;
  padding: 1px 6px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  color: var(--color-text-secondary);
}

.member-item .link-btn {
  margin-left: auto;
}

.add-member {
  display: flex;
  gap: var(--space-2);
  flex-wrap: wrap;
  align-items: center;
}

.add-member select {
  flex: 1 1 220px;
  min-width: 0;
}

.access-select {
  flex: 0 0 120px;
}

.muted-inline {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

.empty-cell {
  text-align: center;
  color: var(--color-text-muted);
  padding: var(--space-6);
}
</style>
