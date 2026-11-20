<script setup lang="ts">
// Trash (G3): restore or delete permanently; 「X 後自動移除」 uses purgeAt as a Taipei date.
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ApiError, api } from '../api/client'
import { errorMessage } from '../api/errors'
import type { TrashItem } from '../api/types'
import { useCurrentUser } from '../composables/useCurrentUser'
import { formatDate, taipeiDateOf } from '../dates/calendar'
import { VEHICLE_TYPE_LABELS } from '../labels'

const { calendar } = useCurrentUser()
const items = ref<TrashItem[]>([])
const loading = ref(false)
const loadError = ref<string | null>(null)
const busyId = ref<string | null>(null)

const taipeiDate = (instant: string) => formatDate(taipeiDateOf(instant), calendar.value)

function showError(error: unknown) {
  if (error instanceof ApiError && error.status === 401) return
  ElMessage.error(errorMessage(error instanceof ApiError ? error.problem : null))
}

async function load() {
  loading.value = true
  loadError.value = null
  try {
    items.value = await api.get<TrashItem[]>('/api/trash')
  } catch (error) {
    if (!(error instanceof ApiError && error.status === 401)) {
      loadError.value = errorMessage(error instanceof ApiError ? error.problem : null)
    }
  } finally {
    loading.value = false
  }
}

async function restore(item: TrashItem) {
  busyId.value = item.id
  try {
    await api.post<void>(`/api/vehicles/${encodeURIComponent(item.id)}/restore`)
    ElMessage.success(`已還原 ${item.plate}`)
    await load()
  } catch (error) {
    showError(error)
  } finally {
    busyId.value = null
  }
}

async function deletePermanently(item: TrashItem) {
  try {
    await ElMessageBox.confirm(`永久刪除 ${item.plate} 後無法復原，確定要刪除嗎？`, '永久刪除', {
      confirmButtonText: '永久刪除',
      cancelButtonText: '取消',
      type: 'warning',
      confirmButtonClass: 'el-button--danger',
    })
  } catch {
    return
  }
  busyId.value = item.id
  try {
    await api.delete<void>(`/api/vehicles/${encodeURIComponent(item.id)}`)
    ElMessage.success(`已永久刪除 ${item.plate}`)
    await load()
  } catch (error) {
    showError(error)
  } finally {
    busyId.value = null
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <div class="page-header">
      <h1>垃圾桶</h1>
    </div>
    <p class="muted">丟進垃圾桶滿 30 天的車輛會自動移除。</p>

    <el-alert v-if="loadError" type="error" :closable="false" show-icon :title="loadError" />

    <div v-loading="loading">
      <el-empty v-if="!loading && !loadError && items.length === 0" description="垃圾桶是空的" />
      <el-card v-for="item in items" :key="item.id" class="item">
        <div class="row">
          <div class="info">
            <span class="plate">{{ item.plate }}</span>
            <span v-if="item.nickname">{{ item.nickname }}</span>
            <span class="muted">{{ item.brand }} {{ item.model }}</span>
            <span class="muted">{{ VEHICLE_TYPE_LABELS[item.vehicleType] }}</span>
            <span class="muted">{{ taipeiDate(item.deletedAt) }} 丟進垃圾桶</span>
            <span class="purge">{{ taipeiDate(item.purgeAt) }} 後自動移除</span>
          </div>
          <div class="buttons">
            <el-button type="primary" :loading="busyId === item.id" @click="restore(item)">還原</el-button>
            <el-button type="danger" plain :disabled="busyId === item.id" @click="deletePermanently(item)">
              永久刪除
            </el-button>
          </div>
        </div>
      </el-card>
    </div>
  </div>
</template>

<style scoped>
.item {
  margin-bottom: 12px;
}

.row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.info {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 12px;
  align-items: baseline;
}

.plate {
  font-weight: 700;
}

.purge {
  color: var(--vg-highlight);
}

.buttons {
  display: flex;
  gap: 8px;
}
</style>
