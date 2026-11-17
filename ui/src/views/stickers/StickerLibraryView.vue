<template>
  <div class="sticker-page">
    <div class="page-header">
      <div>
        <h1>Sticker library</h1>
        <p class="subtitle">
          The artwork shelf: one image per label, no size and no copies here. A sticker project
          picks an image, gives it a tile footprint (1 stud = 8&nbsp;mm) and prints it on an A4
          sheet at 1:1.
        </p>
      </div>
      <RouterLink to="/sticker-projects" class="muted-link">Sticker projects →</RouterLink>
    </div>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-if="!canWrite" class="notice">
      The library is shared by the whole instance. Your access is read-only here: you can browse the
      artwork and print from a project, but changing it needs
      <strong>Editor access in at least one collection</strong>.
    </p>

    <!-- ── Upload ── -->
    <section v-if="canWrite" class="card">
      <h2>Add artwork</h2>
      <div class="form-row">
        <div class="form-field">
          <label>Upload images (PNG or JPEG, up to 10&nbsp;MB each — one file = one entry, named
            after the file)</label>
          <input type="file" accept="image/png,image/jpeg" multiple :disabled="uploading > 0"
                 @change="onFilesPicked" />
        </div>
      </div>
      <p v-if="uploading > 0" class="muted">Uploading {{ uploading }} file(s)…</p>
    </section>

    <!-- ── Library ── -->
    <p v-if="loading" class="muted">Loading the library…</p>

    <section v-else-if="images.length === 0" class="card empty">
      <p>No artwork yet.</p>
      <p v-if="canWrite">Upload a PNG or a JPEG above to start the library.</p>
      <p v-else>The artwork library is empty.</p>
    </section>

    <div v-else class="grid">
      <article v-for="image in images" :key="image.id" class="card sticker-card">
        <div class="thumb" :class="{ missing: !image.imageCached }">
          <img v-if="image.imageCached" :src="imageUrl(image)" :alt="image.name" loading="lazy" />
          <span v-else class="missing-label">no artwork</span>
        </div>

        <div class="card-body">
          <div class="name-row">
            <input v-if="editingId === image.id" v-model="editName" maxlength="80"
                   @keyup.enter="saveName(image)" @blur="saveName(image)" />
            <strong v-else class="name" :class="{ editable: canWrite }" :title="image.name"
                    @click="canWrite && startEdit(image)">{{ image.name }}</strong>
          </div>

          <div v-if="canWrite" class="row-actions">
            <label class="upload-btn">
              {{ image.imageCached ? 'Replace' : 'Upload' }}
              <input type="file" accept="image/png,image/jpeg" @change="uploadTo(image, $event)" />
            </label>
            <button v-if="image.imageCached" @click="removeArtwork(image)">Remove image</button>
            <button class="danger" @click="askDelete(image)">Delete</button>
          </div>
          <p v-if="rowError[image.id]" class="error small-error">{{ rowError[image.id] }}</p>
        </div>
      </article>
    </div>

    <ConfirmDialog :open="!!pendingDelete" :message="deleteMessage"
                   @confirm="confirmDelete" @cancel="pendingDelete = null" />
  </div>
</template>

<script setup>
import { computed, onMounted, reactive, ref } from 'vue'
import ConfirmDialog from '../../components/ConfirmDialog.vue'
import { useAuth } from '../../composables/useAuth.js'
import { stickerArtworkUrl } from '../../utils/stickerImages.js'
import {
  createStickerImage,
  deleteStickerArtwork,
  deleteStickerImage,
  getStickerImages,
  updateStickerImage,
  uploadStickerArtwork,
} from '../../api/stickerImages.js'

const auth = useAuth()
const canWrite = computed(() => auth.canWriteAnywhere)

const images = ref([])
const loading = ref(true)
const error = ref('')
const uploading = ref(0)
const pendingDelete = ref(null)
const rowError = reactive({})

const editingId = ref(null)
const editName = ref('')

const deleteMessage = computed(() =>
  pendingDelete.value
    ? `Delete "${pendingDelete.value.name}" and its artwork from the library?`
    : '',
)

const imageUrl = (image) => stickerArtworkUrl(image)

async function load() {
  loading.value = true
  error.value = ''
  try {
    images.value = (await getStickerImages()) ?? []
  } catch (e) {
    error.value = e.message || 'Could not load the sticker library.'
    images.value = []
  } finally {
    loading.value = false
  }
}

function replaceRow(updated) {
  const index = images.value.findIndex((i) => i.id === updated.id)
  if (index >= 0) images.value[index] = updated
  else images.value.push(updated)
}

/** The file name without its extension, separators turned into spaces. */
const fileNameToLabel = (file) =>
  file.name
    .replace(/\.[^.]+$/, '')
    .replace(/[_-]+/g, ' ')
    .trim()
    .slice(0, 80) || 'Sticker'

async function createOne(file) {
  const created = await createStickerImage({ name: fileNameToLabel(file) })
  replaceRow(created)
  try {
    replaceRow(await uploadStickerArtwork(created.id, file))
  } catch (e) {
    // The row exists without artwork: the card shows the "no artwork" badge and the upload can
    // be retried from there.
    rowError[created.id] = e.message || `Upload of ${file.name} failed.`
  }
  return created
}

async function onFilesPicked(event) {
  const files = [...(event.target.files ?? [])]
  event.target.value = ''
  if (files.length === 0) return

  uploading.value = files.length
  error.value = ''
  for (const file of files) {
    try {
      await createOne(file)
    } catch (e) {
      error.value = e.message || `Could not create the sticker for ${file.name}.`
    } finally {
      uploading.value -= 1
    }
  }
}

async function uploadTo(image, event) {
  const file = event.target.files?.[0]
  event.target.value = ''
  if (!file) return
  try {
    replaceRow(await uploadStickerArtwork(image.id, file))
    delete rowError[image.id]
  } catch (e) {
    rowError[image.id] = e.message || 'Upload failed.'
  }
}

async function removeArtwork(image) {
  try {
    replaceRow(await deleteStickerArtwork(image.id))
    delete rowError[image.id]
  } catch (e) {
    rowError[image.id] = e.message || 'Could not remove the image.'
  }
}

function startEdit(image) {
  editingId.value = image.id
  editName.value = image.name
}

async function saveName(image) {
  if (editingId.value !== image.id || !canWrite.value) return
  const name = editName.value.trim()
  editingId.value = null
  if (!name || name === image.name) return
  try {
    replaceRow(await updateStickerImage(image.id, { name, version: image.version }))
    delete rowError[image.id]
  } catch (e) {
    rowError[image.id] = e.message || 'Could not rename the image.'
    await load()
  }
}

function askDelete(image) {
  pendingDelete.value = image
}

async function confirmDelete() {
  const image = pendingDelete.value
  pendingDelete.value = null
  if (!image) return
  try {
    await deleteStickerImage(image.id)
    images.value = images.value.filter((i) => i.id !== image.id)
  } catch (e) {
    // A 400 here means a project item still prints this artwork: show the server's message.
    rowError[image.id] = e.message || 'Could not delete the image.'
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

.empty p + p {
  margin-top: 0.35rem;
}
</style>
