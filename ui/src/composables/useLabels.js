import { reactive, readonly } from 'vue'
import { getLabelConfig, printLabel, downloadLabelByKind } from '../api/labels.js'

/**
 * Centralizes the two label sinks:
 *  - client: triggers a browser download (user manages the files);
 *  - server: posts the label to the remote label-tool server, which prints it.
 * The mode is a backend setting, so it is stored on the server and cached here.
 */
const state = reactive({
  loaded: false,
  mode: 'client',
  serverUrl: null,
  hasServerToken: false,
})

async function ensureConfig(force = false) {
  if (state.loaded && !force) return
  try {
    const cfg = await getLabelConfig()
    state.mode = cfg.mode
    state.serverUrl = cfg.serverUrl
    state.hasServerToken = cfg.hasServerToken
    state.loaded = true
  } catch {
    // Backend unreadable: fall back to client mode so the UI still works.
    state.mode = 'client'
    state.loaded = true
  }
}

/**
 * Prints (server) or downloads (client) a label, returning a message for the UI.
 * @returns {Promise<{ message: string, error?: string }>}
 */
async function printOrDownload(kind, id, { copies = null, size = null } = {}) {
  await ensureConfig()

  if (state.mode !== 'server') {
    downloadLabelByKind(kind, id)
    return { message: 'Label downloading — save it to the label-tool watch folder.' }
  }

  const outcome = await printLabel(kind, id, { copies, size })
  const parts = []
  if (outcome.sent) parts.push(`${outcome.sent} in coda`)
  if (outcome.rejected) parts.push(`${outcome.rejected} rifiutate`)
  if (outcome.uncertain) parts.push(`${outcome.uncertain} con esito incerto`)

  let message = parts.length ? `Stampa: ${parts.join(', ')}.` : 'Stampa: nessuna etichetta inviata.'
  if (outcome.message) message += ` ${outcome.message}`

  const firstError = outcome.items?.find((i) => i.error)?.error
  return { message, error: firstError ? `Errore: ${firstError}` : undefined }
}

export function useLabels() {
  return {
    config: readonly(state),
    ensureConfig,
    printOrDownload,
  }
}
