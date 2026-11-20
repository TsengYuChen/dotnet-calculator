<script setup lang="ts">
// Calendar preference (G6, CUJ 11): stored on the server, so it follows the user to other devices.
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import { ApiError } from '../api/client'
import { errorMessage } from '../api/errors'
import type { CalendarPreference } from '../api/types'
import { useCurrentUser } from '../composables/useCurrentUser'
import { formatDate } from '../dates/calendar'

const { user, calendar, setCalendar } = useCurrentUser()
const saving = ref(false)
const example = '2026-11-19'

async function change(value: string | number | boolean | undefined) {
  const next = value as CalendarPreference
  if (next === calendar.value) return
  saving.value = true
  try {
    await setCalendar(next)
    ElMessage.success('已儲存設定')
  } catch (error) {
    if (!(error instanceof ApiError && error.status === 401)) {
      ElMessage.error(errorMessage(error instanceof ApiError ? error.problem : null))
    }
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="page">
    <div class="page-header">
      <h1>設定</h1>
    </div>
    <el-card>
      <el-form label-position="top">
        <el-form-item label="帳號">
          <span>{{ user?.email }}</span>
        </el-form-item>
        <el-form-item label="日期顯示與輸入">
          <el-radio-group :model-value="calendar" :disabled="saving" @change="change">
            <el-radio value="ROC">民國年（例如 {{ formatDate(example, 'ROC') }}）</el-radio>
            <el-radio value="GREGORIAN">西元年（例如 {{ formatDate(example, 'GREGORIAN') }}）</el-radio>
          </el-radio-group>
        </el-form-item>
      </el-form>
    </el-card>
  </div>
</template>
