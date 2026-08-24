<template>
  <div class="page">
    <h1>Scanner</h1>
    <p class="muted">
      Scan a label QR code with your scanner (or type/paste the code and press Enter)
      to locate the piece.
    </p>

    <div class="scan-row">
      <input
        ref="codeInput"
        v-model="code"
        class="scan-input"
        placeholder="Scan or paste code…"
        autocomplete="off"
        @keyup.enter="resolve"
      />
      <button class="primary" :disabled="!code.trim() || loading" @click="resolve">
        {{ loading ? 'Searching…' : 'Find' }}
      </button>
      <button class="secondary" :disabled="cameraActive" @click="toggleCamera">
        {{ cameraActive ? 'Stop camera' : 'Use camera' }}
      </button>
    </div>

    <div v-if="cameraActive" class="camera-box">
      <div id="trovekeep-camera" ref="cameraEl" class="camera-target"></div>
      <p v-if="cameraError" class="error">{{ cameraError }}</p>
    </div>

    <p v-if="error" class="error">{{ error }}</p>

    <div v-if="result" class="result card">
      <header class="result-header">
        <span class="piece-id">{{ result.legoId }}</span>
        <span v-if="result.legoColorName" class="color-badge">
          <span v-if="result.legoColorRgb" class="swatch" :style="{ background: '#' + result.legoColorRgb }"></span>
          {{ result.legoColorName }}
        </span>
      </header>
      <p class="piece-desc">{{ result.description }}</p>

      <template v-if="result.allocations?.length">
        <h2 class="alloc-title">Stored in</h2>
        <ul class="alloc-list">
          <li v-for="a in result.allocations" :key="`${a.storageType}-${a.storageId}-${a.drawerPosition ?? ''}`">
            <RouterLink :to="locationLink(a)" class="alloc-link">
              <strong>{{ a.storageName }}</strong>
              <span class="alloc-type">{{ a.storageType }}</span>
              <span class="alloc-qty">× {{ a.quantity }}</span>
            </RouterLink>
          </li>
        </ul>
      </template>
      <p v-else class="muted no-alloc">Not stored anywhere yet.</p>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import { resolveCode } from '../api/scanner.js'

const code = ref('')
const codeInput = ref(null)
const loading = ref(false)
const error = ref('')
const result = ref(null)

const cameraActive = ref(false)
const cameraError = ref('')
const cameraEl = ref(null)
let scanner = null

function locationLink(a) {
  return a.storageType === 'Box'
    ? `/boxes/${a.storageId}`
    : `/drawers/${a.storageId}/${a.drawerPosition}`
}

async function resolve() {
  const value = code.value.trim()
  if (!value) return
  loading.value = true
  error.value = ''
  result.value = null
  try {
    result.value = await resolveCode(value)
    code.value = ''
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
    codeInput.value?.focus()
  }
}

async function toggleCamera() {
  if (cameraActive.value) {
    await stopCamera()
    return
  }

  cameraError.value = ''
  cameraActive.value = true
  try {
    const { Html5Qrcode } = await import('html5-qrcode')
    scanner = new Html5Qrcode('trovekeep-camera')

    await scanner.start(
      { facingMode: 'environment' },
      { fps: 10, qrbox: { width: 220, height: 220 } },
      (decodedText) => {
        code.value = decodedText
        stopCamera().then(resolve)
      },
      () => { /* per-frame decode errors are ignorable */ },
    )
  } catch (e) {
    cameraError.value = `Camera unavailable: ${e.message ?? e}`
    cameraActive.value = false
  }
}

async function stopCamera() {
  cameraActive.value = false
  if (scanner) {
    try { await scanner.stop() } catch { /* ignore */ }
    try { scanner.clear() } catch { /* ignore */ }
    scanner = null
  }
}

onMounted(() => codeInput.value?.focus())
onUnmounted(() => { stopCamera() })
</script>

<style scoped>
.page { max-width: 720px; }

.muted { color: var(--color-text-muted); font-size: var(--text-sm); }

.scan-row {
  display: flex;
  gap: var(--space-2);
  align-items: center;
  flex-wrap: wrap;
  margin-bottom: var(--space-4);
}

.scan-input {
  flex: 1;
  min-width: 220px;
  font-family: var(--font-mono);
  font-size: var(--text-base);
  padding: var(--space-2) var(--space-3);
}

.camera-box {
  margin-bottom: var(--space-4);
}

.camera-target {
  width: 100%;
  max-width: 320px;
  margin: 0 auto;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  overflow: hidden;
}

.card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: var(--space-4);
}

.result-header {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  flex-wrap: wrap;
}

.piece-id {
  font-family: var(--font-mono);
  font-size: var(--text-xl);
  font-weight: 500;
}

.color-badge {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  font-size: var(--text-sm);
  color: var(--color-text-secondary);
  background: var(--color-surface-alt);
  border: 1px solid var(--color-border);
  border-radius: 20px;
  padding: 2px var(--space-3);
}

.swatch {
  width: 12px;
  height: 12px;
  border-radius: 3px;
  border: 1px solid rgba(0, 0, 0, 0.15);
}

.piece-desc {
  font-size: var(--text-base);
  color: var(--color-text-secondary);
  margin: var(--space-2) 0 0;
}

.alloc-title {
  font-size: var(--text-sm);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
  margin: var(--space-4) 0 var(--space-2);
}

.alloc-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}

.alloc-link {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-2) var(--space-3);
  border: 1px solid var(--color-border);
  border-radius: 6px;
  text-decoration: none;
  color: var(--color-text-primary);
  transition: border-color var(--transition-fast), background var(--transition-fast);
}

.alloc-link:hover {
  border-color: var(--color-accent);
  background: var(--color-surface-alt);
}

.alloc-type {
  font-size: var(--text-xs);
  color: var(--color-text-muted);
}

.alloc-qty {
  margin-left: auto;
  font-family: var(--font-mono);
  font-size: var(--text-sm);
  color: var(--color-text-secondary);
}

.no-alloc { margin-top: var(--space-3); }
</style>
