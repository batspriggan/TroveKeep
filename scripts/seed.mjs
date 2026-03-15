#!/usr/bin/env node
/**
 * Seed script — creates boxes, drawer containers, sets, bulk pieces + allocations.
 * Usage: node scripts/seed.mjs [base_url]
 * Default base URL: http://localhost:5221
 */

const BASE = process.argv[2] ?? 'http://localhost:5221'

async function api(method, path, body) {
  const res = await fetch(`${BASE}${path}`, {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: body ? JSON.stringify(body) : undefined,
  })
  if (!res.ok) {
    const text = await res.text().catch(() => '')
    throw new Error(`${method} ${path} → ${res.status}: ${text}`)
  }
  if (res.status === 204) return null
  return res.json()
}

const post = (path, body) => api('POST', path, body)

function progress(label, i, total) {
  process.stdout.write(`\r  ${label}: ${i}/${total}`)
  if (i === total) process.stdout.write('\n')
}

// ── Lego theme names for realistic set descriptions ──────────────────────────
const THEMES = [
  'Star Wars', 'Technic', 'City', 'Creator', 'Ninjago', 'Harry Potter',
  'Marvel', 'DC', 'Architecture', 'Ideas', 'Friends', 'Speed Champions',
  'Icons', 'Botanical', 'Art', 'Monkie Kid', 'Minecraft', 'Jurassic World',
]
const PARTS = [
  'Millennium Falcon', 'Death Star', 'Eiffel Tower', 'Batmobile', 'Castle',
  'Space Station', 'Train', 'Fire Station', 'Police Station', 'Hospital',
  'Airport', 'Race Car', 'Bulldozer', 'Excavator', 'Crane', 'Lighthouse',
  'Pirate Ship', 'Dragon', 'Robot', 'Spaceship', 'Village', 'Market',
]

// ── Color IDs (Rebrickable) ──────────────────────────────────────────────────
const COLOR_IDS = [1, 5, 6, 7, 11, 14, 25, 26, 28, 70, 71, 72, 84, 85]
const COLOR_NAMES = ['White', 'Red', 'Bright Blue', 'Blue', 'Black', 'Yellow',
  'Orange', 'Bright Green', 'Dark Green', 'Reddish Brown', 'Light Bluish Gray',
  'Dark Bluish Gray', 'Dark Orange', 'Lavender']

// ── Part IDs for bulk pieces ─────────────────────────────────────────────────
const PART_IDS = [
  '3001', '3002', '3003', '3004', '3005', '3006', '3007', '3008', '3009', '3010',
  '3020', '3021', '3022', '3023', '3024', '3032', '3033', '3034', '3035', '3036',
  '3037', '3038', '3039', '3040', '3045', '3046', '3048', '3049', '3062b', '3068b',
  '4150', '4162', '4865b', '6111', '6112', '6141', '6222', '6636', '11211', '11212',
  '15068', '15573', '18654', '22388', '24246', '28653', '30136', '32028', '32523', '32524',
  '3673', '2780', '3713', '3706', '3707', '3708', '3747a', '3829c01', '57585', '99563',
]

function pick(arr) { return arr[Math.floor(Math.random() * arr.length)] }
function rand(min, max) { return Math.floor(Math.random() * (max - min + 1)) + min }
function setNum(i) { return `${rand(1000, 99999)}-${rand(1, 3)}` }

