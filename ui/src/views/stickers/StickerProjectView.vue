<template>
  <div class="project-page">
    <RouterLink to="/sticker-projects" class="back-link">← Sticker projects</RouterLink>

    <p v-if="loading" class="muted">Loading the project…</p>
    <p v-else-if="loadError" class="error">{{ loadError }}</p>

    <template v-else-if="project">
      <div class="page-header">
        <div class="title-block">
          <input v-if="canWrite" v-model="project.name" class="name-input" maxlength="80"
                 aria-label="Project name" @input="scheduleSave()" />
          <h1 v-else>{{ project.name }}</h1>
          <span class="save-state" :class="saveState">{{ saveLabel }}</span>
        </div>
        <div class="summary">
          <span><strong>{{ project.items.length }}</strong> item{{ project.items.length === 1 ? '' : 's' }}</span>
          <span><strong>{{ totalLabels }}</strong> label{{ totalLabels === 1 ? '' : 's' }}</span>
        </div>
      </div>

      <p v-if="saveError" class="error">{{ saveError }}</p>
      <p v-if="!canWrite" class="notice">
        This project is shared by the whole instance. Your access is read-only here: you can print
        it, but editing the items needs
        <strong>Editor access in at least one collection</strong>.
      </p>

      <!-- ── Add an item ── -->
      <section v-if="canWrite" class="card">
        <h2>Add an item</h2>
        <StickerImagePicker v-model="draft.imageId" :images="images" />

        <div class="form-row item-form">
          <div class="form-field">
            <label>Mount</label>
            <select v-model="draft.mount">
              <option v-for="mount in MOUNT_OPTIONS" :key="mount.value" :value="mount.value">
                {{ mount.label }} — {{ mount.hint }}
              </option>
            </select>
          </div>
          <!-- Tile: the label is width × depth in studs, so the tile presets apply. -->
          <template v-if="!draftIsBrickStack">
            <div class="form-field">
              <label>Footprint</label>
              <select v-model="draft.preset">
                <option v-for="preset in presets" :key="preset.key" :value="preset.key">
                  {{ preset.label }} — {{ preset.widthStuds * STUD_MM }}×{{ preset.depthStuds * STUD_MM }} mm
                </option>
                <option value="custom">Custom…</option>
              </select>
            </div>
            <template v-if="draft.preset === 'custom'">
              <div class="form-field small">
                <label>Width (studs)</label>
                <input v-model.number="draft.widthStuds" type="number" min="1" max="32" />
              </div>
              <div class="form-field small">
                <label>Depth (studs)</label>
                <input v-model.number="draft.depthStuds" type="number" min="1" max="32" />
              </div>
            </template>
          </template>
          <!-- Brick stack: only the facade is printed, so the second field is the height in bricks. -->
          <template v-else>
            <div class="form-field small">
              <label>Facade length (studs)</label>
              <input v-model.number="draft.widthStuds" type="number" min="1" max="32" step="1" />
            </div>
            <div class="form-field small">
              <label>Brick height (bricks)</label>
              <input v-model.number="draft.brickHeight" type="number" min="0.01" step="0.01" />
            </div>
          </template>
          <div class="form-field small">
            <label>Copies</label>
            <input v-model.number="draft.copies" type="number" min="1" max="999" />
          </div>
          <div class="form-field">
            <label>Fit</label>
            <select v-model="draft.fit">
              <option v-for="fit in FIT_OPTIONS" :key="fit.value" :value="fit.value">
                {{ fit.label }} — {{ fit.hint }}
              </option>
            </select>
          </div>
          <div class="form-field">
            <label>Artwork orientation</label>
            <select v-model="draft.orientation">
              <option v-for="orientation in ORIENTATION_OPTIONS" :key="orientation.value"
                      :value="orientation.value">
                {{ orientation.label }} — {{ orientation.hint }}
              </option>
            </select>
          </div>
          <div class="form-field">
            <label>Notes (optional)</label>
            <input v-model="draft.notes" maxlength="500" placeholder="e.g. windscreen side" />
          </div>
          <button class="primary" :disabled="saveState === 'saving'" @click="addItem">Add item</button>
        </div>
        <p v-if="itemError" class="error">{{ itemError }}</p>
      </section>

      <!-- ── Items ── -->
      <section class="card">
        <h2>Items</h2>
        <p v-if="project.items.length === 0" class="muted">
          No items yet: pick an artwork above to build the sheet.
        </p>

        <div v-else class="items">
          <div v-for="item in project.items" :key="item.id" class="item">
            <div class="thumb" :class="{ missing: !item.imageCached }">
              <img v-if="item.imageCached" :src="artworkUrl(item)" :alt="item.imageName || ''" loading="lazy" />
              <span v-else class="missing-label">no artwork</span>
            </div>

            <div class="item-name">
              <strong :title="item.imageName || ''">{{ item.imageName || 'Missing image' }}</strong>
              <span class="muted">{{ dimsLabel(item) }} · {{ mmLabel(item) }}</span>
            </div>

            <!-- The mount is editable like `fit` and `orientation`: a wrong choice must be
                 repairable without deleting the row. Its own two fields follow it. -->
            <div v-if="canWrite" class="mount-cell">
              <select :value="normalizeMount(item.mount)"
                      @change="setMount(item, $event.target.value)">
                <option v-for="mount in MOUNT_OPTIONS" :key="mount.value" :value="mount.value">
                  {{ mount.label }}
                </option>
              </select>
            </div>
            <div v-else class="mount-cell">
              <span class="muted">{{ mountLabel(item.mount) }}</span>
            </div>

            <div class="dims-cell">
              <template v-if="normalizeMount(item.mount) === 'BrickStack'">
                <input v-if="canWrite" class="step-input" type="number" min="1" max="32" step="1"
                       :value="item.widthStuds" title="Facade length in studs"
                       @change="setWidthStuds(item, $event.target.value)" />
                <span v-else>{{ item.widthStuds }}</span>
                <span class="muted">stud ×</span>
                <input v-if="canWrite" class="step-input" type="number" min="0.01" step="0.01"
                       :value="item.brickHeight" title="Height in bricks"
                       @change="setBrickHeight(item, $event.target.value)" />
                <span v-else>{{ item.brickHeight }}</span>
                <span class="muted">bricks</span>
              </template>
              <template v-else>
                <input v-if="canWrite" class="step-input" type="number" min="1" max="32" step="1"
                       :value="item.widthStuds" title="Width in studs"
                       @change="setWidthStuds(item, $event.target.value)" />
                <span v-else>{{ item.widthStuds }}</span>
                <span class="muted">×</span>
                <input v-if="canWrite" class="step-input" type="number" min="1" max="32" step="1"
                       :value="item.depthStuds" title="Depth in studs"
                       @change="setDepthStuds(item, $event.target.value)" />
                <span v-else>{{ item.depthStuds }}</span>
                <span class="muted">studs</span>
              </template>
            </div>

            <div v-if="canWrite" class="copies">
              <button class="step" :disabled="item.copies <= 1" @click="stepCopies(item, -1)">−</button>
              <input class="step-input" type="number" min="1" max="999" :value="item.copies"
                     @input="setCopies(item, $event.target.value)" />
              <button class="step" :disabled="item.copies >= 999" @click="stepCopies(item, 1)">+</button>
            </div>
            <div v-else class="copies static">
              {{ item.copies }} {{ item.copies === 1 ? 'copy' : 'copies' }}
            </div>

            <div class="fit-cell">
              <select v-if="canWrite" :value="item.fit" @change="setFit(item, $event.target.value)">
                <option v-for="fit in FIT_OPTIONS" :key="fit.value" :value="fit.value">
                  {{ fit.label }}
                </option>
              </select>
              <span v-else class="muted">{{ fitLabel(item.fit) }}</span>
            </div>

            <div class="fit-cell">
              <select v-if="canWrite" :value="normalizeOrientation(item.orientation)"
                      @change="setOrientation(item, $event.target.value)">
                <option v-for="orientation in ORIENTATION_OPTIONS" :key="orientation.value"
                        :value="orientation.value">
                  {{ orientation.label }}
                </option>
              </select>
              <span v-else class="muted">{{ orientationLabel(item.orientation) }}</span>
            </div>

            <div class="notes-cell">
              <input v-if="canWrite" :value="item.notes ?? ''" maxlength="500" placeholder="Notes"
                     @change="setNotes(item, $event.target.value)" />
              <span v-else class="muted">{{ item.notes || '' }}</span>
            </div>

            <button v-if="canWrite" class="danger" @click="removeItem(item)">Remove</button>
          </div>
        </div>

        <p v-if="canWrite" class="hint">
          Changes are saved automatically. The artwork's mount, label size and copy count are what
          the sheet prints: one label per copy. A tile is sized in studs, a brick stack by its
          facade length in studs and its height in bricks; a stack ignores the depth, since only the
          facade is printed. The artwork orientation only turns the artwork inside the label — the
          label keeps its millimetres.
        </p>
      </section>

      <!-- ── Print ── -->
      <StickerPrintPanel :items="project.items" :images="images" />
    </template>
  </div>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { useRoute } from 'vue-router'
