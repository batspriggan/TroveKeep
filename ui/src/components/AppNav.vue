<template>
  <nav>
    <span class="brand">TroveKeep</span>
    <RouterLink to="/sets">Sets</RouterLink>
    <RouterLink to="/bulkpieces">Bulk Pieces</RouterLink>
    <RouterLink to="/boxes">Boxes</RouterLink>
    <RouterLink to="/drawercontainers">Drawer Containers</RouterLink>
    <RouterLink to="/search">Search</RouterLink>
    <RouterLink to="/archives">Archives</RouterLink>
  </nav>
</template>

<style scoped>
nav {
  background: #1e293b;
  color: #e2e8f0;
  display: flex;
  align-items: center;
  gap: 1.5rem;
  padding: 0 1.5rem;
  height: 52px;
}

.brand {
  font-weight: 700;
  font-size: 1.1rem;
  color: #f8fafc;
  margin-right: 1rem;
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
</style>
