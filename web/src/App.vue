<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import zhTw from 'element-plus/es/locale/lang/zh-tw'
import { ElMessage } from 'element-plus'
import { useCurrentUser } from './composables/useCurrentUser'
import { ApiError } from './api/client'
import { errorMessage } from './api/errors'

const route = useRoute()
const router = useRouter()
const { user, logout } = useCurrentUser()

const showNav = computed(() => !route.meta.public && user.value !== null)

async function onLogout() {
  try {
    await logout()
    await router.replace('/login')
  } catch (error) {
    if (!(error instanceof ApiError && error.status === 401)) {
      ElMessage.error(errorMessage(error instanceof ApiError ? error.problem : null))
    }
  }
}
</script>

<template>
  <el-config-provider :locale="zhTw">
    <header v-if="showNav" class="nav">
      <nav class="nav-inner">
        <router-link to="/vehicles" class="brand">車輛檢驗期間</router-link>
        <router-link to="/vehicles">車輛</router-link>
        <router-link to="/trash">垃圾桶</router-link>
        <router-link to="/settings">設定</router-link>
        <span class="spacer" />
        <span class="muted email">{{ user?.email }}</span>
        <el-button link type="primary" @click="onLogout">登出</el-button>
      </nav>
    </header>
    <main>
      <router-view />
    </main>
  </el-config-provider>
</template>

<style scoped>
.nav {
  background: #fff;
  border-bottom: 1px solid #e5e7eb;
}

.nav-inner {
  max-width: 960px;
  margin: 0 auto;
  padding: 10px 16px;
  display: flex;
  align-items: center;
  gap: 16px;
  flex-wrap: wrap;
}

.nav-inner a {
  color: #374151;
  text-decoration: none;
}

.nav-inner a.router-link-active {
  color: var(--el-color-primary);
  font-weight: 600;
}

.brand {
  font-weight: 700;
}

.spacer {
  flex: 1;
}

.email {
  font-size: 0.875rem;
}
</style>
