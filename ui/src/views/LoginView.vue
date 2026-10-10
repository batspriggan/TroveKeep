<template>
  <div class="login-page">
    <form class="login-card" @submit.prevent="submit">
      <h1>TroveKeep</h1>
      <p class="subtitle">Accedi per continuare</p>

      <label>
        Email
        <input v-model="email" type="email" autocomplete="username" required autofocus />
      </label>

      <label>
        Password
        <input v-model="password" type="password" autocomplete="current-password" required />
      </label>

      <p v-if="error" class="error">{{ error }}</p>

      <button type="submit" :disabled="loading">
        {{ loading ? 'Accesso…' : 'Accedi' }}
      </button>

      <template v-if="oidcEnabled">
        <div class="separator"><span>oppure</span></div>
        <button type="button" class="oidc" @click="loginWithPangolin">
          Accedi con Pangolin
        </button>
      </template>
    </form>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuth } from '../composables/useAuth.js'
import { useAppMode } from '../composables/useAppMode.js'
import { getProviders } from '../api/auth.js'

const route = useRoute()
const router = useRouter()
const auth = useAuth()
const appMode = useAppMode()

const email = ref('')
const password = ref('')
// Pre-filled from a failed OIDC round-trip (?error=…), then reused for local login errors.
const error = ref(route.query.error ? String(route.query.error) : '')
const loading = ref(false)
const oidcEnabled = ref(false)

onMounted(async () => {
  // Login does not exist in single-user mode (the backend answers 404) and is meaningless before
  // the first-run setup: leave instead of showing a form that cannot work.
  if (!appMode.initialized) {
    router.replace('/setup')
    return
  }
  if (!appMode.multiUserEnabled) {
    router.replace('/')
    return
  }

  try {
    const providers = await getProviders()
    oidcEnabled.value = providers?.oidc === true
  } catch {
    // The login page must stay usable even if the probe fails: fall back to local auth only.
    oidcEnabled.value = false
  }
})

// Full-page navigation on purpose: the challenge endpoint answers with a redirect to Pangolin.
function loginWithPangolin() {
  window.location.href = '/api/auth/oidc/challenge'
}

async function submit() {
  error.value = ''
  loading.value = true
  try {
    await auth.login(email.value, password.value)
    // Honour a redirect set by the guard, otherwise land on the home route.
    const target = router.currentRoute.value.query.redirect ?? '/'
    router.replace(typeof target === 'string' ? target : '/')
  } catch (e) {
    // The API returns a uniform failure for unknown email and wrong password on purpose.
    error.value = 'Email o password non corretti.'
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.login-page {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  background: #0f172a;
  padding: 1rem;
}

.login-card {
  background: #1e293b;
  color: #e2e8f0;
  padding: 2rem;
  border-radius: 12px;
  width: 100%;
  max-width: 360px;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.4);
}

h1 {
  margin: 0;
  font-size: 1.4rem;
  color: #f8fafc;
}

.subtitle {
  margin: 0 0 0.5rem;
  font-size: 0.85rem;
  color: #94a3b8;
}

label {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  font-size: 0.85rem;
  color: #cbd5e1;
}

input {
  padding: 0.6rem 0.7rem;
  border-radius: 6px;
  border: 1px solid #334155;
  background: #0f172a;
  color: #f1f5f9;
  font-size: 0.95rem;
}

input:focus {
  outline: none;
  border-color: #3b82f6;
}

button {
  margin-top: 0.5rem;
  padding: 0.65rem;
  border: none;
  border-radius: 6px;
  background: #3b82f6;
  color: #fff;
  font-size: 0.95rem;
  font-weight: 600;
  cursor: pointer;
}

button:disabled {
  opacity: 0.6;
  cursor: default;
}

.error {
  margin: 0;
  color: #f87171;
  font-size: 0.85rem;
}

.separator {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  color: #64748b;
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.separator::before,
.separator::after {
  content: '';
  flex: 1;
  height: 1px;
  background: #334155;
}

button.oidc {
  margin-top: 0;
  background: #475569;
}
</style>
