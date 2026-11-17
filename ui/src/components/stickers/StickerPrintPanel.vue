<template>
  <section class="card print-panel">
    <h2>Print sheet</h2>
    <div class="totals">
      <span><strong>{{ totalCopies }}</strong> labels</span>
      <span><strong>{{ plan ? plan.pages.length : 0 }}</strong> A4 pages</span>
      <span v-if="plan" class="muted">{{ perPageLabel }}</span>
    </div>

    <p v-if="planError" class="error">{{ planError }}</p>
    <p v-else-if="totalCopies === 0" class="muted">
      Add an item with a number of copies to compose a sheet.
    </p>

    <div class="options">
      <label title="White margin of the sheet, in mm (5–20)">
        Sheet margin
        <input type="number" step="0.1" min="5" max="20" :value="options.marginMm"
               @input="setOption('marginMm', $event.target.value)" />
      </label>
      <label title="Thickness of the black cut border, in mm (0–3)">
        Cut border
        <input type="number" step="0.1" min="0" max="3" :value="options.borderMm"
               @input="setOption('borderMm', $event.target.value)" />
      </label>
      <label title="Inner border: room between the black border and the artwork, in mm (0–3)">
        Inner border
        <input type="number" step="0.1" min="0" max="3" :value="options.paddingMm"
               @input="setOption('paddingMm', $event.target.value)" />
      </label>
      <label title="Distance between two cut lines, in mm (0–10)">
        Gap between labels
        <input type="number" step="0.1" min="0" max="10" :value="options.gapMm"
               @input="setOption('gapMm', $event.target.value)" />
      </label>
      <label title="Grows (+) or shrinks (−) the label on every side, in mm; use it when the printer is slightly off scale">
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
      <button @click="sheet.resetOptions()">Reset layout</button>
      <span class="hint">
        Print at <strong>100%</strong> and disable “fit to page”, otherwise the labels are not
        tile-sized.
      </span>
    </div>

    <p v-if="progress" class="muted">{{ progress }}</p>
    <p v-if="printMessage" class="ok">{{ printMessage }}</p>
    <p v-if="printError" class="error">{{ printError }}</p>
  </section>
</template>

<script setup>
import { computed, ref } from 'vue'
import { useStickerSheet } from '../../composables/useStickerSheet.js'
import { loadStickerImages } from '../../utils/stickerImages.js'
import { downloadStickerPdf } from '../../utils/stickerPdf.js'

const props = defineProps({
  /** The items of the project being printed (embedded `StickerItemResponse`). */
  items: { type: Array, default: () => [] },
  /** The image library, used only to cache-bust a replaced artwork (`updatedAt`). */
  images: { type: Array, default: () => [] },
})

const sheet = useStickerSheet()
/** Layout options from the shared composer state; the template binds them by name. */
const options = sheet.options

const generating = ref(false)
const progress = ref('')
const printMessage = ref('')
const printError = ref('')

const totalCopies = computed(() => sheet.totalCopies(props.items))
const layout = computed(() => sheet.planFor(props.items))
const plan = computed(() => layout.value.plan)
const planError = computed(() => layout.value.error?.message ?? '')

const perPageLabel = computed(() => {
  if (!plan.value || plan.value.pages.length === 0) return ''
  const first = plan.value.pages[0].cells.length
  return `${first} labels on the first sheet`
})

function setOption(key, value) {
  sheet.setOption(key, value)
}

/** The artwork version of every image in the library, so a replaced file is re-fetched. */
const versions = computed(() => new Map((props.images ?? []).map((i) => [String(i.id), i.updatedAt])))

async function generatePdf() {
  printMessage.value = ''
  printError.value = ''
  progress.value = ''

  const { plan: current, error: planFailure } = sheet.planFor(props.items)
  if (planFailure || !current || current.count === 0) {
    printError.value = planFailure?.message || 'Nothing to print.'
    return
  }

  generating.value = true
  try {
    // The engine units carry the item id, the fit and the copies; the artwork loader needs the
    // image id (and its version) of the very same rows.
    const selection = sheet.selection(props.items)
    const printable = sheet.printableItems(props.items).map((item) => ({
      ...item,
      updatedAt: versions.value.get(String(item.imageId)),
    }))
    const rasters = await loadStickerImages(printable, {
      plan: current,
      options: sheet.options,
      onProgress: ({ done, total }) => { progress.value = `Rasterising ${done}/${total}…` },
    })
    const result = downloadStickerPdf(selection, sheet.options, rasters)
    printMessage.value = `Generated ${result.fileName}: ${result.count} labels on ${result.pages} page(s).`
  } catch (e) {
    printError.value = e.message || 'Could not generate the PDF.'
  } finally {
    progress.value = ''
    generating.value = false
  }
}
</script>

<style scoped>
.muted {
  color: #64748b;
  font-size: 0.85rem;
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
  flex-wrap: wrap;
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
</style>
