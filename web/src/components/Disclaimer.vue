<script setup lang="ts">
// The disclaimer of "The Problem": once above the list and once on the vehicle detail page (G5).
const text =
  '檢驗期間依您登錄的資料計算，僅供參考，請以行照、監理站及環保單位的正式通知為準。期限遇假日可能順延；標示「推算日期」的期間可能比實際早或晚。本系統不會提醒已過期或已完成的檢驗。本系統不支援電動大型重型機車、營業用車及貨車。'
</script>

<template>
  <el-alert class="disclaimer" type="info" :closable="false" show-icon :title="text" />
</template>

<style scoped>
.disclaimer {
  margin-bottom: 16px;
}
</style>
