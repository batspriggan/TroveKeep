<template>
  <AppNav />
  <main>
    <RouterView />
  </main>
</template>

<script setup>
import AppNav from './components/AppNav.vue'
</script>

<style>
* {
  box-sizing: border-box;
  margin: 0;
  padding: 0;
}

body {
  font-family: system-ui, sans-serif;
  background: #f5f5f5;
  color: #222;
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

input, select, textarea {
  padding: 0.4rem 0.6rem;
  border: 1px solid #ccc;
  border-radius: 4px;
  font-size: 0.875rem;
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
</style>
