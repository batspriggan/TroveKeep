<template>
  <nav :class="{ open }">
    <span class="brand">TroveKeep</span>
    <button class="hamburger" @click="open = !open" aria-label="Toggle menu">☰</button>
    <div class="nav-links" :class="{ open }">
      <RouterLink to="/sets">Sets</RouterLink>
      <RouterLink to="/bulkpieces">Bulk Pieces</RouterLink>
      <RouterLink to="/boxes">Boxes</RouterLink>
      <RouterLink to="/drawercontainers">Drawer Containers</RouterLink>
      <RouterLink to="/search">Search</RouterLink>
      <RouterLink to="/archives">Archives</RouterLink>
      <RouterLink to="/settings">Settings</RouterLink>
      <RouterLink to="/table-planner" class="mobile-hide">Table Planner</RouterLink>
    </div>
  </nav>
</template>

<script setup>
import { ref, watch } from 'vue'
import { useRoute } from 'vue-router'

const open = ref(false)
const route = useRoute()

watch(route, () => { open.value = false })
</script>

<style scoped>
nav {
  background: #1e293b;
  color: #e2e8f0;
  display: flex;
  align-items: center;
  gap: 1.5rem;
  padding: 0 1.5rem;
  height: 52px;
  flex-wrap: wrap;
}

.brand {
  font-weight: 700;
  font-size: 1.1rem;
  color: #f8fafc;
  margin-right: 1rem;
}

.hamburger {
  display: none;
  background: none;
  border: none;
  color: #e2e8f0;
  font-size: 1.4rem;
  cursor: pointer;
  padding: 0.25rem 0.5rem;
  margin-left: auto;
  min-height: unset;
}

.nav-links {
  display: flex;
  gap: 1.5rem;
  align-items: center;
}

a {
  color: #94a3b8;
  text-decoration: none;
  font-size: 0.9rem;
  padding: 0.3rem 0;
}

a:hover,
a.router-link-active {
  color: #f8fafc;
}

a.router-link-active {
  border-bottom: 2px solid #3b82f6;
}

@media (max-width: 640px) {
  nav {
    height: auto;
    padding: 0 1rem;
    min-height: 52px;
  }

  .brand {
    line-height: 52px;
  }

  .hamburger {
    display: block;
  }

  .nav-links {
    display: none;
    width: 100%;
    flex-direction: column;
    align-items: flex-start;
    gap: 0;
    padding: 0.5rem 0 1rem;
  }

  .nav-links.open {
    display: flex;
  }

  .nav-links a {
    width: 100%;
    padding: 0.6rem 0;
    border-bottom: 1px solid #334155;
    font-size: 1rem;
  }

  .nav-links a.router-link-active {
    border-bottom-color: #3b82f6;
  }

  .mobile-hide { display: none; }
}
</style>
