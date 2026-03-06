import { createRouter, createWebHistory } from 'vue-router'
import { useSettings } from '../composables/useSettings.js'

import SetList from '../views/sets/SetList.vue'
import SetDetail from '../views/sets/SetDetail.vue'
import BulkPieceList from '../views/bulkpieces/BulkPieceList.vue'
import BulkPieceDetail from '../views/bulkpieces/BulkPieceDetail.vue'
import BoxList from '../views/boxes/BoxList.vue'
import BoxDetail from '../views/boxes/BoxDetail.vue'
import DrawerContainerList from '../views/drawercontainers/DrawerContainerList.vue'
import DrawerContainerDetail from '../views/drawercontainers/DrawerContainerDetail.vue'
import DrawerDetail from '../views/drawers/DrawerDetail.vue'
import SearchView from '../views/SearchView.vue'
import ArchivesView from '../views/ArchivesView.vue'
import SettingsView from '../views/SettingsView.vue'
import TablePlannerView from '../views/TablePlannerView.vue'
import RoomPlannerView from '../views/tableplanner/RoomPlannerView.vue'

const routes = [
  { path: '/', redirect: '/sets' },
  { path: '/sets', component: SetList },
  { path: '/sets/:id', component: SetDetail },
  { path: '/bulkpieces', component: BulkPieceList },
  { path: '/bulkpieces/:id', component: BulkPieceDetail },
  { path: '/boxes', component: BoxList },
  { path: '/boxes/:id', component: BoxDetail },
  { path: '/drawercontainers', component: DrawerContainerList },
  { path: '/drawercontainers/:id', component: DrawerContainerDetail },
  { path: '/drawers/:containerId/:position', component: DrawerDetail },
  { path: '/search', component: SearchView },
  { path: '/archives', component: ArchivesView },
  { path: '/settings', component: SettingsView },
  { path: '/table-planner', component: TablePlannerView },
  { path: '/table-planner/rooms/:id', component: RoomPlannerView },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.beforeEach((to) => {
  if (to.path.startsWith('/table-planner')) {
    const settings = useSettings()
    if (!settings.tablePlannerEnabled) return '/'
  }
})

export default router
