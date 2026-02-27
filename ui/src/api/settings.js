export async function downloadBackup() {
  const res = await fetch('/api/settings/backup')
  if (!res.ok) throw new Error(`Server error: ${res.status}`)

  const blob = await res.blob()
  const contentDisposition = res.headers.get('Content-Disposition')
  let fileName = 'trovekeep-backup.json'
  if (contentDisposition) {
    const match = contentDisposition.match(/filename[^;=\n]*=\s*["']?([^"';\n]+)["']?/)
    if (match) fileName = match[1].trim()
  }

  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  a.click()
  URL.revokeObjectURL(url)
}

export async function uploadRestore(file) {
  const form = new FormData()
  form.append('file', file)
  const res = await fetch('/api/settings/restore', { method: 'POST', body: form })
  if (!res.ok) {
    let msg = `Server error: ${res.status}`
    try {
      const json = await res.json()
      if (json.error) msg = json.error
    } catch {
      // non-JSON error body
    }
    throw new Error(msg)
  }
}
