<!-- Redesigned: 2026-03-06 — Sticky bottom navigation bar for mobile -->
<template>
  <nav class="bottom-nav" aria-label="Main navigation">
    <RouterLink to="/sets" class="nav-item">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
        <rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/>
        <rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/>
      </svg>
      <span>Sets</span>
    </RouterLink>

    <RouterLink v-if="settings.bulkPiecesEnabled" to="/bulkpieces" class="nav-item">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
        <circle cx="8" cy="8" r="2.5"/><circle cx="16" cy="8" r="2.5"/>
        <circle cx="8" cy="16" r="2.5"/><circle cx="16" cy="16" r="2.5"/>
      </svg>
      <span>Parts</span>
    </RouterLink>

    <RouterLink to="/boxes" class="nav-item">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
        <path d="M21 8H3M21 8v11a1 1 0 01-1 1H4a1 1 0 01-1-1V8M21 8l-2-5H5L3 8M9 12h6"/>
      </svg>
      <span>Boxes</span>
    </RouterLink>

    <RouterLink v-if="settings.bulkPiecesEnabled" to="/drawercontainers" class="nav-item">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
        <path d="M12 3L2 8l10 5 10-5-10-5zM2 13l10 5 10-5M2 18l10 5 10-5"/>
      </svg>
      <span>Drawers</span>
    </RouterLink>

    <RouterLink to="/search" class="nav-item">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
        <circle cx="11" cy="11" r="7"/><path d="M21 21l-4.35-4.35"/>
      </svg>
      <span>Search</span>
    </RouterLink>
  </nav>
</template>

<script setup>
import { useSettings } from '../composables/useSettings.js'

const settings = useSettings()
</script>

<style scoped>
.bottom-nav {
  display: none;
}

@media (max-width: 640px) {
  .bottom-nav {
    display: flex;
    position: fixed;
    bottom: 0;
    left: 0;
    right: 0;
    height: var(--bottom-nav-height);
    background: var(--color-surface);
    border-top: 1px solid var(--color-border);
    z-index: 100;
    justify-content: space-around;
    align-items: stretch;
    padding: 0 var(--space-2);
    /* safe area for notched phones */
    padding-bottom: env(safe-area-inset-bottom, 0);
  }

  .nav-item {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 3px;
    flex: 1;
    min-height: 44px;
    color: var(--color-text-muted);
    text-decoration: none;
    font-size: var(--text-xs);
    font-family: var(--font-display);
    font-weight: 500;
    transition: color var(--transition-fast);
    padding: var(--space-1) 0;
  }

  .nav-item svg {
    width: 22px;
    height: 22px;
    flex-shrink: 0;
  }

  .nav-item:hover,
  .nav-item.router-link-active {
    color: var(--color-accent);
    text-decoration: none;
  }
}
</style>