// ─────────────────────────────────────────────────────────────────────────────
async function main() {
  console.log(`Seeding against ${BASE}\n`)

  // 1. Boxes ──────────────────────────────────────────────────────────────────
  console.log('Creating boxes…')
  const boxNames = ['Box A — Small Parts', 'Box B — Minifigs', 'Box C — Technic',
    'Box D — Large Bricks', 'Box E — Plates', 'Box F — Special Elements']
  const boxes = []
  for (let i = 0; i < boxNames.length; i++) {
    const b = await post('/api/boxes', { name: boxNames[i] })
    boxes.push(b)
    progress('boxes', i + 1, boxNames.length)
  }

  // 2. Drawer containers ──────────────────────────────────────────────────────
  console.log('Creating drawer containers…')
  const containerDefs = [
    { name: 'Cabinet Alpha', description: 'Main sorting cabinet', drawers: 12 },
    { name: 'Cabinet Beta',  description: 'Overflow cabinet',     drawers: 10 },
    { name: 'Cabinet Gamma', description: 'Technic-only cabinet', drawers: 8  },
  ]
  const containers = []
  for (let i = 0; i < containerDefs.length; i++) {
    const def = containerDefs[i]
    const c = await post('/api/drawercontainers', {
      name: def.name,
      description: def.description,
      drawerCount: def.drawers,
    })
    containers.push(c)
    progress('containers', i + 1, containerDefs.length)
  }

  // 3. Sets (120) ─────────────────────────────────────────────────────────────
  console.log('Creating sets…')
  const TOTAL_SETS = 120
  const sets = []
  for (let i = 0; i < TOTAL_SETS; i++) {
    const isMoc = Math.random() < 0.15
    const theme = pick(THEMES)
    const part  = pick(PARTS)
    const s = await post('/api/sets', {
      setNumber: isMoc ? `MOC-${String(i + 1).padStart(3, '0')}` : setNum(i),
      description: isMoc ? `${theme} — Custom ${part}` : `${theme} ${part}`,
      quantity: rand(1, 3),
      isMoc,
      photoUrl: null,
    })
    sets.push(s)
    progress('sets', i + 1, TOTAL_SETS)
  }

  // 4. Bulk pieces (120) ──────────────────────────────────────────────────────
  console.log('Creating bulk pieces…')
  const TOTAL_PIECES = 120
  const pieces = []
  for (let i = 0; i < TOTAL_PIECES; i++) {
    const colorIdx = i % COLOR_IDS.length
    const partId   = PART_IDS[i % PART_IDS.length]
    const p = await post('/api/bulkpieces', {
      legoId: partId,
      legoColorId: COLOR_IDS[colorIdx],
      description: `${COLOR_NAMES[colorIdx]} ${partId}`,
      quantity: rand(10, 500),
    })
    pieces.push(p)
    progress('pieces', i + 1, TOTAL_PIECES)
  }

  // 5. Allocate sets to boxes ─────────────────────────────────────────────────
  console.log('Allocating sets to boxes…')
  let allocated = 0
  for (const s of sets) {
    if (Math.random() < 0.6) {   // 60% of sets get stored
      const box = pick(boxes)
      const qty = rand(1, s.quantity)
      try {
        await post(`/api/sets/${s.id}/storage/box/${box.id}`, { quantity: qty })
      } catch { /* skip if over-quota */ }
      allocated++
    }
    progress('set allocs', allocated, Math.round(TOTAL_SETS * 0.6))
  }
  process.stdout.write('\n')

  // 6. Allocate pieces to boxes and drawers ───────────────────────────────────
  console.log('Allocating pieces to boxes/drawers…')
  allocated = 0
  for (const p of pieces) {
    if (Math.random() < 0.7) {   // 70% of pieces get stored
      const useDrawer = Math.random() < 0.5 && containers.length > 0
      try {
        if (useDrawer) {
          const container = pick(containers)
          const maxPos = container.drawers?.length ?? containerDefs.find(d => d.name === container.name)?.drawers ?? 10
          const position = rand(1, maxPos)
          const qty = rand(1, Math.min(50, p.quantity))
          await post(`/api/bulkpieces/${p.id}/storage/drawer/${container.id}/${position}`, { quantity: qty })
        } else {
          const box = pick(boxes)
          const qty = rand(1, Math.min(50, p.quantity))
          await post(`/api/bulkpieces/${p.id}/storage/box/${box.id}`, { quantity: qty })
        }
      } catch { /* skip on conflict */ }
      allocated++
    }
    progress('piece allocs', allocated, Math.round(TOTAL_PIECES * 0.7))
  }
  process.stdout.write('\n')

  console.log('\nDone!')
  console.log(`  ${boxes.length} boxes`)
  console.log(`  ${containers.length} drawer containers`)
  console.log(`  ${sets.length} sets`)
  console.log(`  ${pieces.length} bulk pieces`)
}

main().catch(e => { console.error('\nError:', e.message); process.exit(1) })
