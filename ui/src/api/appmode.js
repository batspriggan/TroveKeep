import { put } from './client.js'

/**
 * Post-setup application-mode switch (`PUT /api/admin/mode`). Admin-only; unlike the first-run
 * setup it does not need the setup token. `useAppMode().refresh()` picks the new state up.
 */
export const setMultiUserMode = (multiUserEnabled) =>
  put('/api/admin/mode', { multiUserEnabled })
