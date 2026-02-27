import { get } from './client.js'

export function search(q) {
  return get(`/api/search?q=${encodeURIComponent(q)}`)
}
