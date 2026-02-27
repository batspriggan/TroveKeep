<template>
  <div class="color-select-wrap">
    <div v-if="selectedColor" class="swatch-preview" :style="{ background: '#' + selectedColor.rgb }" :title="selectedColor.name"></div>
    <select :value="modelValue" @change="$emit('update:modelValue', +$event.target.value)" required>
      <option value="0" disabled>— select color —</option>
      <option v-for="c in colors" :key="c.id" :value="c.id">{{ c.name }}</option>
    </select>
  </div>
</template>

<script setup>
import { computed } from 'vue'

const props = defineProps({
  modelValue: { type: Number, default: 0 },
  colors: { type: Array, default: () => [] },
})
defineEmits(['update:modelValue'])

const selectedColor = computed(() =>
  props.colors.find(c => c.id === props.modelValue) ?? null
)
</script>

<style scoped>
.color-select-wrap {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.swatch-preview {
  width: 1.25rem;
  height: 1.25rem;
  border-radius: 3px;
  border: 1px solid rgba(0, 0, 0, 0.2);
  flex-shrink: 0;
}

select {
  flex: 1;
}
</style>
