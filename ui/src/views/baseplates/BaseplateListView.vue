<template>
  <div class="bp-page">
    <div class="bp-header">
      <h1>Baseplates</h1>
      <RouterLink v-if="settings.tablePlannerEnabled" to="/table-planner" class="planner-link">
        Open Table Planner →
      </RouterLink>
    </div>

    <p v-if="error" class="error">{{ error }}</p>

    <!-- ── Unresolved MOC plates (quarantine wizard) ── -->
    <section v-if="!loading && quarantined.length > 0" class="quarantine-section">
      <h2 class="q-title">
        ⚠ Unresolved MOC plates ({{ quarantined.length }})
        <span class="q-subtitle">not placeable in the planner</span>
      </h2>
      <p class="q-intro">
        These rows couldn't be converted automatically and are excluded from the totals below.
        Reconcile each one into real plates, mark it as a physical plate, or delete it.
      </p>

      <div v-for="bp in quarantined" :key="bp.id" class="quarantine-card">
        <div class="q-head">
          <strong class="q-name">{{ bp.name || bp.partNum || 'Unnamed plate' }}</strong>
          <span class="id-col">{{ bp.partNum || '—' }}</span>
          <span class="q-size">{{ bp.widthStuds }}×{{ bp.depthStuds }} stud</span>
          <span class="q-reason" :title="bp.quarantineReason ?? ''">
            {{ bp.quarantineReason || 'unknown reason' }}
          </span>
        </div>

        <div class="q-options">
          <!-- 1. Reconcile -->
          <div class="q-option">
            <div class="q-option-label">1 · Convert to real plates</div>
            <div class="q-option-body">
              <div class="search-wrap">
                <input
                  class="search-input"
                  placeholder="Target plate type…"
                  :value="qDraft(bp.id).query"
                  @input="onReconcileQuery(bp.id, $event.target.value)"
                />
                <ul v-if="reconcileResults(bp.id).length > 0" class="dropdown">
                  <li
                    v-for="t in reconcileResults(bp.id)"
                    :key="t.id"
                    class="dropdown-item"
                    @click="selectReconcileTarget(bp.id, t, bp)"
                  >
                    {{ t.partNum ? t.partNum + ' — ' : '' }}{{ t.name }}
                    <span class="muted">· {{ t.widthStuds }}×{{ t.depthStuds }} · {{ t.availableQuantity ?? 0 }} free</span>
                  </li>
                </ul>
              </div>
              <label class="qty-label">cols
                <input
                  class="step-input"
                  type="number"
                  min="1"
                  :value="qDraft(bp.id).cols ?? ''"
                  @input="setReconcileCols(bp.id, $event.target.value, bp)"
                />
              </label>
              <label class="qty-label">rows
                <input
                  class="step-input"
                  type="number"
                  min="1"
                  :value="qDraft(bp.id).rows ?? ''"
                  @input="setReconcileRows(bp.id, $event.target.value, bp)"
                />
              </label>
              <button
                class="primary small"
                :disabled="qDraft(bp.id).busy || !qDraft(bp.id).targetId || !(qDraft(bp.id).cols > 0) || !(qDraft(bp.id).rows > 0)"
                @click="doReconcile(bp)"
              >{{ qDraft(bp.id).busy ? 'Working…' : 'Reconcile' }}</button>
            </div>
          </div>

          <!-- 2. Not a MOC -->
          <div class="q-option">
            <div class="q-option-label">2 · Not a MOC</div>
            <div class="q-option-body">
              <span class="muted">Keep it as a physical plate (unlinks the set).</span>
              <button
                class="import-btn"
                :disabled="qDraft(bp.id).busy"
                @click="doUnquarantine(bp)"
              >Confirm as physical plate</button>
            </div>
          </div>

          <!-- 3. Delete -->
          <div class="q-option">
            <div class="q-option-label">3 · Delete</div>
            <div class="q-option-body">
              <button
                class="import-btn danger-btn"
                :disabled="qDraft(bp.id).busy"
                @click="doQuarantineDelete(bp)"
              >Delete row</button>
            </div>
          </div>
        </div>

        <p v-if="qDraft(bp.id).error" class="error q-error">{{ qDraft(bp.id).error }}</p>
      </div>
    </section>

    <!-- ── Summary ── -->
    <section class="summary">
      <div class="summary-cards">
        <div class="sum-card">
          <span class="sum-label">Total</span>
          <span class="sum-value">{{ summary.totalQty }}</span>
        </div>
        <div class="sum-card">
          <span class="sum-label">Reserved</span>
          <span class="sum-value">{{ summary.reserved }}</span>
        </div>
        <div class="sum-card">
          <span class="sum-label">Free</span>
          <span class="sum-value">{{ summary.free }}</span>
        </div>
        <div class="sum-card">
          <span class="sum-label">Surface</span>
          <span class="sum-value">
            {{ summary.studs2.toLocaleString() }} stud²
            <span class="sum-sub">≈ {{ summary.m2.toFixed(2) }} m²</span>
          </span>
        </div>
      </div>

      <div class="type-totals">
        <span class="type-chip standard">Standard {{ summary.byType.Standard }}</span>
        <span class="type-chip road">Road {{ summary.byType.Road }}</span>
        <span class="type-chip custom">Custom {{ summary.byType.Custom }}</span>
      </div>
    </section>

    <!-- ── Filters ── -->
    <section class="filters">
      <div class="filter-row">
        <span class="filter-label">Type</span>
        <button
          v-for="t in ['all', 'Standard', 'Road', 'Custom']"
          :key="t"
          class="chip"
          :class="{ active: filterType === t }"
          @click="filterType = t"
        >{{ t === 'all' ? 'All' : t }}</button>
      </div>

      <div v-if="colorOptions.length" class="filter-row">
        <span class="filter-label">Color</span>
        <button
          class="chip"
          :class="{ active: filterColor === 'all' }"
          @click="filterColor = 'all'"
        >All</button>
        <button
          v-for="c in colorOptions"
          :key="c.id"
          class="chip"
          :class="{ active: filterColor === String(c.id) }"
          @click="filterColor = String(c.id)"
        >
          <span class="swatch" :style="{ background: c.rgb ? '#' + c.rgb : '#ccc' }"></span>{{ c.name }}
        </button>
      </div>

      <div class="filter-row">
        <button
          v-if="needsReviewCount > 0"
          class="chip"
          :class="{ active: onlyReview }"
          @click="onlyReview = !onlyReview"
        >
          ⚠ Only to review ({{ needsReviewCount }})
        </button>
        <button
          v-if="overReservedCount > 0"
          class="chip"
          :class="{ active: onlyOverReserved }"
          @click="onlyOverReserved = !onlyOverReserved"
        >
          ⚠ Over-reserved ({{ overReservedCount }})
        </button>
        <input v-model="search" class="search-input" placeholder="Search part # or name…" />
        <button class="primary small" @click="openAdd">+ Add Baseplate</button>
        <button
          v-if="filteredReviewCount > 0"
          class="import-btn"
          :disabled="confirmingAll"
          @click="confirmAllFiltered"
        >{{ confirmingAll ? 'Confirming…' : 'Confirm all filtered' }}</button>
      </div>
    </section>

    <!-- ── Add / Edit form ── -->
    <section v-if="formOpen" class="bp-form">
      <h2>{{ editingId ? 'Edit baseplate' : 'Add baseplate' }}</h2>

      <div class="form-grid">
        <label class="field">
          <span>Type</span>
          <select v-model="form.type" @change="onTypeChange">
            <option>Standard</option>
            <option>Road</option>
            <option>Custom</option>
          </select>
        </label>

        <!-- Part search (Standard / Road) -->
        <div v-if="form.type !== 'Custom'" class="field search-field">
          <span>Part</span>
          <div class="search-wrap">
            <input v-model="partQuery" class="search-input wide" placeholder="Search part archive…" />
            <ul v-if="partResults.length > 0" class="dropdown">
              <li
                v-for="r in partResults"
                :key="r.partNum"
                class="dropdown-item"
                @click="selectPart(r)"
              >{{ r.partNum }} — {{ r.name }}</li>
            </ul>
          </div>
        </div>

        <!-- Set typeahead (Custom) -->
        <div v-else class="field search-field">
          <span>Linked set</span>
          <div class="search-wrap">
            <input v-model="setQuery" class="search-input wide" placeholder="Search your sets…" />
            <ul v-if="setResults.length > 0" class="dropdown">
              <li
                v-for="s in setResults"
                :key="s.id"
                class="dropdown-item"
                @click="selectSet(s)"
              >{{ s.setNumber ? s.setNumber + ' — ' : '' }}{{ s.description }}{{ s.isMoc ? ' (MOC)' : '' }}</li>
            </ul>
          </div>
        </div>

        <label class="field">
          <span>Part #</span>
          <input v-model="form.partNum" class="text-input" />
        </label>

        <label class="field grow">
          <span>Name</span>
          <input v-model="form.name" class="text-input" />
        </label>

        <label class="field">
          <span>Canonical size</span>
          <input
            v-model="sizeInput"
            class="text-input"
            list="bp-canonical-sizes"
            placeholder="e.g. 32x32"
            @change="applySizeInput"
          />
          <datalist id="bp-canonical-sizes">
            <option v-for="s in CANONICAL_SIZES" :key="s" :value="s"></option>
          </datalist>
        </label>

        <label class="field">
          <span>W (studs)</span>
          <input v-model.number="form.widthStuds" type="number" min="0" max="512" class="num-input" />
        </label>

        <label class="field">
          <span>D (studs)</span>
          <input v-model.number="form.depthStuds" type="number" min="0" max="512" class="num-input" />
        </label>

        <label class="field">
          <span>Quantity</span>
          <input v-model.number="form.quantity" type="number" min="0" class="num-input" />
        </label>

        <label v-if="form.type === 'Standard'" class="field grow">
          <span>Color</span>
          <ColorSelect v-model="form.colorUid" :colors="colors" />
        </label>

        <label v-if="form.type === 'Road'" class="field">
          <span>Road shape</span>
          <select v-model="form.roadShape">
            <option value="">— select —</option>
            <option v-for="s in ROAD_SHAPES" :key="s" :value="s">{{ s }}</option>
          </select>
        </label>

        <label class="field grow">
          <span>Notes</span>
          <input v-model="form.notes" class="text-input" placeholder="optional" />
        </label>
      </div>

      <p v-if="formError" class="error">{{ formError }}</p>

      <div class="form-actions">
        <button class="primary small" :disabled="saving" @click="submitForm">
          {{ saving ? 'Saving…' : (editingId ? 'Save changes' : 'Add baseplate') }}
        </button>
        <button class="import-btn" @click="closeForm">Cancel</button>
      </div>
    </section>

    <!-- ── Catalog ── -->
    <div v-if="loading" class="muted">Loading…</div>
    <p v-else-if="baseplates.length === 0" class="muted">No baseplates yet.</p>
    <p v-else-if="placeableBaseplates.length === 0" class="muted">
      No placeable baseplates — reconcile the unresolved MOC rows above.
    </p>
    <p v-else-if="filtered.length === 0" class="muted">No baseplates match the current filters.</p>

    <table v-else class="data-table bp-table">
      <thead>
        <tr>
          <th></th>
          <th>Preview</th>
          <th>Type</th>
          <th>Part #</th>
          <th>Name</th>
          <th>Size (studs)</th>
          <th>Color</th>
          <th class="num-col">Qty</th>
          <th class="num-col">Free</th>
          <th>RoadShape</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <template v-for="bp in filtered" :key="bp.id">
          <tr :class="{ 'row-review': bp.needsReview, 'row-empty': (bp.availableQuantity ?? 0) === 0 }">
            <td class="expand-col">
              <button class="expand-btn" :title="expanded[bp.id] ? 'Collapse' : 'Show reservations'" @click="toggleExpand(bp.id)">
                {{ expanded[bp.id] ? '▾' : '▸' }}
              </button>
            </td>
            <td class="preview-col">
              <template v-if="bp.type === 'Standard'">
                <span
                  class="swatch"
                  :style="{ background: bp.legoColorRgb ? '#' + bp.legoColorRgb : '#ccc' }"
                  :title="bp.legoColorName ?? ''"
                ></span>
              </template>
              <template v-else>
                <img
                  v-if="bp.imageCached || (bp.type === 'Custom' && bp.linkedSetId)"
                  :src="getBaseplateImageUrl(bp.id)"
                  class="bp-thumb"
                  :alt="bp.name"
                  @error="e => e.target.style.display = 'none'"
                />
                <label class="bp-upload-label" :title="bp.imageCached ? 'Replace image' : 'Upload image'">
                  <input type="file" accept="image/*" style="display:none" @change="e => onBpFileChange(e, bp.id)" />
                  <span class="bp-upload-link">{{ bp.imageCached ? 'Replace' : 'Upload' }}</span>
                </label>
                <button v-if="bpPendingFile[bp.id]" class="primary small" @click="saveBpImage(bp.id)">Save</button>
                <button
                  v-if="bp.imageCached"
                  class="remove-img-btn"
                  title="Remove the uploaded preview"
                  @click="removeBpImage(bp.id)"
                >Remove</button>
              </template>
            </td>
            <td>
              <span class="bp-type-badge" :class="bp.type.toLowerCase()">{{ bp.type }}</span>
              <span v-if="bp.needsReview" class="review-badge" title="To review">⚠ To review</span>
              <span
                v-if="bp.overReserved"
                class="over-badge"
                :title="`${bp.reservedQuantity} reserved by MOCs, but only ${bp.quantity} owned — update the quantity`"
              >⚠ Over-reserved</span>
            </td>
            <td class="id-col">{{ bp.partNum || '—' }}</td>
            <td>{{ bp.name }}</td>
            <td>{{ bp.widthStuds }}×{{ bp.depthStuds }}</td>
            <td>
              <template v-if="bp.type === 'Standard'">
                <span class="swatch" :style="{ background: bp.legoColorRgb ? '#' + bp.legoColorRgb : '#ccc' }"></span>
                <span>{{ bp.legoColorName ?? '—' }}</span>
              </template>
              <span v-else class="muted">—</span>
            </td>
            <td class="num-col">
              <div class="stepper">
                <button class="step-btn" :disabled="rowLocked(bp) || (bp.quantity ?? 0) <= 0" @click="saveQuantity(bp, (bp.quantity ?? 0) - 1)">−</button>
                <input
                  class="step-input"
                  type="number"
                  min="0"
                  :value="bp.quantity ?? 0"
                  :disabled="rowLocked(bp)"
                  @change="e => saveQuantity(bp, e.target.value)"
                />
                <button class="step-btn" :disabled="rowLocked(bp)" @click="saveQuantity(bp, (bp.quantity ?? 0) + 1)">+</button>
              </div>
            </td>
            <td class="num-col" :class="{ 'zero': (bp.availableQuantity ?? 0) === 0 }">{{ bp.availableQuantity ?? 0 }}</td>
            <td>
              <span v-if="bp.type === 'Road'">{{ bp.roadShape || '—' }}</span>
              <span v-else class="muted">—</span>
            </td>
            <td class="actions-col">
              <button class="import-btn" :disabled="rowLocked(bp)" @click="openEdit(bp)">Edit</button>
              <button v-if="bp.needsReview" class="import-btn confirm-btn" :disabled="rowLocked(bp)" @click="confirmRow(bp)">Confirm</button>
              <button class="import-btn danger-btn" :disabled="rowLocked(bp)" @click="removeRow(bp)">Delete</button>
            </td>
          </tr>

          <!-- Reserved by -->
          <tr v-if="expanded[bp.id]" :key="bp.id + '-res'" class="res-row">
            <td></td>
            <td colspan="10">
              <div class="res-panel">
                <div class="res-header">
                  <span class="res-stat">
                    <strong>Reserved by MOC</strong>
                    <span class="res-stat-value">{{ bp.reservedQuantity ?? 0 }}</span>
                  </span>
                </div>

                <ul v-if="(bp.reservations?.length ?? 0) > 0" class="res-list">
                  <li v-for="r in bp.reservations" :key="r.setId" class="res-item">
                    <span class="res-desc">{{ r.setDescription || r.setId }}</span>
                    <span class="res-qty">×{{ r.quantity }}</span>
                    <button class="import-btn danger-btn small-btn" :disabled="rowLocked(bp)" @click="removeMoc(bp, r.setId)">Remove</button>
                  </li>
                </ul>
                <p v-else class="muted res-empty">No reservations.</p>

                <div class="add-moc">
                  <div class="search-wrap">
                    <input
                      class="search-input"
                      placeholder="+ Add MOC (search set…)"
                      :value="mocDraft[bp.id]?.query ?? ''"
                      :disabled="rowLocked(bp)"
                      @input="onMocQuery(bp.id, $event.target.value)"
                    />
                    <ul v-if="mocResults(bp.id).length > 0" class="dropdown">
                      <li
                        v-for="s in mocResults(bp.id)"
                        :key="s.id"
                        class="dropdown-item"
                        @click="selectMocSet(bp.id, s)"
                      >{{ s.setNumber ? s.setNumber + ' — ' : '' }}{{ s.description }}{{ s.isMoc ? ' (MOC)' : '' }}</li>
                    </ul>
                  </div>
                  <label class="qty-label">qty
                    <input
                      class="step-input"
                      type="number"
                      min="1"
                      :value="mocDraft[bp.id]?.quantity ?? 1"
                      :disabled="rowLocked(bp)"
                      @input="onMocQty(bp.id, $event.target.value)"
                    />
                  </label>
                  <button
                    class="primary small"
                    :disabled="!mocDraft[bp.id]?.setId || rowLocked(bp)"
                    @click="confirmAddMoc(bp)"
                  >Add</button>
                </div>
              </div>
            </td>
          </tr>
        </template>
      </tbody>
    </table>
  </div>
