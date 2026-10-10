import { createApp } from 'vue'
import App from './App.vue'
import router from './router/index.js'
import { useAuth } from './composables/useAuth.js'

// Resolve the session before the first navigation so the guard knows whether the user is
// authenticated (and with which role) instead of bouncing to /login on every reload.
const auth = useAuth()
auth.fetchMe().finally(() => {
  createApp(App).use(router).mount('#app')
})
