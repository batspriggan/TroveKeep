<template>
  <div>
    <h1>Search</h1>

    <form class="form-row" @submit.prevent="submit">
      <div class="form-field">
        <label>Set number, Lego ID, or description</label>
        <input v-model="query" placeholder="e.g. 75313 or Brick 2x4" />
      </div>
      <button class="primary" type="submit">Search</button>
    </form>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-if="loading">Searching…</p>

    <template v-else-if="searched">
      <!-- Sets -->
      <section style="margin-bottom: 2rem;">
        <h2>Sets <span class="count">({{ results.sets.length }})</span></h2>
        <table v-if="results.sets.length">
          <thead>
            <tr>
              <th>Set Number</th>
              <th>Description</th>
              <th>Qty</th>
              <th>Stored in</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="s in results.sets" :key="s.id">
              <td><RouterLink :to="`/sets/${s.id}`">{{ s.setNumber }}</RouterLink></td>
              <td>{{ s.description }}</td>
              <td>{{ s.quantity }}</td>
              <td>
                <span v-if="!s.allocations.length">—</span>
                <ul v-else class="alloc-list">
                  <li v-for="a in s.allocations" :key="a.storageId">
                    <RouterLink :to="`/boxes/${a.storageId}`">{{ a.storageName }}</RouterLink>
                    <span class="qty-badge">× {{ a.quantity }}</span>
                  </li>
                </ul>
              </td>
            </tr>
          </tbody>
        </table>
        <p v-else class="muted">No matching sets.</p>
      </section>

      <!-- Bulk Pieces -->
      <section>
        <h2>Bulk Pieces <span class="count">({{ results.pieces.length }})</span></h2>
        <table v-if="results.pieces.length">
          <thead>
            <tr>
              <th>Lego ID</th>
              <th>Color</th>
              <th>Description</th>
              <th>Qty</th>
              <th>Stored in</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="p in results.pieces" :key="p.id">
              <td><RouterLink :to="`/bulkpieces/${p.id}`">{{ p.legoId }}</RouterLink></td>
              <td>{{ p.legoColor }}</td>
              <td>{{ p.description }}</td>
              <td>{{ p.quantity }}</td>
              <td>
                <span v-if="!p.allocations.length">—</span>
                <ul v-else class="alloc-list">
                  <li v-for="a in p.allocations" :key="a.storageId">
                    <RouterLink
                      :to="a.storageType === 'Box' ? `/boxes/${a.storageId}` : `/drawers/${a.storageId}`"
                    >
                      <template v-if="a.storageType === 'Drawer' && a.drawerContainerName">
                        {{ a.drawerContainerName }} › {{ a.storageName }}
                      </template>
                      <template v-else>{{ a.storageName }}</template>
                    </RouterLink>
                    <span class="qty-badge">× {{ a.quantity }}</span>
                  </li>
                </ul>
              </td>
            </tr>
          </tbody>
        </table>
        <p v-else class="muted">No matching bulk pieces.</p>
      </section>
    </template>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { search } from '../api/search.js'

const query = ref('')
const loading = ref(false)
const error = ref('')
const searched = ref(false)
const results = ref({ sets: [], pieces: [] })

async function submit() {
  if (!query.value.trim()) return
  loading.value = true
  error.value = ''
  searched.value = false
  try {
    results.value = await search(query.value.trim())
    searched.value = true
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.count {
  font-weight: 400;
  color: #64748b;
  font-size: 0.9rem;
}

.muted {
  color: #64748b;
  font-size: 0.875rem;
  margin-top: 0.5rem;
}

.alloc-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.qty-badge {
  margin-left: 0.4rem;
  color: #64748b;
  font-size: 0.8rem;
}
</style>
