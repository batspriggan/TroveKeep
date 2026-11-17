<template>
  <div class="sticker-page">
    <div class="page-header">
      <div>
        <h1>Sticker library</h1>
        <p class="subtitle">
          Labels sized for LEGO tiles (1 stud = 8&nbsp;mm): pick the artwork and how many copies,
          and the engine lays them out on A4 sheets at 1:1 with a black cut border.
        </p>
      </div>
      <RouterLink to="/settings" class="muted-link">Settings</RouterLink>
    </div>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-if="!canWrite" class="notice">
      The library is shared by the whole instance. Your access is read-only here: you can compose
      and print a sheet, but changing stickers needs <strong>Editor access in at least one
      collection</strong>.
    </p>

    <!-- ── Compose & print ── -->
    <section class="card print-panel">
      <h2>Print sheet</h2>
      <div class="totals">
        <span><strong>{{ totalCopies }}</strong> labels</span>
        <span><strong>{{ plan ? plan.pages.length : 0 }}</strong> A4 pages</span>
        <span v-if="plan" class="muted">{{ perPageLabel }}</span>
      </div>

      <p v-if="planError" class="error">{{ planError }}</p>
      <p v-else-if="totalCopies === 0" class="muted">
        Pick a number of copies on the stickers below to compose a sheet.
      </p>

      <div class="options">
        <label :title="'White margin of the sheet, in mm (5–20)'">
          Sheet margin
          <input type="number" step="0.1" min="5" max="20" :value="options.marginMm"
                 @input="setOption('marginMm', $event.target.value)" />
        </label>
        <label :title="'Thickness of the black cut border, in mm (0–3)'">
          Cut border
          <input type="number" step="0.1" min="0" max="3" :value="options.borderMm"
                 @input="setOption('borderMm', $event.target.value)" />
        </label>
        <label :title="'Inner border: room between the black border and the artwork, in mm (0–3)'">
          Inner border
          <input type="number" step="0.1" min="0" max="3" :value="options.paddingMm"
                 @input="setOption('paddingMm', $event.target.value)" />
        </label>
        <label :title="'Distance between two cut lines, in mm (0–10)'">
          Gap between labels
          <input type="number" step="0.1" min="0" max="10" :value="options.gapMm"
                 @input="setOption('gapMm', $event.target.value)" />
        </label>
        <label :title="'Grows (+) or shrinks (−) the label on every side, in mm; use it when the printer is slightly off scale'">
          Size trim
          <input type="number" step="0.1" min="-0.5" max="1" :value="options.growMm"
                 @input="setOption('growMm', $event.target.value)" />
        </label>
        <label class="check" title="Turn a label 90° when it is the only way it fits the sheet">
          <input type="checkbox" :checked="options.allowRotate"
                 @change="setOption('allowRotate', $event.target.checked)" />
          Allow 90° rotation
        </label>
      </div>

      <div class="print-actions">
        <button class="primary" :disabled="!plan || totalCopies === 0 || generating" @click="generatePdf">
          {{ generating ? 'Composing…' : 'Generate PDF' }}
        </button>
        <button v-if="totalCopies > 0" @click="sheet.clearCopies()">Clear copies</button>
        <button @click="sheet.resetOptions()">Reset layout</button>
        <span class="hint">
          Print at <strong>100%</strong> and disable “fit to page”, otherwise the labels are not
          tile-sized.
        </span>
      </div>

      <p v-if="printMessage" class="ok">{{ printMessage }}</p>
      <p v-if="printError" class="error">{{ printError }}</p>
    </section>

    <!-- ── Add to the library ── -->
    <section v-if="canWrite" class="card">
      <h2>Add stickers</h2>
      <div class="form-row">
        <div class="form-field">
          <label>Footprint</label>
          <select v-model="newPreset">
            <option v-for="preset in presets" :key="preset.key" :value="preset.key">
              {{ preset.label }} — {{ preset.widthStuds * STUD_MM }}×{{ preset.depthStuds * STUD_MM }} mm
            </option>
            <option value="custom">Custom…</option>
          </select>
        </div>
        <template v-if="newPreset === 'custom'">
          <div class="form-field small">
            <label>Width (studs)</label>
            <input type="number" min="1" max="32" v-model.number="newWidth" />
          </div>
          <div class="form-field small">
            <label>Depth (studs)</label>
            <input type="number" min="1" max="32" v-model.number="newDepth" />
          </div>
        </template>
        <div class="form-field">
          <label>Name (optional)</label>
          <input v-model="newName" maxlength="80" placeholder="e.g. Red apple" />
        </div>
        <button class="primary" :disabled="creating" @click="createEmpty">
          {{ creating ? 'Saving…' : 'Add without artwork' }}
        </button>
      </div>

      <div class="form-row">
        <div class="form-field">
          <label>Upload artwork (PNG or JPEG, one file = one sticker, name from the file)</label>
          <input type="file" accept="image/png,image/jpeg" multiple :disabled="uploading > 0"
                 @change="onFilesPicked" />
        </div>
      </div>
      <p v-if="uploading > 0" class="muted">Uploading {{ uploading }} file(s)…</p>
    </section>

    <!-- ── Library ── -->
    <p v-if="loading" class="muted">Loading the library…</p>
    <p v-else-if="stickers.length === 0" class="card empty">
      No stickers yet. Upload an image above to start the library.
    </p>

    <div v-else class="grid">
      <article v-for="sticker in stickers" :key="sticker.id" class="card sticker-card">
        <div class="thumb" :class="{ missing: !sticker.imageCached }">
          <img v-if="sticker.imageCached" :src="imageUrl(sticker)" :alt="sticker.name" loading="lazy" />
          <span v-else class="missing-label">no artwork</span>
        </div>

        <div class="card-body">
          <div class="name-row">
            <input v-if="editingId === sticker.id" v-model="editName" maxlength="80"
                   @keyup.enter="saveName(sticker)" @blur="saveName(sticker)" />
            <strong v-else class="name" :class="{ editable: canWrite }" :title="sticker.name"
                    @click="canWrite && startEdit(sticker)">{{ sticker.name }}</strong>
          </div>
          <div class="size">
            {{ sticker.widthStuds }}×{{ sticker.depthStuds }} stud
            <span class="muted">· {{ sticker.widthMm }}×{{ sticker.depthMm }} mm</span>
          </div>

          <div class="copies">
            <span class="copies-label">Copies</span>
            <button class="step" :disabled="copiesOf(sticker.id) === 0" @click="sheet.addCopies(sticker.id, -1)">−</button>
            <input class="step-input" type="number" min="0" max="999"
                   :value="copiesOf(sticker.id)"
                   @input="sheet.setCopies(sticker.id, $event.target.value)" />
            <button class="step" @click="sheet.addCopies(sticker.id, 1)">+</button>
          </div>

          <div v-if="canWrite" class="row-actions">
            <label class="upload-btn">
              {{ sticker.imageCached ? 'Replace' : 'Upload' }}
              <input type="file" accept="image/png,image/jpeg" @change="uploadTo(sticker, $event)" />
            </label>
            <button v-if="sticker.imageCached" @click="removeImage(sticker)">Remove image</button>
            <button class="danger" @click="askDelete(sticker)">Delete</button>
          </div>
          <p v-if="rowError[sticker.id]" class="error small-error">{{ rowError[sticker.id] }}</p>
        </div>
      </article>
    </div>

    <ConfirmDialog :open="!!pendingDelete" :message="deleteMessage" @confirm="confirmDelete" @cancel="pendingDelete = null" />
  </div>
