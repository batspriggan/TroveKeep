<template>
  <div class="picker">
    <div class="picker-head">
      <input v-model="query" type="search" class="search" placeholder="Filter the artwork…" />
      <span class="count muted">{{ filtered.length }} of {{ images.length }}</span>
    </div>

    <p v-if="images.length === 0" class="muted empty">
      The image library is empty.
      <RouterLink to="/stickers">Upload artwork</RouterLink> first.
    </p>
    <p v-else-if="filtered.length === 0" class="muted empty">No artwork matches “{{ query }}”.</p>

    <div v-else class="grid">
      <button
        v-for="image in filtered"
        :key="image.id"
        type="button"
        class="option"
        :class="{ selected: String(modelValue) === String(image.id) }"
        :title="image.name"
        @click="$emit('update:modelValue', image.id)"
      >
        <span class="thumb" :class="{ missing: !image.imageCached }">
          <img v-if="image.imageCached" :src="stickerArtworkUrl(image)" :alt="image.name" loading="lazy" />
          <span v-else class="missing-label">no artwork</span>
        </span>
        <span class="name">{{ image.name }}</span>
      </button>
    </div>
  </div>
</template>

<script setup>
import { computed, ref } from 'vue'
import { stickerArtworkUrl } from '../../utils/stickerImages.js'

const props = defineProps({
  /** The global image library (`StickerImageResponse[]`). */
  images: { type: Array, default: () => [] },
  /** Selected image id. */
  modelValue: { type: String, default: null },
})

defineEmits(['update:modelValue'])

const query = ref('')

const filtered = computed(() => {
  const q = query.value.trim().toLowerCase()
  if (!q) return props.images
  return props.images.filter((image) => image.name.toLowerCase().includes(q))
})
</script>

<style scoped>
.picker-head {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.5rem;
}

.search {
  max-width: 260px;
}

.muted {
  color: #64748b;
  font-size: 0.8rem;
}

.empty {
  margin: 0.25rem 0;
}

.grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(96px, 1fr));
  gap: 0.5rem;
  max-height: 320px;
  overflow-y: auto;
  padding: 0.25rem;
}

.option {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  padding: 0.3rem;
  border: 2px solid transparent;
  border-radius: 6px;
  background: #fff;
  cursor: pointer;
  text-align: left;
}

.option.selected {
  border-color: #2563eb;
  background: #eff6ff;
}

.thumb {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 64px;
  border-radius: 4px;
  background: #f1f5f9;
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
  font-size: 0.7rem;
  color: #94a3b8;
}

.name {
  font-size: 0.75rem;
  color: #1a1917;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