import StickerImagePicker from '../../components/stickers/StickerImagePicker.vue'
import StickerPrintPanel from '../../components/stickers/StickerPrintPanel.vue'
import { useAuth } from '../../composables/useAuth.js'
import { STUD_MM } from '../../utils/stickerSheets.js'
import {
  DEFAULT_FIT,
  DEFAULT_MOUNT,
  DEFAULT_ORIENTATION,
  FIT_OPTIONS,
  MOUNT_OPTIONS,
  ORIENTATION_OPTIONS,
  fitLabel,
  mountLabel,
  normalizeFit,
  normalizeMount,
  normalizeOrientation,
  orientationLabel,
  stickerArtworkUrl,
} from '../../utils/stickerImages.js'
import { getStickerImages } from '../../api/stickerImages.js'
import { getStickerProject, toItemPayload, updateStickerProject } from '../../api/stickerProjects.js'

const route = useRoute()
const auth = useAuth()
const canWrite = computed(() => auth.canWriteAnywhere)

const projectId = route.params.id
const project = ref(null)
const images = ref([])
const loading = ref(true)
const loadError = ref('')
const itemError = ref('')

/** `idle` | `saving` | `saved` | `error` — what the header shows. */
const saveState = ref('idle')
const saveError = ref('')

/** Common LEGO tile footprints (1 stud = 8 mm). */
const presets = [
  { key: '1x1', label: '1×1', widthStuds: 1, depthStuds: 1 },
  { key: '1x2', label: '1×2', widthStuds: 1, depthStuds: 2 },
  { key: '1x3', label: '1×3', widthStuds: 1, depthStuds: 3 },
  { key: '1x4', label: '1×4', widthStuds: 1, depthStuds: 4 },
  { key: '1x6', label: '1×6', widthStuds: 1, depthStuds: 6 },
  { key: '1x8', label: '1×8', widthStuds: 1, depthStuds: 8 },
  { key: '2x2', label: '2×2', widthStuds: 2, depthStuds: 2 },
  { key: '2x3', label: '2×3', widthStuds: 2, depthStuds: 3 },
  { key: '2x4', label: '2×4', widthStuds: 2, depthStuds: 4 },
  { key: '2x6', label: '2×6', widthStuds: 2, depthStuds: 6 },
  { key: '2x8', label: '2×8', widthStuds: 2, depthStuds: 8 },
]

