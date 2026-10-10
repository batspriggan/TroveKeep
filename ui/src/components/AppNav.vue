<template>
  <nav :class="{ open }">
    <span class="brand">TroveKeep <span class="version">{{ version }}</span></span>
    <button class="hamburger" @click="open = !open" aria-label="Toggle menu">☰</button>
    <div class="nav-links" :class="{ open }">
      <RouterLink v-if="!auth.isGuest" to="/sets" class="mobile-hide">Sets</RouterLink>
      <RouterLink v-if="!auth.isGuest && settings.bulkPiecesEnabled" to="/bulkpieces" class="mobile-hide">Bulk Pieces</RouterLink>
      <RouterLink v-if="!auth.isGuest" to="/boxes" class="mobile-hide">Boxes</RouterLink>
      <RouterLink v-if="!auth.isGuest && settings.bulkPiecesEnabled" to="/drawercontainers" class="mobile-hide">Drawer Containers</RouterLink>
      <RouterLink v-if="!auth.isGuest" to="/search" class="mobile-hide">Search</RouterLink>
      <RouterLink v-if="!auth.isGuest" to="/scanner" class="mobile-hide">Scanner</RouterLink>
      <RouterLink v-if="!auth.isGuest" to="/archives">Archives</RouterLink>
      <RouterLink v-if="!auth.isGuest" to="/baseplates">Baseplates</RouterLink>
      <RouterLink v-if="!auth.isGuest" to="/settings">Settings</RouterLink>
      <RouterLink v-if="auth.isAdmin" to="/admin/users" class="admin-link">Users</RouterLink>
      <RouterLink v-if="auth.isAdmin" to="/admin/collections" class="admin-link">Collections</RouterLink>
      <!-- A Guest has the Table Planner as their only destination, so the link stays visible
           on mobile too (the other mobile-hide links are hidden inside the hamburger). -->
      <RouterLink v-if="settings.tablePlannerEnabled" to="/table-planner" :class="{ 'mobile-hide': !auth.isGuest }">Table Planner</RouterLink>
      <span class="user-block">
        <!-- In single-user there is no session and no account to show: the badge alone explains
             that requests run as the technical administrator. -->
        <span v-if="appMode.isSingleUser" class="mode-badge">single-user</span>
        <template v-else>
          <span class="user-name">{{ auth.state.user?.displayName }}</span>
          <button class="logout" @click="onLogout" title="Logout">Esci</button>
        </template>
      </span>
    </div>
  </nav>
</template>

<script setup>
import { ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useSettings } from '../composables/useSettings.js'
import { useAuth } from '../composables/useAuth.js'
import { useAppMode } from '../composables/useAppMode.js'

const open = ref(false)
const route = useRoute()
const router = useRouter()
const settings = useSettings()
const auth = useAuth()
const appMode = useAppMode()
const version = import.meta.env.VITE_APP_VERSION

watch(route, () => { open.value = false })

async function onLogout() {
  await auth.logout()
  router.replace('/login')
}
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

.version {
  font-size: 0.65rem;
  font-weight: 400;
  color: #64748b;
  vertical-align: middle;
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
  /* Let the user block sit at the far right instead of hugging the last link. */
  flex: 1;
}

.user-block {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-left: auto;
}

.user-name {
  font-size: 0.8rem;
  color: #94a3b8;
}

.mode-badge {
  display: inline-block;
  margin-left: 0.35rem;
  padding: 0 0.35rem;
  border-radius: 3px;
  background: #334155;
  color: #cbd5e1;
  font-size: 0.65rem;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.logout {
  background: none;
  border: 1px solid #475569;
  color: #cbd5e1;
  border-radius: 4px;
  padding: 0.2rem 0.6rem;
  font-size: 0.8rem;
  min-height: unset;
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

  /* The menu is a vertical list here: keep the user block aligned like the other rows. */
  .user-block {
    margin-left: 0;
    padding-top: 0.6rem;
  }

  .mobile-hide { display: none; }
}
</style>