</template>

<script setup>
import { ref, reactive, computed, watch, onMounted } from 'vue'
import ColorSelect from '../../components/ColorSelect.vue'
import { useSettings } from '../../composables/useSettings.js'
import { searchArchivePartsBaseplates, getColorsList } from '../../api/archives.js'
import { getAllSets } from '../../api/sets.js'
import {
  getAllBaseplates, createBaseplate, updateBaseplate, confirmBaseplate,
  deleteBaseplate, getBaseplateImageUrl, uploadBaseplateImage, deleteBaseplateImage,
  addReservation, removeReservation,
  reconcileBaseplate, unquarantineBaseplate,
} from '../../api/baseplates.js'

const settings = useSettings()

const ROAD_SHAPES = ['Straight', 'Curve', 'Junction', 'TJunction', 'Crossroad']
const CANONICAL_SIZES = ['8x8', '8x16', '16x16', '16x32', '32x32', '32x48', '48x48']
const STUDS_TO_M2 = 0.000064 // (8 mm)²

// ── Data ─────────────────────────────────────────────────────────────────────
const baseplates = ref([])
const colors = ref([])
const allSets = ref([])
const loading = ref(true)
const error = ref('')

// ── Filters ──────────────────────────────────────────────────────────────────
const filterType = ref('all')
const filterColor = ref('all')
const onlyReview = ref(false)
const onlyOverReserved = ref(false)
const search = ref('')