const draft = reactive({
  imageId: null,
  /** `Tile` | `BrickStack`: the mount decides the second label dimension. */
  mount: DEFAULT_MOUNT,
  preset: '2x4',
  widthStuds: 2,
  depthStuds: 4,
  /** Height of a brick stack in bricks; the presets are tile footprints, so the form has both. */
  brickHeight: 6,
  copies: 1,
  fit: DEFAULT_FIT,
  orientation: DEFAULT_ORIENTATION,
  notes: '',
})

/** A brick stack prints only the facade: the depth field and the tile presets are hidden. */
const draftIsBrickStack = computed(() => draft.mount === 'BrickStack')

const totalLabels = computed(() =>
  (project.value?.items ?? []).reduce((n, item) => n + (Number(item.copies) || 0), 0),
)

const saveLabel = computed(() => {
  if (saveState.value === 'saving') return 'Saving…'
  if (saveState.value === 'saved') return 'Saved'
  if (saveState.value === 'error') return 'Not saved'
  return ''
})

/** Artwork version of each image, so a replaced file is not served from the browser cache. */
const imagesById = computed(() => new Map((images.value ?? []).map((i) => [String(i.id), i])))
const artworkUrl = (item) =>
  stickerArtworkUrl({
    imageId: item.imageId,
    updatedAt: imagesById.value.get(String(item.imageId))?.updatedAt,
  })

