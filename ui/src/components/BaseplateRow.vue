<template>
<tr :class="{ 'row-review': bp.needsReview, 'row-empty': (bp.availableQuantity ?? 0) === 0 }">
  <td class="expand-col">
    <button class="expand-btn" :title="expanded[bp.id] ? 'Collapse' : 'Show reservations'" @click="handlers.toggleExpand(bp.id)">
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
        :src="getImageUrl(bp.id)"
        class="bp-thumb"
        :alt="bp.name"
        @error="e => e.target.style.display = 'none'"
      />
      <label class="bp-upload-label" :title="bp.imageCached ? 'Replace image' : 'Upload image'">
        <input type="file" accept="image/*" style="display:none" @change="e => onBpFileChange(e, bp.id)" />
        <span class="bp-upload-link">{{ bp.imageCached ? 'Replace' : 'Upload' }}</span>
      </label>
      <button v-if="bpPendingFile[bp.id]" class="primary small" @click="handlers.saveBpImage(bp.id)">Save</button>
      <button
        v-if="bp.imageCached"
        class="remove-img-btn"
        title="Remove the uploaded preview"
        @click="handlers.removeBpImage(bp.id)"
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
      <button class="step-btn" :disabled="rowLocked(bp) || (bp.quantity ?? 0) <= 0" @click="handlers.saveQuantity(bp, (bp.quantity ?? 0) - 1)">−</button>
      <input
        class="step-input"
        type="number"
        min="0"
        :value="bp.quantity ?? 0"
        :disabled="rowLocked(bp)"
        @change="e => saveQuantity(bp, e.target.value)"
      />
      <button class="step-btn" :disabled="rowLocked(bp)" @click="handlers.saveQuantity(bp, (bp.quantity ?? 0) + 1)">+</button>
    </div>
  </td>
  <td class="num-col" :class="{ 'zero': (bp.availableQuantity ?? 0) === 0 }">{{ bp.availableQuantity ?? 0 }}</td>
  <td>
    <span v-if="bp.type === 'Road'">{{ bp.roadShape || '—' }}</span>
    <span v-else class="muted">—</span>
  </td>
  <td class="actions-col">
    <button class="import-btn" :disabled="rowLocked(bp)" @click="handlers.openEdit(bp)">Edit</button>
    <button v-if="bp.needsReview" class="import-btn confirm-btn" :disabled="rowLocked(bp)" @click="handlers.confirmRow(bp)">Confirm</button>
    <button class="import-btn danger-btn" :disabled="rowLocked(bp)" @click="handlers.removeRow(bp)">Delete</button>
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
          <button class="import-btn danger-btn small-btn" :disabled="rowLocked(bp)" @click="handlers.removeMoc(bp, r.setId)">Remove</button>
        </li>
      </ul>
      <p v-else class="muted res-empty">No reservations.</p>

      <div class="add-moc">
        <div class="search-wrap">
          <input
            class="search-input"
            placeholder="+ Add MOC (search set…)"
            :value="handlers.mocDraft[bp.id]?.query ?? ''"
            :disabled="rowLocked(bp)"
            @input="handlers.onMocQuery(bp.id, $event.target.value)"
          />
          <ul v-if="handlers.mocResults(bp.id).length > 0" class="dropdown">
            <li
              v-for="s in handlers.mocResults(bp.id)"
              :key="s.id"
              class="dropdown-item"
              @click="handlers.selectMocSet(bp.id, s)"
            >{{ s.setNumber ? s.setNumber + ' — ' : '' }}{{ s.description }}{{ s.isMoc ? ' (MOC)' : '' }}</li>
          </ul>
        </div>
        <label class="qty-label">qty
          <input
            class="step-input"
            type="number"
            min="1"
            :value="handlers.mocDraft[bp.id]?.quantity ?? 1"
            :disabled="rowLocked(bp)"
            @input="handlers.onMocQty(bp.id, $event.target.value)"
          />
        </label>
        <button
          class="primary small"
          :disabled="!handlers.mocDraft[bp.id]?.setId || rowLocked(bp)"
          @click="handlers.confirmAddMoc(bp)"
        >Add</button>
      </div>
    </div>
  </td>
</tr>

</template>

<script setup>
import './baseplateRows.css'
// One catalogue row plus its expandable reservations panel.
//
// Extracted from BaseplateListView so the flat and the grouped rendering share the exact same
// markup: duplicating 140 lines of table rows would have guaranteed they drift apart.
const props = defineProps({
  bp: { type: Object, required: true },
  expanded: { type: Object, required: true },
  rowLocked: { type: Function, required: true },
  getImageUrl: { type: Function, required: true },
  bpPendingFile: { type: Object, required: true },
  handlers: { type: Object, required: true },
})
</script>