// ── Reservations UI state ────────────────────────────────────────────────────
const expanded = ref({})
const mocDraft = ref({})

// ── Quarantine wizard state ──────────────────────────────────────────────────
const quarantineDraft = ref({})

// ── Form state ───────────────────────────────────────────────────────────────
const formOpen = ref(false)
const editingId = ref(null)
const saving = ref(false)
const formError = ref('')
const sizeInput = ref('')
const partQuery = ref('')
const partResults = ref([])
const setQuery = ref('')
const setResults = ref([])
const bpPendingFile = ref({})
const confirmingAll = ref(false)

const form = reactive({
  type: 'Standard',
  partNum: '',
  name: '',
  widthStuds: null,
  depthStuds: null,
  colorUid: '',
  roadShape: '',
  quantity: 1,
  linkedSetId: null,
  notes: '',
  legoColorIdFallback: 0,
})

// ── Computed ─────────────────────────────────────────────────────────────────
// Quarantined rows are never placeable: they are pulled out of the catalog,
// the totals and the review counters, and surfaced in their own section.
const quarantined = computed(() => baseplates.value.filter(b => b.quarantined))
const placeableBaseplates = computed(() => baseplates.value.filter(b => !b.quarantined))

const needsReviewCount = computed(() => placeableBaseplates.value.filter(b => b.needsReview).length)