/** The physical label size as the server reported it (the SPA never does the maths). */
const mmLabel = (item) =>
  item.widthMm != null && item.depthMm != null ? `${item.widthMm}×${item.depthMm} mm` : '…'

/** The label dimensions as the user thinks of them: studs × studs, or studs × bricks. */
const dimsLabel = (item) => {
  if (normalizeMount(item.mount) === 'BrickStack') {
    const bricks = Number(item.brickHeight) || 0
    return `${item.widthStuds} stud × ${bricks} brick${bricks === 1 ? '' : 's'}`
  }
  return `${item.widthStuds}×${item.depthStuds} stud`
}

const clampStuds = (value) => {
  const n = Math.floor(Number(value))
  return n >= 1 && n <= 32 ? n : 1
}
const clampCopies = (value) => {
  const n = Math.floor(Number(value))
  return n >= 1 && n <= 999 ? n : 1
}
/** A brick height is a positive decimal in bricks with 2 decimals (e.g. 6 or 2.33). */
const clampBrickHeight = (value) => {
  const n = Math.round(Number(value) * 100) / 100
  return n > 0 ? n : 1
}

/** A GUID for a row the server has not seen yet (it keeps the id we send). */
function newItemId() {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') return crypto.randomUUID()
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0
    return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16)
  })
}

// ── Persistence: one PUT of the whole item list, debounced ────────────────────
let saveTimer = null
// An edit is still waiting for its debounce: navigation must still write it.
let pendingEdit = false
// Saves are serialised: a queued one reads the state at the moment it runs, so the last write
// always carries the latest edits and two PUTs never race on the version.
let chain = Promise.resolve(true)

function scheduleSave(delay = 600) {
  if (!canWrite.value) return
  pendingEdit = true
  if (saveState.value !== 'saving') saveState.value = 'idle'
  clearTimeout(saveTimer)
  saveTimer = setTimeout(() => { persist() }, delay)
}

function persist() {
  clearTimeout(saveTimer)
  chain = chain.then(() => runSave(), () => runSave())
  return chain
}

