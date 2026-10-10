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
    </form>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuth } from '../composables/useAuth.js'

const router = useRouter()
const auth = useAuth()

const email = ref('')
const password = ref('')
const error = ref('')
const loading = ref(false)

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
</style>