// Over-reservation is a warning, not an error: `reservedQuantity` may exceed `quantity`
// until the user reconciles the owned count here.
const overReservedCount = computed(() => placeableBaseplates.value.filter(b => b.overReserved).length)

// The warning chips disappear once there is nothing left to review: clear the corresponding
// filter too, otherwise the list would stay filtered with no visible way out.
watch(needsReviewCount, (n) => { if (n === 0) onlyReview.value = false })
watch(overReservedCount, (n) => { if (n === 0) onlyOverReserved.value = false })

const colorOptions = computed(() => {
  const map = new Map()
  for (const bp of placeableBaseplates.value) {
    if ((bp.legoColorId ?? 0) > 0 && !map.has(bp.legoColorId)) {
      map.set(bp.legoColorId, {
        id: bp.legoColorId,
        name: bp.legoColorName ?? `Color ${bp.legoColorId}`,
        rgb: bp.legoColorRgb,
      })
    }
  }
  return [...map.values()].sort((a, b) => a.name.localeCompare(b.name))
})

const filtered = computed(() => {
  const q = search.value.trim().toLowerCase()
  return baseplates.value.filter(bp => {
    if (bp.quarantined) return false
    if (filterType.value !== 'all' && bp.type !== filterType.value) return false
    if (filterColor.value !== 'all' && String(bp.legoColorId) !== filterColor.value) return false
    if (onlyReview.value && !bp.needsReview) return false
    if (onlyOverReserved.value && !bp.overReserved) return false
    if (q) {
      const hay = `${bp.partNum ?? ''} ${bp.name ?? ''}`.toLowerCase()
      if (!hay.includes(q)) return false
    }
    return true
  })
})

const filteredReviewCount = computed(() => filtered.value.filter(b => b.needsReview).length)

const summary = computed(() => {
  let totalQty = 0, reserved = 0, free = 0, studs2 = 0
  const byType = { Standard: 0, Road: 0, Custom: 0 }
  for (const bp of placeableBaseplates.value) {
    const q = bp.quantity ?? 0
    totalQty += q
    reserved += bp.reservedQuantity ?? 0
    free += bp.availableQuantity ?? 0
    studs2 += (bp.widthStuds ?? 0) * (bp.depthStuds ?? 0) * q
    if (byType[bp.type] !== undefined) byType[bp.type] += q
  }
  return { totalQty, reserved, free, studs2, m2: studs2 * STUDS_TO_M2, byType }
})

// ── Data loading ─────────────────────────────────────────────────────────────
async function loadBaseplates() {
  baseplates.value = await getAllBaseplates()
}

