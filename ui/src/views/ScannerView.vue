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
    </div>

    <p v-if="cameraError" class="error">{{ cameraError }}</p>
    <p v-if="error" class="error">{{ error }}</p>

    <div v-if="result" class="result card">
      <header class="result-header">
        <span class="kind-badge">{{ result.kind }}</span>
        <span class="piece-id">{{ result.title }}</span>
        <span v-if="result.colorName" class="color-badge">
          <span v-if="result.colorRgb" class="swatch" :style="{ background: '#' + result.colorRgb }"></span>
          {{ result.colorName }}
        </span>
      </header>
      <p v-if="result.subtitle" class="piece-desc">{{ result.subtitle }}</p>

      <!-- Box: direct navigation to the box itself -->
      <template v-if="result.kind === 'Box'">
        <RouterLink :to="`/boxes/${result.id}`" class="alloc-link root-link">
          Open box
        </RouterLink>
      </template>

      <!-- Piece / Set: show storage allocations -->
      <template v-else>
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
        <RouterLink :to="detailLink" class="open-detail">Open detail →</RouterLink>
      </template>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, nextTick } from 'vue'
import { resolveCode } from '../api/scanner.js'

const code = ref('')
const codeInput = ref(null)
const loading = ref(false)
const error = ref('')
const result = ref(null)

const detailLink = computed(() => {
  if (!result.value) return '/'
  if (result.value.kind === 'Box') return `/boxes/${result.value.id}`
  if (result.value.kind === 'Set') return `/sets/${result.value.id}`
  return `/bulkpieces/${result.value.id}`
})

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

  const onDecoded = (decodedText) => {
    // Ignore spurious/false-positive frames: only codes for labels belong to TroveKeep
    // (TK:BP:, TK:SET:, TK:BOX:). This stops the camera from flashing closed on random
    // patterns picked up by the webcam.
    const t = (decodedText ?? '').trim()
    if (!/^TK:(BP|SET|BOX):/i.test(t)) return
    code.value = t
    stopCamera().then(resolve)
  }
  const onDecodeError = () => { /* per-frame decode errors are ignorable */ }
  const config = { fps: 10, qrbox: { width: 220, height: 220 } }

  try {
    const { Html5Qrcode } = await import('html5-qrcode')

    // Ensure the target <div> is mounted before html5-qrcode attaches to it.
    await nextTick()
    scanner = new Html5Qrcode('trovekeep-camera')

    // Per html5-qrcode source (createVideoConstraints), a config object is ONLY valid
    // with exactly one key, either { facingMode: 'user'|'environment' } or
    // { deviceId: ... } — never an empty object. Prefer the rear camera, then (on
    // desktop where there is no rear camera) fall back to 'user'. Use a FRESH
    // Html5Qrcode instance per attempt: stop()/start() on the same instance raises
    // "already under transition".
    const attempts = [
      { facingMode: 'environment' },
      { facingMode: 'user' },
    ]

    let lastError = null
    for (let i = 0; i < attempts.length; i++) {
      if (i > 0) {
        try { scanner.clear() } catch { /* ignore */ }
        await nextTick()
        scanner = new Html5Qrcode('trovekeep-camera')
        await nextTick()
      }
      try {
        await scanner.start(attempts[i], config, onDecoded, onDecodeError)
        cameraError.value = ''
        return
      } catch (err) {
        lastError = err
        cameraError.value = `Camera attempt failed: ${err?.message ?? err}`
      }
    }

    cameraError.value = `Camera unavailable: ${lastError?.message ?? 'no camera could be started'}`
    cameraActive.value = false
    await stopCamera().catch(() => {})
  } catch (e) {
    cameraError.value = `Camera unavailable: ${e?.message ?? e}`
    cameraActive.value = false
    await stopCamera().catch(() => {})
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
  min-height: 220px;
  margin: 0 auto;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  overflow: hidden;
  background: #000;
}

.camera-target :deep(video) {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.camera-target :deep(canvas) {
  display: none;
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

.kind-badge {
  font-size: var(--text-xs);
  font-weight: 700;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: var(--color-accent);
  background: var(--color-accent-soft);
  border: 1px solid var(--color-accent);
  border-radius: 4px;
  padding: 2px 6px;
}

.root-link {
  display: inline-flex;
  margin-top: var(--space-3);
  font-weight: 600;
}

.open-detail {
  display: inline-block;
  margin-top: var(--space-3);
  font-size: var(--text-sm);
}
</style>
