<template>
  <AppNav />
  <main>
    <RouterView />
  </main>
  <BottomNav />
</template>

<script setup>
import AppNav from './components/AppNav.vue'
import BottomNav from './components/BottomNav.vue'
</script>

<style>
@import url('https://fonts.googleapis.com/css2?family=DM+Mono:wght@400;500&family=Sora:wght@400;500;600;700&display=swap');

:root {
  --color-bg:           #F7F6F3;
  --color-surface:      #FFFFFF;
  --color-surface-alt:  #F0EEE9;
  --color-border:       #E2DED8;

  --color-text-primary:   #1A1917;
  --color-text-secondary: #6B6760;
  --color-text-muted:     #A8A39C;

  --color-accent:      #E3000B;
  --color-accent-soft: #FFF0F0;

  --color-success: #2D6A4F;
  --color-warning: #F4A015;
  --color-info:    #2563EB;

  --font-display: 'Sora', sans-serif;
  --font-mono:    'DM Mono', monospace;
  --font-body:    'Sora', sans-serif;

  --text-xs:   11px;
  --text-sm:   13px;
  --text-base: 15px;
  --text-lg:   17px;
  --text-xl:   20px;
  --text-2xl:  26px;

  --space-1: 4px;
  --space-2: 8px;
  --space-3: 12px;
  --space-4: 16px;
  --space-6: 24px;
  --space-8: 32px;

  --transition-fast: 120ms ease;
  --transition-base: 200ms ease;
  --transition-slow: 300ms ease-in-out;

  --bottom-nav-height: 56px;
}

* {
  box-sizing: border-box;
  margin: 0;
  padding: 0;
}

body {
  font-family: var(--font-body);
  background: var(--color-bg);
  color: var(--color-text-primary);
}

main {
  max-width: 1100px;
  margin: 0 auto;
  padding: 1.5rem 1rem;
}

h1 { font-size: 1.6rem; margin-bottom: 1rem; }
h2 { font-size: 1.25rem; margin-bottom: 0.75rem; }

table {
  width: 100%;
  border-collapse: collapse;
  background: #fff;
  border-radius: 6px;
  overflow: hidden;
  box-shadow: 0 1px 3px rgba(0,0,0,.08);
}

th, td {
  text-align: left;
  padding: 0.6rem 0.9rem;
  border-bottom: 1px solid #eee;
}

th { background: #f0f0f0; font-weight: 600; }

tr:last-child td { border-bottom: none; }

button {
  cursor: pointer;
  padding: 0.35rem 0.8rem;
  border: 1px solid #ccc;
  border-radius: 4px;
  background: #fff;
  font-size: 0.875rem;
}

button.primary {
  background: #2563eb;
  color: #fff;
  border-color: #2563eb;
}

button.danger {
  background: #dc2626;
  color: #fff;
  border-color: #dc2626;
}

button:hover { opacity: 0.85; }

input, select, textarea, button {
  font-family: inherit;
  font-size: 0.875rem;
}

input, select, textarea {
  padding: 0.4rem 0.6rem;
  border: 1px solid #ccc;
  border-radius: 4px;
  width: 100%;
}

.form-row {
  display: flex;
  gap: 0.5rem;
  align-items: flex-end;
  flex-wrap: wrap;
  margin-bottom: 1rem;
}

.form-row label { display: block; font-size: 0.8rem; margin-bottom: 0.2rem; }

.form-field { flex: 1; min-width: 140px; }

.error { color: #dc2626; margin: 0.5rem 0; font-size: 0.875rem; }

.card {
  background: #fff;
  border-radius: 6px;
  box-shadow: 0 1px 3px rgba(0,0,0,.08);
  padding: 1.25rem;
  margin-bottom: 1rem;
}

a { color: #2563eb; text-decoration: none; }
a:hover { text-decoration: underline; }

.back-link { display: inline-block; margin-bottom: 1rem; font-size: 0.875rem; }

.actions { display: flex; gap: 0.5rem; }

@media (max-width: 640px) {
  main {
    padding: 1rem 0.75rem;
    padding-bottom: calc(var(--bottom-nav-height) + 1rem);
  }

  table { display: block; overflow-x: auto; -webkit-overflow-scrolling: touch; }

  input[type="search"] { max-width: 100% !important; }

  button { min-height: 2.25rem; padding: 0.4rem 0.9rem; }
}
</style>
