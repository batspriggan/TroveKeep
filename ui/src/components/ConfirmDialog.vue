<template>
  <div v-if="open" class="overlay" @click.self="$emit('cancel')">
    <div class="dialog">
      <p>{{ message }}</p>
      <div class="btns">
        <button @click="$emit('cancel')">Cancel</button>
        <button class="danger" @click="$emit('confirm')">Delete</button>
      </div>
    </div>
  </div>
</template>

<script setup>
defineProps({
  open: Boolean,
  message: { type: String, default: 'Are you sure you want to delete this item?' },
})
defineEmits(['confirm', 'cancel'])
</script>

<style scoped>
.overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.4);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 100;
}

.dialog {
  background: #fff;
  border-radius: 8px;
  padding: 1.5rem;
  min-width: 280px;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.15);
}

p {
  margin-bottom: 1.25rem;
}

.btns {
  display: flex;
  gap: 0.5rem;
  justify-content: flex-end;
}
</style>
