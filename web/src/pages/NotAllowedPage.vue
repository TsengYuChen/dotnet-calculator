<script setup lang="ts">
// CUJ 2 / CUJ 10: shown after a sign-in that is not on the allowlist; no automatic redirect to Google.
</script>

<template>
  <div class="page not-allowed">
    <el-result icon="warning" title="此帳號尚未開放使用" sub-title="請確認您使用的 Google 帳號，或聯絡系統管理者。">
      <template #extra>
        <router-link to="/login">
          <el-button>回到登入頁</el-button>
        </router-link>
      </template>
    </el-result>
  </div>
</template>

<style scoped>
.not-allowed {
  padding-top: 60px;
}
</style>