onMounted(async () => {
  try {
    const [bps, cols] = await Promise.all([
      getAllBaseplates(),
      getColorsList().catch(() => []),
    ])
    baseplates.value = bps ?? []
    colors.value = cols ?? []
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
  try {
    const setsPage = await getAllSets(1, 1000)
    allSets.value = setsPage.items ?? setsPage ?? []
  } catch {
    allSets.value = []
  }
})

// ── Helpers ──────────────────────────────────────────────────────────────────
function replaceRow(updated) {
  if (!updated) return
  const i = baseplates.value.findIndex(b => b.id === updated.id)
  if (i >= 0) baseplates.value.splice(i, 1, updated)
  else baseplates.value.push(updated)
}

function toUpdateBody(bp, overrides = {}) {
  return {
    type: bp.type,
    partNum: bp.partNum ?? '',
    name: bp.name ?? '',
    widthStuds: bp.widthStuds ?? 0,
    depthStuds: bp.depthStuds ?? 0,
    legoColorId: bp.legoColorId ?? 0,
    linkedSetId: bp.linkedSetId ?? null,
    roadShape: bp.roadShape ?? null,
    quantity: bp.quantity ?? 1,
    notes: bp.notes ?? null,
    ...overrides,
  }
}

async function saveQuantity(bp, value) {
  if (rowLocked(bp)) return
  const q = Math.max(0, Math.floor(Number(value) || 0))
  if (q === (bp.quantity ?? 0)) return
  try {
    replaceRow(await updateBaseplate(bp.id, toUpdateBody(bp, { quantity: q })))
  } catch (e) {
    error.value = e.message
  }
}

async function confirmRow(bp) {
  if (rowLocked(bp)) return
  try {
    replaceRow(await confirmBaseplate(bp.id))
  } catch (e) {
    error.value = e.message
  }
}

async function confirmAllFiltered() {
  confirmingAll.value = true
  error.value = ''
  try {
    for (const bp of filtered.value.filter(b => b.needsReview)) {
      replaceRow(await confirmBaseplate(bp.id))
    }
  } catch (e) {
    error.value = e.message
  } finally {
    confirmingAll.value = false
  }
}

async function removeRow(bp) {
  if (rowLocked(bp)) return
  if (!confirm(`Delete "${bp.name || bp.partNum || 'baseplate'}"?`)) return
  try {
    await deleteBaseplate(bp.id)
    baseplates.value = baseplates.value.filter(b => b.id !== bp.id)
  } catch (e) {
    error.value = e.message
  }
}

function toggleExpand(id) {
  expanded.value = { ...expanded.value, [id]: !expanded.value[id] }
}

// ── Reservations ─────────────────────────────────────────────────────────────
function onMocQuery(id, value) {
  const draft = mocDraft.value[id] ?? { quantity: 1, setId: null, name: '' }
  mocDraft.value = { ...mocDraft.value, [id]: { ...draft, query: value, setId: null, name: '' } }
}

function onMocQty(id, value) {
  const draft = mocDraft.value[id] ?? { quantity: 1, setId: null, name: '', query: '' }
  mocDraft.value = { ...mocDraft.value, [id]: { ...draft, quantity: Math.max(1, Number(value) || 1) } }
}

function mocResults(id) {
  const draft = mocDraft.value[id]
  if (!draft || draft.setId) return []
  const q = (draft.query ?? '').trim().toLowerCase()
  if (!q) return []
  return allSets.value
    .filter(s =>
      (s.setNumber ?? '').toLowerCase().includes(q) ||
      (s.description ?? '').toLowerCase().includes(q)
    )
    .slice(0, 8)
}

function selectMocSet(id, s) {
  const draft = mocDraft.value[id] ?? { quantity: 1 }
  const label = `${s.setNumber ? s.setNumber + ' — ' : ''}${s.description}`
  mocDraft.value = { ...mocDraft.value, [id]: { ...draft, setId: s.id, name: label, query: label } }
}

async function confirmAddMoc(bp) {
  if (rowLocked(bp)) return
  const draft = mocDraft.value[bp.id]
  if (!draft?.setId) return
  try {
    replaceRow(await addReservation(bp.id, draft.setId, Math.max(1, draft.quantity || 1)))
    mocDraft.value = { ...mocDraft.value, [bp.id]: { query: '', setId: null, name: '', quantity: 1 } }
  } catch (e) {
    error.value = e.message
  }
}

async function removeMoc(bp, setId) {
  if (rowLocked(bp)) return
  if (!confirm('Remove this reservation?')) return
  try {
    replaceRow(await removeReservation(bp.id, setId))
  } catch (e) {
    error.value = e.message
  }
}

// ── Quarantine wizard ────────────────────────────────────────────────────────
function qDraft(id) {
  return quarantineDraft.value[id] ?? {
    query: '', targetId: null, cols: null, rows: null, error: '', busy: false,
  }
}

function setQDraft(id, patch) {
  quarantineDraft.value = {
    ...quarantineDraft.value,
    [id]: { ...qDraft(id), ...patch },
  }
}

// Derives cols×rows by tiling the MOC footprint with the target plate, trying
// both orientations (a 64×32 MOC can be tiled 2×2 with a 32×32 plate).
function deriveArrangement(moc, target) {
  const w = moc.widthStuds ?? 0
  const d = moc.depthStuds ?? 0
  const tw = target.widthStuds ?? 0
  const td = target.depthStuds ?? 0
  if (tw <= 0 || td <= 0) return null
  if (w % tw === 0 && d % td === 0) return { cols: w / tw, rows: d / td }
  if (w % td === 0 && d % tw === 0) return { cols: w / td, rows: d / tw }
  return null
}

function onReconcileQuery(id, value) {
  setQDraft(id, {
    query: value, targetId: null, cols: null, rows: null, error: '',
  })
}

function reconcileResults(id) {
  const d = quarantineDraft.value[id]
  if (!d || d.targetId) return []
  const q = (d.query ?? '').trim().toLowerCase()
  if (!q) return []
  return placeableBaseplates.value
    .filter(t => `${t.partNum ?? ''} ${t.name ?? ''}`.toLowerCase().includes(q))
    .slice(0, 8)
}

function selectReconcileTarget(id, target, moc) {
  const label = `${target.partNum ? target.partNum + ' — ' : ''}${target.name}`
  const arrangement = deriveArrangement(moc, target)
  setQDraft(id, {
    query: label,
    targetId: target.id,
    cols: arrangement?.cols ?? null,
    rows: arrangement?.rows ?? null,
    error: arrangement
      ? ''
      : 'Selected target does not tile the MOC footprint — enter cols/rows manually.',
  })
}

function setReconcileCols(id, value) {
  const cols = Math.max(1, Math.floor(Number(value) || 0))
  setQDraft(id, { cols, error: '' })
}

function setReconcileRows(id, value) {
  const rows = Math.max(1, Math.floor(Number(value) || 0))
  setQDraft(id, { rows, error: '' })
}

async function doReconcile(bp) {
  const d = qDraft(bp.id)
  if (!d.targetId) return
  setQDraft(bp.id, { busy: true, error: '' })
  try {
    const body = { targetBaseplateId: d.targetId }
    if (d.cols > 0) body.cols = Number(d.cols)
    if (d.rows > 0) body.rows = Number(d.rows)
    const res = await reconcileBaseplate(bp.id, body)
    if (!res?.dissolved) {
      setQDraft(bp.id, { busy: false, error: res?.reason || 'Reconciliation failed.' })
      return
    }
    await loadBaseplates()
    const next = { ...quarantineDraft.value }
    delete next[bp.id]
    quarantineDraft.value = next
  } catch (e) {
    setQDraft(bp.id, { busy: false, error: e.message })
  }
}

async function doUnquarantine(bp) {
  setQDraft(bp.id, { busy: true, error: '' })
  try {
    replaceRow(await unquarantineBaseplate(bp.id, { confirmAsPhysicalPlate: true }))
  } catch (e) {
    setQDraft(bp.id, { busy: false, error: e.message })
  }
}

async function doQuarantineDelete(bp) {
  const label = bp.name || bp.partNum || 'baseplate'
  if (!confirm(`Delete quarantined row "${label}"? This cannot be undone.`)) return
  setQDraft(bp.id, { busy: true, error: '' })
  try {
    await deleteBaseplate(bp.id)
    baseplates.value = baseplates.value.filter(b => b.id !== bp.id)
  } catch (e) {
    setQDraft(bp.id, { busy: false, error: e.message })
  }
}

// ── Images ───────────────────────────────────────────────────────────────────
function onBpFileChange(e, id) {
  const file = e.target.files[0]
  if (!file) return
  bpPendingFile.value = { ...bpPendingFile.value, [id]: file }
}

async function saveBpImage(id) {
  const file = bpPendingFile.value[id]
  if (!file) return
  try {
    await uploadBaseplateImage(id, file)
    const next = { ...bpPendingFile.value }
    delete next[id]
    bpPendingFile.value = next
    await loadBaseplates()
  } catch (e) {
    error.value = e.message
  }
}

async function removeBpImage(id) {
  if (!confirm('Remove this baseplate preview?')) return
  try {
    await deleteBaseplateImage(id)
    const next = { ...bpPendingFile.value }
    delete next[id]
    bpPendingFile.value = next
    await loadBaseplates()
  } catch (e) {
    error.value = e.message
  }
}

// ── Form ─────────────────────────────────────────────────────────────────────
function resetForm() {
  editingId.value = null
  Object.assign(form, {
    type: 'Standard', partNum: '', name: '',
    widthStuds: null, depthStuds: null,
    colorUid: '', roadShape: '', quantity: 1,
    linkedSetId: null, notes: '', legoColorIdFallback: 0,
  })
  sizeInput.value = ''
  partQuery.value = ''
  partResults.value = []
  setQuery.value = ''
  setResults.value = []
  formError.value = ''
}

function openAdd() {
  resetForm()
  formOpen.value = true
}

function openEdit(bp) {
  resetForm()
  editingId.value = bp.id
  form.type = bp.type
  form.partNum = bp.partNum ?? ''
  form.name = bp.name ?? ''
  form.widthStuds = bp.widthStuds ?? null
  form.depthStuds = bp.depthStuds ?? null
  form.roadShape = bp.roadShape ?? ''
  form.quantity = bp.quantity ?? 1
  form.linkedSetId = bp.linkedSetId ?? null
  form.notes = bp.notes ?? ''
  form.legoColorIdFallback = bp.legoColorId ?? 0
  const c = colors.value.find(col => col.id === bp.legoColorId)
  form.colorUid = c?.uniqueId ?? ''
  if (bp.widthStuds && bp.depthStuds) sizeInput.value = `${bp.widthStuds}x${bp.depthStuds}`
  formOpen.value = true
}

function closeForm() {
  formOpen.value = false
  resetForm()
}

// While a baseplate is open in the edit form, its table row is locked: the quantity stepper,
// the reservations and Delete would mutate the same document the form is about to overwrite.
// Editing has to be the only writer on that row until the form is saved or closed.
function rowLocked(bp) {
  return formOpen.value && editingId.value === bp.id
}

function onTypeChange() {
  form.partNum = ''
  form.name = ''
  form.colorUid = ''
  form.roadShape = ''
  form.linkedSetId = null
  partQuery.value = ''
  partResults.value = []
  setQuery.value = ''
  setResults.value = []
}

function applySizeInput() {
  const m = sizeInput.value.match(/(\d+)\s*[xX×]\s*(\d+)/)
  if (m) {
    form.widthStuds = Number(m[1])
    form.depthStuds = Number(m[2])
  }
}

function selectPart(r) {
  form.partNum = r.partNum ?? ''
  form.name = r.name ?? ''
  if (r.guessedStudX > 0) form.widthStuds = r.guessedStudX
  if (r.guessedStudY > 0) form.depthStuds = r.guessedStudY
  if (r.guessedStudX > 0 && r.guessedStudY > 0) sizeInput.value = `${r.guessedStudX}x${r.guessedStudY}`
  // The part dimensions come from the archive but the physical colour still has to be
  // picked in the form, and the guessed size may be wrong: the row is NOT an import.
  // Importing a legacy backlog is a separate flow (the `imported` query flag).
  partResults.value = []
  partQuery.value = ''
}

function selectSet(s) {
  form.linkedSetId = s.id
  form.partNum = s.setNumber ?? ''
  form.name = s.description ?? ''
  setResults.value = []
  setQuery.value = ''
}

async function submitForm() {
  formError.value = ''
  if (form.type !== 'Custom' && !form.partNum && !form.name) {
    formError.value = 'Select a part from the archive first.'
    return
  }
  if (form.type === 'Custom' && !form.name) {
    formError.value = 'Select a set or enter a name.'
    return
  }
  const matchedColor = colors.value.find(c => c.uniqueId === form.colorUid)
  // When no color archive is loaded we cannot resolve the current selection, so keep
  // the original color id instead of silently resetting a Standard baseplate to 0.
  const legoColorId = form.type === 'Standard'
    ? (matchedColor ? matchedColor.id : (colors.value.length === 0 ? (form.legoColorIdFallback || 0) : 0))
    : 0
  const body = {
    type: form.type,
    partNum: form.partNum ?? '',
    name: form.name ?? '',
    widthStuds: Number(form.widthStuds) || 0,
    depthStuds: Number(form.depthStuds) || 0,
    legoColorId,
    linkedSetId: form.type === 'Custom' ? (form.linkedSetId ?? null) : null,
    roadShape: form.type === 'Road' ? (form.roadShape || null) : null,
    quantity: Math.max(0, Number(form.quantity) || 0),
    notes: form.notes || null,
  }
  saving.value = true
  try {
    const saved = editingId.value
      ? await updateBaseplate(editingId.value, body)
      : await createBaseplate(body)
    replaceRow(saved)
    closeForm()
  } catch (e) {
    formError.value = e.message
  } finally {
    saving.value = false
  }
}

// ── Searches ─────────────────────────────────────────────────────────────────
watch(partQuery, async (q) => {
  if (q && q.length >= 2) partResults.value = await searchArchivePartsBaseplates(q, 10)
  else partResults.value = []
})

watch(setQuery, (q) => {
  if (!q) { setResults.value = []; return }
  const lq = q.toLowerCase()
  setResults.value = allSets.value
    .filter(s =>
      (s.setNumber ?? '').toLowerCase().includes(lq) ||
      (s.description ?? '').toLowerCase().includes(lq)
    )
    .slice(0, 8)
})
</script>

<style scoped>
.bp-page {
  padding: 1.25rem 1.5rem;
  max-width: 1200px;
}

.bp-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  flex-wrap: wrap;
  margin-bottom: 1rem;
}

