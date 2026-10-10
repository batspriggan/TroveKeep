import { createRouter, createWebHistory } from 'vue-router'
import { useSettings } from '../composables/useSettings.js'
import { useAuth } from '../composables/useAuth.js'
import { useAppMode } from '../composables/useAppMode.js'

import LoginView from '../views/LoginView.vue'
import SetupView from '../views/SetupView.vue'

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
import ScannerView from '../views/ScannerView.vue'
import ArchivesView from '../views/ArchivesView.vue'
import SettingsView from '../views/SettingsView.vue'
import BaseplateListView from '../views/baseplates/BaseplateListView.vue'
import UserAdminView from '../views/admin/UserAdminView.vue'
import CollectionAdminView from '../views/admin/CollectionAdminView.vue'
import TablePlannerView from '../views/TablePlannerView.vue'
import RoomPlannerView from '../views/tableplanner/RoomPlannerView.vue'
import AggregatePlannerListView from '../views/tableplanner/AggregatePlannerListView.vue'
import AggregateBpPlannerView from '../views/tableplanner/AggregateBpPlannerView.vue'
import BuildCheckView from '../views/tableplanner/BuildCheckView.vue'

const routes = [
  { path: '/login', component: LoginView, meta: { public: true } },
  { path: '/setup', component: SetupView, meta: { public: true } },
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
  { path: '/scanner', component: ScannerView },
  { path: '/archives', component: ArchivesView },
  { path: '/settings', component: SettingsView },
  { path: '/admin/users', component: UserAdminView, meta: { admin: true } },
  { path: '/admin/collections', component: CollectionAdminView, meta: { admin: true } },
  { path: '/baseplates', component: BaseplateListView },
  { path: '/table-planner', component: TablePlannerView },
  { path: '/table-planner/rooms/:id', component: RoomPlannerView, meta: { fullscreen: true } },
  { path: '/table-planner/baseplates', component: AggregatePlannerListView },
  { path: '/table-planner/baseplates/:roomId/:repId', component: AggregateBpPlannerView, meta: { fullscreen: true } },
  { path: '/table-planner/build-check', component: BuildCheckView, meta: {} },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.beforeEach((to) => {
  const appMode = useAppMode()
  const auth = useAuth()
  const settings = useSettings()

  // First run: nothing is reachable until the instance is configured, setup included.
  if (appMode.needsSetup) {
    return to.path === '/setup' ? true : { path: '/setup' }
  }

  // /setup only makes sense while single-user (the way back to multi-user). An already
  // multi-user instance must not expose it.
  if (to.path === '/setup') {
    return appMode.multiUserEnabled ? '/' : true
  }

  const singleUser = appMode.isSingleUser

  // Authentication gate: everything except /login requires a session. Single-user has no session
  // to establish — the backend serves every request as the technical admin — so it is skipped.
  if (!singleUser && !to.meta.public && !auth.isAuthenticated) {
    return { path: '/login', query: to.fullPath === '/' ? {} : { redirect: to.fullPath } }
  }
  // An authenticated user has no business on the login page. In single-user login does not exist
  // at all, so /login always bounces home.
  if (to.meta.public && (auth.isAuthenticated || singleUser)) return '/'

  // Role gate (step 3/5 expands this): a Guest only reaches the Table Planner.
  if (auth.isGuest && !to.path.startsWith('/table-planner')) return '/table-planner'

  // Admin gate: the user/collection administration pages are off limits to everyone else.
  // Single-user runs as the technical admin, so it passes even before fetchMe settles.
  if (to.meta.admin && !auth.isAdmin && !singleUser) return '/'

  if (to.path.startsWith('/table-planner') && !settings.tablePlannerEnabled) return '/'
  if ((to.path.startsWith('/bulkpieces') || to.path.startsWith('/drawercontainers') || to.path.startsWith('/drawers')) && !settings.bulkPiecesEnabled) return '/'
})

export default router
