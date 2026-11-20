// Module-level user and calendar state (D-T7: built-in composables, no Pinia).
import { computed, readonly, ref } from 'vue'
import { api } from '../api/client'
import type { CalendarPreference, Me, UpdatePreferencesRequest } from '../api/types'

const user = ref<Me | null>(null)
let loading: Promise<Me> | null = null

/** GET /api/me: also issues the XSRF-TOKEN cookie, so it runs right after login (design §2.5). */
async function load(): Promise<Me> {
  loading ??= api
    .get<Me>('/api/me')
    .then((me) => {
      user.value = me
      return me
    })
    .finally(() => {
      loading = null
    })
  return loading
}

async function ensureLoaded(): Promise<Me> {
  return user.value ?? load()
}

async function setCalendar(calendar: CalendarPreference): Promise<void> {
  const body: UpdatePreferencesRequest = { calendar }
  user.value = await api.patch<Me>('/api/me/preferences', body)
}

async function logout(): Promise<void> {
  await api.post<void>('/api/auth/logout')
  user.value = null
}

function reset(): void {
  user.value = null
}

export function useCurrentUser() {
  return {
    user: readonly(user),
    /** The user's calendar; ROC until /api/me has answered (G6 default). */
    calendar: computed<CalendarPreference>(() => user.value?.calendar ?? 'ROC'),
    load,
    ensureLoaded,
    setCalendar,
    logout,
    reset,
  }
}