</template>

<script setup>
import { computed, onMounted, reactive, ref } from 'vue'
import ConfirmDialog from '../../components/ConfirmDialog.vue'
import { useAuth } from '../../composables/useAuth.js'
import { useStickerSheet } from '../../composables/useStickerSheet.js'
import { STUD_MM } from '../../utils/stickerSheets.js'
import { loadStickerImages } from '../../utils/stickerImages.js'
import { downloadStickerPdf } from '../../utils/stickerPdf.js'
import {
  createSticker,
  deleteSticker,
  deleteStickerImage,
  getStickers,
  stickerImageUrl,
  updateSticker,
  uploadStickerImage,
} from '../../api/stickers.js'

const auth = useAuth()
const sheet = useStickerSheet()
/** Layout options from the shared composer state; the template binds them by name. */
const options = sheet.options

const stickers = ref([])
const loading = ref(true)
const error = ref('')
const creating = ref(false)
const uploading = ref(0)
const generating = ref(false)
const printMessage = ref('')
const printError = ref('')
const pendingDelete = ref(null)
const rowError = reactive({})

const editingId = ref(null)
const editName = ref('')

const newName = ref('')
const newPreset = ref('2x4')
const newWidth = ref(2)
const newDepth = ref(4)

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

const canWrite = computed(() => auth.canWriteAnywhere)
const totalCopies = computed(() => sheet.totalCopies(stickers.value))
const layout = computed(() => sheet.planFor(stickers.value))
const plan = computed(() => layout.value.plan)
const planError = computed(() => layout.value.error?.message ?? '')

