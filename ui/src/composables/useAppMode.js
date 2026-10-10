import { reactive } from 'vue'
import { getSetupStatus } from '../api/setup.js'

/**
 * Application-wide mode, read from the anonymous `GET /api/setup/status` endpoint so it works
 * before any session exists. Singleton, same approach as `useSettings` / `useAuth` (no Pinia).
 *
 * The defaults are deliberately optimistic: if the probe fails (for instance the API is down) the
 * app must not lock itself behind the setup screen. A successful probe always overwrites them.
 */
const state = reactive({
  initialized: true,
  multiUserEnabled: true,
  requiresToken: false,
  loaded: false,
  error: null,
})

function apply(status) {
  state.initialized = status?.initialized === true
  state.multiUserEnabled = status?.multiUserEnabled === true
  state.requiresToken = status?.requiresToken === true
  state.error = null
  state.loaded = true
}

/**
 * Resolves the mode. Called once during bootstrap, before the router guard can run, and again
 * after the setup form succeeds so the freshly stored state is picked up.
 */
async function refresh() {
  try {
    apply(await getSetupStatus())
  } catch (e) {
    state.error = e.message
    state.loaded = true
  }
  return state
}

export function useAppMode() {
  return {
    state,
    refresh,
    /** Not configured yet: every route is forced to /setup. */
    get needsSetup() {
      return !state.initialized
    },
    /** Configured without accounts: no login, every request runs as the technical admin. */
    get isSingleUser() {
      return state.initialized && !state.multiUserEnabled
    },
    get initialized() {
      return state.initialized
    },
    get multiUserEnabled() {
      return state.multiUserEnabled
    },
  }
}
