import { createRouter, createWebHistory, type RouteLocationRaw } from 'vue-router'
import { ApiError } from './api/client'
import { useCurrentUser } from './composables/useCurrentUser'

declare module 'vue-router' {
  interface RouteMeta {
    /** Reachable without signing in. */
    public?: boolean
    title?: string
  }
}

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/vehicles' },
    { path: '/login', component: () => import('./pages/LoginPage.vue'), meta: { public: true, title: '登入' } },
    {
      path: '/not-allowed',
      component: () => import('./pages/NotAllowedPage.vue'),
      meta: { public: true, title: '此帳號尚未開放使用' },
    },
    { path: '/vehicles', component: () => import('./pages/VehicleListPage.vue'), meta: { title: '我的車輛' } },
    { path: '/vehicles/new', component: () => import('./pages/VehicleFormPage.vue'), meta: { title: '新增車輛' } },
    {
      path: '/vehicles/:id',
      component: () => import('./pages/VehicleFormPage.vue'),
      props: true,
      meta: { title: '車輛資料' },
    },
    { path: '/trash', component: () => import('./pages/TrashPage.vue'), meta: { title: '垃圾桶' } },
    { path: '/settings', component: () => import('./pages/SettingsPage.vue'), meta: { title: '設定' } },
    { path: '/:pathMatch(.*)*', redirect: '/vehicles' },
  ],
})

export function loginLocation(returnUrl: string): RouteLocationRaw {
  return { path: '/login', query: returnUrl && returnUrl !== '/' ? { returnUrl } : {} }
}

router.beforeEach(async (to) => {
  if (to.meta.public) return true
  try {
    await useCurrentUser().ensureLoaded()
    return true
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return loginLocation(to.fullPath)
    throw error
  }
})

router.afterEach((to) => {
  document.title = to.meta.title ? `${to.meta.title}｜車輛檢驗期間` : '車輛檢驗期間'
})

/** 401 from any API call (e.g. removed from ALLOWED_EMAILS, CUJ 10): back to the login page. */
export function redirectToLogin(): void {
  useCurrentUser().reset()
  const current = router.currentRoute.value
  // During the initial navigation the guard handles the 401 itself.
  if (current.matched.length === 0 || current.meta.public) return
  void router.replace(loginLocation(current.fullPath))
}
