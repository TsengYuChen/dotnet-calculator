<script setup lang="ts">
// Vehicle list (G5): sorted by the API (`sort=deadline` default, or `manufactureDate`); disclaimer on top.
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ApiError, api } from '../api/client'
import { errorMessage } from '../api/errors'
import type { Vehicle, VehicleSort } from '../api/types'
import Disclaimer from '../components/Disclaimer.vue'
import InspectionBadge from '../components/InspectionBadge.vue'
import { useCurrentUser } from '../composables/useCurrentUser'
import { formatDate } from '../dates/calendar'
import { VEHICLE_TYPE_LABELS } from '../labels'

const route = useRoute()
const router = useRouter()
const { calendar } = useCurrentUser()

const vehicles = ref<Vehicle[]>([])
const loading = ref(false)
const loadError = ref<string | null>(null)

const sort = computed<VehicleSort>({
  get: () => (route.query.sort === 'manufactureDate' ? 'manufactureDate' : 'deadline'),
  set: (value) => void router.replace({ query: value === 'deadline' ? {} : { sort: value } }),
})

async function load() {
  loading.value = true
  loadError.value = null
  try {
    vehicles.value = await api.get<Vehicle[]>(`/api/vehicles?sort=${sort.value}`)
  } catch (error) {
    if (!(error instanceof ApiError && error.status === 401)) {
      loadError.value = errorMessage(error instanceof ApiError ? error.problem : null)
    }
  } finally {
    loading.value = false
  }
}

watch(sort, load, { immediate: true })
</script>

<template>
  <div class="page">
    <div class="page-header">
      <h1>我的車輛</h1>
      <div class="actions">
        <el-radio-group v-model="sort" size="small" aria-label="排序">
          <el-radio-button value="deadline">依最後期限</el-radio-button>
          <el-radio-button value="manufactureDate">依出廠日期</el-radio-button>
        </el-radio-group>
        <router-link to="/vehicles/new">
          <el-button type="primary">新增車輛</el-button>
        </router-link>
      </div>
    </div>

    <Disclaimer />

    <el-alert v-if="loadError" type="error" :closable="false" show-icon :title="loadError" />

    <div v-loading="loading">
      <el-empty v-if="!loading && !loadError && vehicles.length === 0" description="還沒有登錄任何車輛">
        <router-link to="/vehicles/new">
          <el-button type="primary">新增車輛</el-button>
        </router-link>
      </el-empty>

      <el-card v-for="vehicle in vehicles" :key="vehicle.id" class="vehicle" shadow="hover">
        <div class="vehicle-header">
          <router-link :to="`/vehicles/${vehicle.id}`" class="plate">{{ vehicle.plate }}</router-link>
          <span v-if="vehicle.nickname" class="nickname">{{ vehicle.nickname }}</span>
          <span class="muted">{{ vehicle.brand }} {{ vehicle.model }}</span>
          <span class="muted">{{ VEHICLE_TYPE_LABELS[vehicle.vehicleType] }}</span>
          <span class="muted">出廠 {{ formatDate(vehicle.manufactureDate, calendar) }}</span>
        </div>
        <InspectionBadge
          v-for="inspection in vehicle.inspections"
          :key="inspection.kind"
          :inspection="inspection"
          :calendar="calendar"
        />
      </el-card>
    </div>
  </div>
</template>

<style scoped>
.actions {
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
}

.vehicle {
  margin-bottom: 12px;
}

.vehicle-header {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 4px 12px;
  margin-bottom: 8px;
}

.plate {
  font-size: 1.15rem;
  font-weight: 700;
  color: var(--el-color-primary);
  text-decoration: none;
}

.nickname {
  font-weight: 600;
}
</style>