const perPageLabel = computed(() => {
  if (!plan.value || plan.value.pages.length === 0) return ''
  const first = plan.value.pages[0].rows.reduce((n, row) => n + row.cells.length, 0)
  return `${first} labels on the first sheet`
})

const deleteMessage = computed(() =>
  pendingDelete.value
    ? `Delete "${pendingDelete.value.name}" and its artwork from the library?`
    : '',
)

const imageUrl = (sticker) => stickerImageUrl(sticker)

function setOption(key, value) {
  sheet.setOption(key, value)
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    stickers.value = await getStickers()
  } catch (e) {
    error.value = e.message || 'Could not load the sticker library.'
    stickers.value = []
  } finally {
    loading.value = false
  }
}

function replaceRow(updated) {
  const index = stickers.value.findIndex((s) => s.id === updated.id)
  if (index >= 0) stickers.value[index] = updated
  else stickers.value.push(updated)
}

function parseStuds(value, fallback) {
  const n = Math.floor(Number(value))
  return n >= 1 && n <= 32 ? n : fallback
}

async function createOne({ name, widthStuds, depthStuds, file }) {
  const created = await createSticker({ name, widthStuds, depthStuds })
  replaceRow(created)
  if (file) await uploadImage(created, file)
  return created
}

async function createEmpty() {
  creating.value = true
  error.value = ''
  try {
    const preset = presets.find((p) => p.key === newPreset.value)
    const widthStuds = preset ? preset.widthStuds : parseStuds(newWidth.value, 1)
    const depthStuds = preset ? preset.depthStuds : parseStuds(newDepth.value, 1)
    const name = newName.value.trim() || (preset ? `Tile ${preset.label}` : `Tile ${widthStuds}×${depthStuds}`)
    await createOne({ name, widthStuds, depthStuds })
    newName.value = ''
  } catch (e) {
    error.value = e.message || 'Could not create the sticker.'
  } finally {
    creating.value = false
  }
}

const fileNameToLabel = (file) =>
  file.name
    .replace(/\.[^.]+$/, '')
    .replace(/[_-]+/g, ' ')
    .trim()
    .slice(0, 80) || 'Sticker'

async function onFilesPicked(event) {
  const files = [...(event.target.files ?? [])]
  event.target.value = ''
  if (files.length === 0) return

  const preset = presets.find((p) => p.key === newPreset.value)
  const widthStuds = preset ? preset.widthStuds : parseStuds(newWidth.value, 1)
  const depthStuds = preset ? preset.depthStuds : parseStuds(newDepth.value, 1)

  uploading.value = files.length
  error.value = ''
  for (const file of files) {
    try {
      await createOne({ name: fileNameToLabel(file), widthStuds, depthStuds, file })
    } catch (e) {
      error.value = e.message || `Could not upload ${file.name}.`
    } finally {
      uploading.value -= 1
    }
  }
}

async function uploadImage(sticker, file) {
  try {
    const updated = await uploadStickerImage(sticker.id, file)
    replaceRow(updated)
    delete rowError[sticker.id]
  } catch (e) {
    // The sticker row exists but has no artwork: the card shows the "no artwork" badge and
    // the upload can be retried from there.
    rowError[sticker.id] = e.message || 'Upload failed.'
  }
}

async function uploadTo(sticker, event) {
  const file = event.target.files?.[0]
  event.target.value = ''
  if (!file) return
  await uploadImage(sticker, file)
}

async function removeImage(sticker) {
  try {
    replaceRow(await deleteStickerImage(sticker.id))
  } catch (e) {
    rowError[sticker.id] = e.message || 'Could not remove the image.'
  }
}

function startEdit(sticker) {
  editingId.value = sticker.id
  editName.value = sticker.name
}