async function runSave() {
  const current = project.value
  if (!current || !canWrite.value) return false
  if (!current.name.trim()) {
    saveState.value = 'error'
    saveError.value = 'The project needs a name.'
    return false
  }

  pendingEdit = false
  saveState.value = 'saving'
  saveError.value = ''
  try {
    const saved = await updateStickerProject(current.id, {
      name: current.name.trim(),
      items: toItemPayload(current.items),
      version: current.version,
    })
    // The rows stay local (a save in flight must not undo typing), but the fields only the
    // server knows — the resolved image name, the mm size — are refreshed from it.
    const stored = new Map((saved.items ?? []).map((i) => [String(i.id), i]))
    for (const item of current.items) {
      const row = stored.get(String(item.id))
      if (!row) continue
      item.imageName = row.imageName
      item.imageCached = row.imageCached
      item.widthMm = row.widthMm
      item.depthMm = row.depthMm
      // These are the fields the server normalises (a stack is always 1 stud deep, a brick height
      // is rounded to 2 decimals): show what was actually stored.
      item.mount = normalizeMount(row.mount)
      item.depthStuds = row.depthStuds
      item.brickHeight = row.brickHeight ?? null
    }
    current.version = saved.version
    current.updatedAt = saved.updatedAt
    saveState.value = 'saved'
    return true
  } catch (e) {
    saveState.value = 'error'
    if (e.status === 409) {
      saveError.value = 'This project was changed elsewhere. Reloaded the saved version.'
      await load()
    } else {
      saveError.value = e.message || 'Could not save the project.'
    }
    return false
  }
}

// ── Item operations ──────────────────────────────────────────────────────────
async function addItem() {
  if (!canWrite.value || !project.value) return
  itemError.value = ''
  if (!draft.imageId) {
    itemError.value = 'Pick an artwork for the new item.'
    return
  }

  const isStack = draft.mount === 'BrickStack'
  const preset = isStack ? null : presets.find((p) => p.key === draft.preset)
  const image = images.value.find((i) => String(i.id) === String(draft.imageId))
  const item = {
    id: newItemId(),
    imageId: draft.imageId,
    imageName: image?.name ?? null,
    imageCached: image?.imageCached ?? false,
    mount: normalizeMount(draft.mount),
    widthStuds: clampStuds(preset ? preset.widthStuds : draft.widthStuds),
    // A brick stack is one stud deep — the server forces it anyway; a tile takes it from the preset.
    depthStuds: isStack ? 1 : clampStuds(preset ? preset.depthStuds : draft.depthStuds),
    brickHeight: isStack ? clampBrickHeight(draft.brickHeight) : null,
    copies: clampCopies(draft.copies),
    fit: normalizeFit(draft.fit),
    orientation: normalizeOrientation(draft.orientation),
    notes: draft.notes.trim() || null,
  }

  project.value.items.push(item)
  draft.notes = ''
  const saved = await persist()
  if (!saved) {
    // The server refused the row (duplicate footprint, vanished image, …): take it back out so
    // the editor matches the stored project, and show the server's own message.
    project.value.items = project.value.items.filter((i) => i.id !== item.id)
    itemError.value = saveError.value
  }
}

async function removeItem(item) {
  const index = project.value.items.findIndex((i) => i.id === item.id)
  if (index < 0) return
  project.value.items.splice(index, 1)
  await persist()
}

function setCopies(item, value) {
  item.copies = clampCopies(value)
  scheduleSave()
}

function stepCopies(item, delta) {
  setCopies(item, Number(item.copies) + delta)
}

function setFit(item, value) {
  item.fit = normalizeFit(value)
  scheduleSave()
}

/**
 * Switches the mount of an existing row. A brick stack is one stud deep and needs a height, a tile
 * needs its depth back, so the fields that do not apply are reset to a printable value instead of
 * being left stale.
 */
function setMount(item, value) {
  const mount = normalizeMount(value)
  item.mount = mount
  if (mount === 'BrickStack') {
    item.depthStuds = 1
    if (!(Number(item.brickHeight) > 0)) item.brickHeight = clampBrickHeight(item.brickHeight)
  } else {
    item.brickHeight = null
    item.depthStuds = clampStuds(item.depthStuds)
  }
  scheduleSave()
}

function setWidthStuds(item, value) {
  item.widthStuds = clampStuds(value)
  scheduleSave()
}

