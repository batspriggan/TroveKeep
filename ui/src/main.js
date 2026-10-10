import { createApp } from 'vue'
import App from './App.vue'
import router from './router/index.js'
import { useAuth } from './composables/useAuth.js'
import { useAppMode } from './composables/useAppMode.js'

// Resolve the application mode and the session before the first navigation, so the router guard
// knows whether the instance still needs setup, runs single-user (no session) or is multi-user —
// instead of bouncing to /login on every reload.
const auth = useAuth()
const appMode = useAppMode()

async function bootstrap() {
  await appMode.refresh()
  // In an uninitialized instance every other endpoint answers 503, so the probe is pointless.
  if (appMode.initialized) await auth.fetchMe()
  createApp(App).use(router).mount('#app')
}

bootstrap()