h1 { margin: 0; }

.planner-link {
  color: #3a6ea5;
  font-size: 0.9rem;
  text-decoration: none;
  white-space: nowrap;
}
.planner-link:hover { text-decoration: underline; }

.error { color: #c0392b; font-size: 0.85rem; }
.muted { color: #64748b; font-size: 0.875rem; }

/* ── Summary ── */
.summary { margin-bottom: 1.25rem; }

.summary-cards {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  margin-bottom: 0.75rem;
}

.sum-card {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 0.6rem 0.9rem;
  min-width: 110px;
}

.sum-label {
  font-size: 0.72rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: #64748b;
}

.sum-value {
  font-size: 1.15rem;
  font-weight: 600;
  color: #1e293b;
}

.sum-sub {
  display: block;
  font-size: 0.75rem;
  font-weight: 400;
  color: #64748b;
}

.type-totals { display: flex; gap: 0.5rem; flex-wrap: wrap; }

.type-chip {
  font-size: 0.78rem;
  padding: 0.15rem 0.55rem;
  border-radius: 10px;
  font-weight: 500;
}
.type-chip.standard { background: #e3f0e8; color: #2a7a3a; }
.type-chip.road     { background: #e8eaf6; color: #3949ab; }
.type-chip.custom   { background: #fff3e0; color: #e65100; }

/* ── Filters ── */
.filters { margin-bottom: 1rem; }

.filter-row {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  flex-wrap: wrap;
  margin-bottom: 0.5rem;
}

.filter-label {
  font-size: 0.78rem;
  color: #64748b;
  min-width: 3.2rem;
}

.chip {
  display: inline-flex;
  align-items: center;
  gap: 0.3rem;
  background: #fff;
  border: 1px solid #cbd5e1;
  border-radius: 14px;
  padding: 0.2rem 0.7rem;
  font-size: 0.8rem;
  color: #475569;
  cursor: pointer;
}
.chip:hover { border-color: #94a3b8; color: #1e293b; }
.chip.active { background: #3a6ea5; border-color: #3a6ea5; color: #fff; }
.chip.active .swatch { border-color: rgba(255,255,255,0.6); }

.search-input {
  padding: 0.3rem 0.5rem;
  border: 1px solid #cbd5e1;
  border-radius: 4px;
  font-size: 0.85rem;
  min-width: 180px;
}
.search-input.wide { min-width: 260px; width: 100%; }

/* ── Table ── */
.data-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.88rem;
}

.data-table th,
.data-table td {
  text-align: left;
  padding: 0.45rem 0.55rem;
  border-bottom: 1px solid #eee;
  vertical-align: middle;
}

.data-table th {
  font-weight: 600;
  background: #f5f5f5;
  color: #475569;
  white-space: nowrap;
}

.data-table tr:hover td { background: #fafbfc; }

.row-review td { background: #fffdf5; }
.row-empty td { background: #fff5f5; }

.num-col { text-align: right; }
.zero { color: #c0392b; font-weight: 600; }
.id-col { color: #64748b; font-size: 0.82rem; }
.expand-col { width: 2rem; }

.expand-btn {
  background: none;
  border: none;
  cursor: pointer;
  color: #64748b;
  font-size: 0.85rem;
  padding: 0.1rem 0.3rem;
}

.preview-col { white-space: nowrap; }

.swatch {
  display: inline-block;
  width: 1rem;
  height: 1rem;
  border-radius: 2px;
  border: 1px solid rgba(0, 0, 0, 0.15);
  vertical-align: middle;
  margin-right: 0.35rem;
}

.bp-thumb {
  width: 36px;
  height: 24px;
  object-fit: cover;
  border-radius: 2px;
  border: 1px solid #e2e8f0;
  vertical-align: middle;
}

.bp-upload-label { cursor: pointer; margin-left: 0.3rem; }
.bp-upload-link { font-size: 0.85rem; color: #3b82f6; }

.remove-img-btn {
  margin-left: 0.4rem;
  font-size: 0.8rem;
  padding: 0.2rem 0.5rem;
  background: #fff;
  color: #b91c1c;
  border: 1px solid #fca5a5;
  border-radius: 4px;
  cursor: pointer;
}
.remove-img-btn:hover { background: #fef2f2; }

.bp-type-badge {
  display: inline-block;
  font-size: 0.72rem;
  padding: 0.1rem 0.45rem;
  border-radius: 10px;
  font-weight: 500;
  white-space: nowrap;
}
.bp-type-badge.standard { background: #e3f0e8; color: #2a7a3a; }
.bp-type-badge.road     { background: #e8eaf6; color: #3949ab; }
.bp-type-badge.custom   { background: #fff3e0; color: #e65100; }

.review-badge {
  display: inline-block;
  margin-left: 0.35rem;
  font-size: 0.7rem;
  padding: 0.1rem 0.4rem;
  border-radius: 10px;
  background: #fef3c7;
  color: #92400e;
  white-space: nowrap;
}

.over-badge {
  display: inline-block;
  margin-left: 0.35rem;
  font-size: 0.7rem;
  padding: 0.1rem 0.4rem;
  border-radius: 10px;
  background: #fee2e2;
  color: #b91c1c;
  white-space: nowrap;
}

.stepper {
  display: inline-flex;
  align-items: center;
  gap: 0.15rem;
}

.step-btn {
  background: #f1f5f9;
  border: 1px solid #cbd5e1;
  border-radius: 4px;
  width: 22px;
  height: 22px;
  line-height: 1;
  cursor: pointer;
  color: #475569;
}
.step-btn:disabled { opacity: 0.4; cursor: not-allowed; }

.step-input {
  width: 48px;
  padding: 0.15rem 0.25rem;
  border: 1px solid #cbd5e1;
  border-radius: 4px;
  font-size: 0.82rem;
  text-align: center;
}

.actions-col { white-space: nowrap; text-align: right; }

.import-btn {
  background: none;
  border: 1px solid #cbd5e1;
  border-radius: 4px;
  padding: 0.25rem 0.5rem;
  font-size: 0.78rem;
  color: #475569;
  cursor: pointer;
  white-space: nowrap;
  margin-left: 0.25rem;
}
.import-btn:hover { background: #f1f5f9; border-color: #94a3b8; color: #1e293b; }
.import-btn.small-btn { padding: 0.15rem 0.4rem; font-size: 0.72rem; }
.confirm-btn { color: #92400e; border-color: #fcd34d; }
.confirm-btn:hover { background: #fef3c7; }
.danger-btn { color: #c0392b; border-color: #fca5a5; }
.danger-btn:hover { background: #fef2f2; border-color: #c0392b; }

.primary.small {
  background: #3a6ea5;
  color: #fff;
  border: none;
  border-radius: 4px;
  padding: 0.3rem 0.7rem;
  font-size: 0.8rem;
  cursor: pointer;
}
.primary.small:hover:not(:disabled) { background: #2e5a8a; }
.primary.small:disabled { opacity: 0.5; cursor: not-allowed; }

/* ── Reservation panel ── */
.res-row td { background: #f8fafc; border-bottom: 1px solid #e2e8f0; }

.res-panel {
  padding: 0.6rem 0.75rem 0.9rem;
  background: #f8fafc;
}

.res-header {
  display: flex;
  align-items: baseline;
  gap: 1.25rem;
  margin-bottom: 0.5rem;
  font-size: 0.85rem;
  padding-bottom: 0.4rem;
  border-bottom: 1px solid #e2e8f0;
}

/* Two separate figures: reservations (a real commitment) and placements in layouts
   (informational only — layouts never consume availability). */
.res-stat {
  display: inline-flex;
  align-items: baseline;
  gap: 0.35rem;
}

.res-stat-value {
  font-weight: 700;
  color: #1e293b;
}

.res-list { list-style: none; margin: 0 0 0.5rem; padding: 0; }

.res-item {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  padding: 0.25rem 0;
  border-bottom: 1px solid #eef2f7;
  font-size: 0.85rem;
}
.res-item:last-child { border-bottom: none; }

.res-desc { flex: 1; }
.res-qty { color: #475569; font-weight: 600; }
.res-empty { margin: 0 0 0.5rem; }

.add-moc {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.qty-label {
  display: flex;
  align-items: center;
  gap: 0.3rem;
  font-size: 0.8rem;
  color: #475569;
}

.search-wrap { position: relative; }

.dropdown {
  position: absolute;
  top: 100%;
  left: 0;
  z-index: 200;
  background: #fff;
  border: 1px solid #cbd5e1;
  border-radius: 4px;
  margin: 0;
  padding: 0;
  list-style: none;
  min-width: 320px;
  max-height: 220px;
  overflow-y: auto;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.12);
}

.dropdown-item {
  padding: 0.35rem 0.65rem;
  font-size: 0.85rem;
  cursor: pointer;
}
.dropdown-item:hover { background: #f0f5ff; }

/* ── Form ── */
.bp-form {
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 1rem 1.25rem 1.25rem;
  margin-bottom: 1.25rem;
}

.bp-form h2 {
  margin: 0 0 0.75rem;
  font-size: 1rem;
  color: #1e293b;
}

.form-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem 1rem;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  font-size: 0.82rem;
  color: #475569;
}

.field.grow { flex: 1; min-width: 220px; }
.search-field { min-width: 260px; }

.field select,
.text-input,
.num-input {
  padding: 0.3rem 0.45rem;
  border: 1px solid #cbd5e1;
  border-radius: 4px;
  font-size: 0.85rem;
  background: #fff;
}

.num-input { width: 80px; }

.form-actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-top: 0.9rem;
}

/* ── Quarantine section ── */
.quarantine-section {
  border: 1px solid #fcd34d;
  background: #fffbeb;
  border-radius: 8px;
  padding: 0.9rem 1rem 1rem;
  margin-bottom: 1.25rem;
}

.q-title {
  display: flex;
  align-items: baseline;
  gap: 0.5rem;
  flex-wrap: wrap;
  margin: 0 0 0.35rem;
  font-size: 1.05rem;
  color: #92400e;
}

.q-subtitle {
  font-size: 0.78rem;
  font-weight: 400;
  color: #b45309;
}

.q-intro {
  margin: 0 0 0.75rem;
  font-size: 0.85rem;
  color: #78350f;
}

.quarantine-card {
  background: #fff;
  border: 1px solid #fde68a;
  border-radius: 6px;
  padding: 0.6rem 0.75rem;
  margin-bottom: 0.6rem;
}
.quarantine-card:last-child { margin-bottom: 0; }

.q-head {
  display: flex;
  align-items: baseline;
  gap: 0.6rem;
  flex-wrap: wrap;
  margin-bottom: 0.5rem;
  font-size: 0.88rem;
}

.q-name { color: #1e293b; }
.q-size { color: #475569; }
.q-reason {
  font-size: 0.75rem;
  padding: 0.1rem 0.5rem;
  border-radius: 10px;
  background: #fef3c7;
  color: #92400e;
  white-space: nowrap;
}

.q-options {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.q-option {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  flex-wrap: wrap;
}

.q-option-label {
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: #92400e;
  min-width: 11.5rem;
}

.q-option-body {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
  flex: 1;
}

.q-error { margin: 0.5rem 0 0; }
</style>