function setDepthStuds(item, value) {
  item.depthStuds = clampStuds(value)
  scheduleSave()
}

function setBrickHeight(item, value) {
  item.brickHeight = clampBrickHeight(value)
  scheduleSave()
}

function setOrientation(item, value) {
  item.orientation = normalizeOrientation(value)
  scheduleSave()
}

function setNotes(item, value) {
  item.notes = value.trim() || null
  scheduleSave()
}

// ── Loading ──────────────────────────────────────────────────────────────────
async function load() {
  loading.value = true
  loadError.value = ''
  try {
    const [loaded, library] = await Promise.all([
      getStickerProject(projectId),
      // The library only feeds the picker and the cache-busting URLs: a failure must not block
      // the project itself.
      getStickerImages().catch(() => []),
    ])
    project.value = { ...loaded, items: loaded.items ?? [] }
    images.value = library ?? []
  } catch (e) {
    loadError.value = e.message || 'Could not load the project.'
    project.value = null
  } finally {
    loading.value = false
  }
}

onMounted(load)
onBeforeUnmount(() => {
  clearTimeout(saveTimer)
  // Leaving the page must not drop the last keystrokes: the debounced save is flushed here.
  if (pendingEdit) persist()
})
</script>

<style scoped>
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  gap: 1rem;
  flex-wrap: wrap;
}

.title-block {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex: 1;
  min-width: 240px;
}

.name-input {
  font-size: 1.4rem;
  font-weight: 600;
  max-width: 420px;
}

h1 {
  margin-bottom: 0;
}

.save-state {
  font-size: 0.75rem;
  color: #64748b;
  white-space: nowrap;
}

.save-state.saving {
  color: #2563eb;
}

.save-state.saved {
  color: #15803d;
}

.save-state.error {
  color: #dc2626;
}

.summary {
  display: flex;
  gap: 1rem;
  font-size: 0.85rem;
  color: #475569;
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
  margin: 0.75rem 0;
}

.item-form {
  margin-top: 0.75rem;
}

.form-field.small {
  flex: 0 0 110px;
  min-width: 110px;
}

.items {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.item {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  flex-wrap: wrap;
  padding: 0.5rem;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
}

.thumb {
  width: 56px;
  height: 56px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #f1f5f9;
  border-radius: 4px;
  flex: 0 0 56px;
  background-image:
    linear-gradient(45deg, #e8eef5 25%, transparent 25%),
    linear-gradient(-45deg, #e8eef5 25%, transparent 25%),
    linear-gradient(45deg, transparent 75%, #e8eef5 75%),
    linear-gradient(-45deg, transparent 75%, #e8eef5 75%);
  background-size: 16px 16px;
  background-position: 0 0, 0 8px, 8px -8px, -8px 0;
}

.thumb img {
  max-width: 90%;
  max-height: 90%;
  object-fit: contain;
}

.thumb.missing {
  background-image: none;
  background: #f8fafc;
}

.missing-label {
  font-size: 0.65rem;
  color: #94a3b8;
}

.item-name {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  min-width: 150px;
  flex: 1;
}

.copies {
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

/* The mount and its own two dimensions: the second field changes meaning with the mount. */
.mount-cell select {
  max-width: 130px;
}

.dims-cell {
  display: flex;
  align-items: center;
  gap: 0.25rem;
  white-space: nowrap;
}

.dims-cell .step-input {
  width: 60px;
}

.copies .step {
  width: 28px;
  padding: 0.1rem 0;
  line-height: 1;
}

.copies .step-input {
  width: 64px;
  text-align: center;
  padding: 0.2rem;
}

.copies.static {
  font-size: 0.85rem;
  color: #475569;
}

.fit-cell select {
  width: 150px;
}

.notes-cell {
  flex: 1;
  min-width: 130px;
}

.hint {
  font-size: 0.75rem;
  color: #64748b;
  margin-top: 0.75rem;
}
</style>
