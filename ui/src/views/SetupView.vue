<template>
  <div class="setup-page">
    <form class="setup-card" @submit.prevent="submit">
      <h1>TroveKeep</h1>
      <p class="subtitle">Configurazione iniziale</p>

      <fieldset class="mode" :disabled="loading">
        <legend>Modalità utente</legend>
        <label class="mode-option" :class="{ selected: !multiUser }">
          <input v-model="multiUser" type="radio" :value="false" />
          <span class="mode-text">
            <strong>Mono-utente</strong>
            <span class="hint">Nessun login: chiunque accede opera come amministratore tecnico.</span>
          </span>
        </label>
        <label class="mode-option" :class="{ selected: multiUser }">
          <input v-model="multiUser" type="radio" :value="true" />
          <span class="mode-text">
            <strong>Multiutente</strong>
            <span class="hint">Login con email e password, ruoli e permessi per collezione.</span>
          </span>
        </label>
      </fieldset>

      <template v-if="multiUser">
        <label>
          Email amministratore
          <input v-model.trim="email" type="email" autocomplete="username" required />
        </label>

        <label>
          Password (minimo 8 caratteri)
          <input v-model="password" type="password" autocomplete="new-password" minlength="8" required />
        </label>

        <label>
          Conferma password
          <input v-model="confirm" type="password" autocomplete="new-password" minlength="8" required />
        </label>
      </template>

      <label>
        Token di setup
        <input v-model.trim="token" type="text" autocomplete="off" spellcheck="false" required />
      </label>
      <p class="hint token-hint">
        Il token è stampato nei log del server all'avvio, nella riga
        <code>TroveKeep setup token: …</code>.
      </p>

      <p v-if="error" class="error">{{ error }}</p>

      <button type="submit" :disabled="loading">
        {{ loading ? 'Configurazione…' : 'Completa configurazione' }}
      </button>
    </form>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAppMode } from '../composables/useAppMode.js'
import { useAuth } from '../composables/useAuth.js'
import { completeSetup } from '../api/setup.js'

const router = useRouter()
const appMode = useAppMode()
const auth = useAuth()

const multiUser = ref(false)
const email = ref('')
const password = ref('')
const confirm = ref('')
const token = ref('')
const error = ref('')
const loading = ref(false)

/** Clear messages per status code; the server message is only a fallback. */
function messageFor(e) {
  switch (e.status) {
    case 400:
      return e.message || 'Dati non validi: controlla email e password.'
    case 403:
      return 'Token di setup non valido. Controlla la riga "TroveKeep setup token" nei log del server.'
    case 409:
      return "L'istanza è già configurata: la modalità non può essere cambiata da qui."
    default:
      return e.message || 'Configurazione non riuscita.'
  }
}

async function submit() {
  error.value = ''

  if (multiUser.value) {
    if (password.value.length < 8) {
      error.value = 'La password deve essere lunga almeno 8 caratteri.'
      return
    }
    if (password.value !== confirm.value) {
      error.value = 'Le due password non coincidono.'
      return
    }
  }

  loading.value = true
  try {
    const result = await completeSetup({
      multiUser: multiUser.value,
      email: multiUser.value ? email.value : null,
      password: multiUser.value ? password.value : null,
      token: token.value,
    })

    // Pick up the freshly stored state before navigating: the guard reads it.
    await appMode.refresh()
    // Multi-user needs a session; single-user resolves straight to the technical admin.
    await auth.fetchMe()

    const enabled = result?.multiUserEnabled ?? multiUser.value
    router.replace(enabled ? '/login' : '/')
  } catch (e) {
    error.value = messageFor(e)
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.setup-page {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  background: #0f172a;
  padding: 1rem;
}

.setup-card {
  background: #1e293b;
  color: #e2e8f0;
  padding: 2rem;
  border-radius: 12px;
  width: 100%;
  max-width: 420px;
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
  margin: -0.75rem 0 0;
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

.mode {
  border: 1px solid #334155;
  border-radius: 8px;
  padding: 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin: 0;
}

.mode legend {
  padding: 0 0.35rem;
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: #94a3b8;
}

.mode-option {
  flex-direction: row;
  align-items: flex-start;
  gap: 0.6rem;
  padding: 0.5rem 0.6rem;
  border: 1px solid #334155;
  border-radius: 6px;
  cursor: pointer;
}

.mode-option.selected {
  border-color: #3b82f6;
  background: rgba(59, 130, 246, 0.12);
}

.mode-option input {
  width: auto;
  margin-top: 0.15rem;
}

.mode-text {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}

.mode-text strong {
  color: #f1f5f9;
  font-size: 0.9rem;
}

.hint {
  font-size: 0.78rem;
  color: #94a3b8;
}

.token-hint {
  margin: -0.5rem 0 0;
}

code {
  font-family: var(--font-mono);
  color: #cbd5e1;
}

.error {
  margin: 0;
  color: #f87171;
  font-size: 0.85rem;
}
</style>
