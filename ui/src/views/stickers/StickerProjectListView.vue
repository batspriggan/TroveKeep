<template>
  <div class="project-page">
    <div class="page-header">
      <div>
        <h1>Sticker projects</h1>
        <p class="subtitle">
          A project is one print job: artwork from the
          <RouterLink to="/stickers">sticker library</RouterLink>, each with its own tile
          footprint, copy count and fit. Open one to compose it, then generate the A4 sheet.
        </p>
      </div>
    </div>

    <form v-if="canWrite" class="card create-card" @submit.prevent="create">
      <h2>New project</h2>
      <div class="form-row">
        <div class="form-field">
          <label>Name *</label>
          <input v-model="newName" maxlength="80" required placeholder="e.g. City vehicles" />
        </div>
        <button class="primary" type="submit" :disabled="creating || !newName.trim()">
          {{ creating ? 'Creating…' : 'Create project' }}
        </button>
      </div>
    </form>

    <p v-if="!canWrite" class="notice">
      Projects are shared by the whole instance. Your access is read-only here: you can open and
      print a project, but changing one needs
      <strong>Editor access in at least one collection</strong>.
    </p>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-if="loading" class="muted">Loading the projects…</p>

    <section v-else-if="projects.length === 0" class="card empty">
      <p>No sticker projects yet.</p>
      <p v-if="canWrite">Create one above, then add artwork to it.</p>
    </section>

    <div v-else class="list">
      <article v-for="project in projects" :key="project.id" class="card project-card">
        <div class="project-main">
          <RouterLink :to="`/sticker-projects/${project.id}`" class="project-name">
            {{ project.name }}
          </RouterLink>
          <div class="meta">
            <span><strong>{{ project.items.length }}</strong> item{{ project.items.length === 1 ? '' : 's' }}</span>
            <span><strong>{{ labelsOf(project) }}</strong> label{{ labelsOf(project) === 1 ? '' : 's' }}</span>
            <span class="muted">created {{ formatDate(project.createdAt) }}</span>
            <span class="muted">updated {{ formatDate(project.updatedAt) }}</span>
          </div>
        </div>
        <div class="project-actions">
          <RouterLink :to="`/sticker-projects/${project.id}`">
            <button>{{ canWrite ? 'Open' : 'View' }}</button>
          </RouterLink>
          <button v-if="canWrite" class="danger" @click="pendingDelete = project">Delete</button>
        </div>
      </article>
    </div>

    <ConfirmDialog :open="!!pendingDelete" :message="deleteMessage"
                   @confirm="confirmDelete" @cancel="pendingDelete = null" />
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import ConfirmDialog from '../../components/ConfirmDialog.vue'
import { useAuth } from '../../composables/useAuth.js'
import { createStickerProject, deleteStickerProject, getStickerProjects } from '../../api/stickerProjects.js'

const router = useRouter()
const auth = useAuth()
const canWrite = computed(() => auth.canWriteAnywhere)

const projects = ref([])
const loading = ref(true)
const error = ref('')
const creating = ref(false)
const newName = ref('')
const pendingDelete = ref(null)

const deleteMessage = computed(() =>
  pendingDelete.value ? `Delete the project "${pendingDelete.value.name}"?` : '',
)

/** Copies requested by the project, i.e. how many labels it prints. */
const labelsOf = (project) =>
  (project.items ?? []).reduce((n, item) => n + (Number(item.copies) || 0), 0)

const formatDate = (value) => (value ? new Date(value).toLocaleDateString() : '—')

async function load() {
  loading.value = true
  error.value = ''
  try {
    projects.value = (await getStickerProjects()) ?? []
  } catch (e) {
    error.value = e.message || 'Could not load the sticker projects.'
    projects.value = []
  } finally {
    loading.value = false
  }
}

async function create() {
  const name = newName.value.trim()
  if (!name) return
  creating.value = true
  error.value = ''
  try {
    const created = await createStickerProject({ name })
    newName.value = ''
    router.push(`/sticker-projects/${created.id}`)
  } catch (e) {
    error.value = e.message || 'Could not create the project.'
  } finally {
    creating.value = false
  }
}

async function confirmDelete() {
  const project = pendingDelete.value
  pendingDelete.value = null
  if (!project) return
  error.value = ''
  try {
    await deleteStickerProject(project.id)
    projects.value = projects.value.filter((p) => p.id !== project.id)
  } catch (e) {
    error.value = e.message || 'Could not delete the project.'
  }
}

onMounted(load)
</script>

<style scoped>
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
}

.subtitle {
  font-size: 0.85rem;
  color: #64748b;
  max-width: 65ch;
  margin-bottom: 1rem;
}

.muted {
  color: #64748b;
  font-size: 0.85rem;
}

.notice {
  background: #fefce8;
  border: 1px solid #fde68a;
  border-radius: 6px;
  padding: 0.7rem 0.9rem;
  font-size: 0.85rem;
  margin-bottom: 1rem;
}

.create-card h2 {
  font-size: 1.05rem;
}

.list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.project-card {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  flex-wrap: wrap;
  margin-bottom: 0;
}

.project-main {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  min-width: 220px;
}

.project-name {
  font-size: 1.05rem;
  font-weight: 600;
}

.meta {
  display: flex;
  gap: 1rem;
  flex-wrap: wrap;
  font-size: 0.8rem;
  color: #475569;
}

.project-actions {
  display: flex;
  gap: 0.4rem;
  align-items: center;
}

.empty {
  color: #64748b;
  font-size: 0.9rem;
}

.empty p + p {
  margin-top: 0.35rem;
}
</style>
