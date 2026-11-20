<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()

const returnUrl = computed(() => {
  const value = route.query.returnUrl
  // The server validates it again (site-relative only); this only avoids sending obvious junk.
  return typeof value === 'string' && value.startsWith('/') && !value.startsWith('//') ? value : '/vehicles'
})
const signInFailed = computed(() => route.query.error === 'signin')

function signIn() {
  window.location.href = `/api/auth/login?returnUrl=${encodeURIComponent(returnUrl.value)}`
}
</script>

<template>
  <div class="page login">
    <el-card class="card">
      <h1>車輛檢驗期間</h1>
      <p class="muted">登錄您的汽機車，查看每台車接下來的定期檢驗與排氣定檢期間。</p>
      <el-alert
        v-if="signInFailed"
        type="error"
        :closable="false"
        show-icon
        title="登入失敗，請再試一次"
        class="error"
      />
      <el-button type="primary" size="large" @click="signIn">用 Google 登入</el-button>
    </el-card>
  </div>
</template>

<style scoped>
.login {
  display: flex;
  justify-content: center;
  padding-top: 80px;
}

.card {
  max-width: 420px;
  width: 100%;
  text-align: center;
}

.error {
  margin-bottom: 16px;
}
</style>