async function saveName(sticker) {
  if (editingId.value !== sticker.id || !canWrite.value) return
  const name = editName.value.trim()
  editingId.value = null
  if (!name || name === sticker.name) return
  try {
    replaceRow(await updateSticker(sticker.id, {
      name,
      widthStuds: sticker.widthStuds,
      depthStuds: sticker.depthStuds,
      version: sticker.version,
    }))
  } catch (e) {
    rowError[sticker.id] = e.message || 'Could not rename the sticker.'
    await load()
  }
}

function askDelete(sticker) {
  pendingDelete.value = sticker
}

async function confirmDelete() {
  const sticker = pendingDelete.value
  pendingDelete.value = null
  if (!sticker) return
  try {
    await deleteSticker(sticker.id)
    stickers.value = stickers.value.filter((s) => s.id !== sticker.id)
    sheet.setCopies(sticker.id, 0)
  } catch (e) {
    error.value = e.message || 'Could not delete the sticker.'
  }
}

async function generatePdf() {
  printMessage.value = ''
  printError.value = ''
  const { plan: current, error: planFailure } = sheet.planFor(stickers.value)
  if (planFailure || !current) {
    printError.value = planFailure?.message || 'Nothing to print.'
    return
  }

  generating.value = true
  try {
    const chosen = sheet.selection(stickers.value)
    const byId = new Map(stickers.value.map((s) => [String(s.id), s]))
    const printable = chosen.map((c) => byId.get(String(c.id))).filter(Boolean)
    const images = await loadStickerImages(printable, sheet.options)
    const result = downloadStickerPdf(chosen, sheet.options, images)
    printMessage.value = `Generated ${result.fileName}: ${result.count} labels on ${result.pages} page(s).`
  } catch (e) {
    printError.value = e.message || 'Could not generate the PDF.'
  } finally {
    generating.value = false
  }
}

const copiesOf = (id) => sheet.copiesOf(id)

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
  max-width: 60ch;
  margin-bottom: 1rem;
}

.muted-link,
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

.ok {
  color: #15803d;
  font-size: 0.85rem;
  margin-top: 0.5rem;
}

.totals {
  display: flex;
  gap: 1.25rem;
  align-items: baseline;
  font-size: 0.9rem;
  margin-bottom: 0.75rem;
}

.options {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  margin: 0.75rem 0 1rem;
}

.options label {
  font-size: 0.75rem;
  color: #475569;
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  width: 120px;
}

.options label.check {
  flex-direction: row;
  align-items: center;
  gap: 0.4rem;
  width: auto;
  padding-top: 1rem;
}

.options label.check input {
  width: auto;
}

.print-actions {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  flex-wrap: wrap;
}

.hint {
  font-size: 0.75rem;
  color: #64748b;
  max-width: 32ch;
}

.form-field.small {
  flex: 0 0 110px;
  min-width: 110px;
}

.grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 1rem;
}

.sticker-card {
  display: flex;
  flex-direction: column;
  padding: 0;
  overflow: hidden;
}

.thumb {
  height: 120px;
  background: #f1f5f9;
  display: flex;
  align-items: center;
  justify-content: center;
  border-bottom: 1px solid #e2e8f0;
  /* A checkerboard hints at the transparency of the artwork. */
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
  font-size: 0.75rem;
  color: #94a3b8;
  border: 1px dashed #cbd5e1;
  border-radius: 4px;
  padding: 0.2rem 0.5rem;
}

.card-body {
  padding: 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}

.name {
  font-size: 0.95rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  display: block;
}

.name.editable {
  cursor: text;
}

.size {
  font-size: 0.75rem;
  color: #475569;
}

.copies {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  margin-top: 0.25rem;
}

.copies-label {
  font-size: 0.75rem;
  color: #475569;
  margin-right: auto;
}

.copies .step {
  width: 28px;
  padding: 0.1rem 0;
  line-height: 1;
}

.copies .step-input {
  width: 56px;
  text-align: center;
  padding: 0.2rem;
}

.row-actions {
  display: flex;
  gap: 0.35rem;
  flex-wrap: wrap;
  margin-top: 0.35rem;
}

.upload-btn {
  font-size: 0.75rem;
  border: 1px solid #ccc;
  border-radius: 4px;
  padding: 0.35rem 0.6rem;
  cursor: pointer;
  background: #fff;
}

.upload-btn input {
  display: none;
}

.row-actions button {
  font-size: 0.75rem;
}

.small-error {
  font-size: 0.75rem;
  margin: 0;
}

.empty {
  color: #64748b;
  font-size: 0.9rem;
}
</style>
