import { reactive, watch } from 'vue'

const STORAGE_KEY = 'trovekeep_settings'

function loadFromStorage() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (raw) return JSON.parse(raw)
  } catch {}
  return {}
}

const defaults = {
  tablePlannerEnabled: true,
}

const settings = reactive({ ...defaults, ...loadFromStorage() })

watch(settings, (val) => {
  localStorage.setItem(STORAGE_KEY, JSON.stringify({ ...val }))
})

export function useSettings() {
  return settings
}
