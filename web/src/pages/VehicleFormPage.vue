<script setup lang="ts">
// /vehicles/new (create) and /vehicles/:id (detail page: inspections + disclaimer + edit form).
import { computed, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { ApiError, api } from '../api/client'
import { RESTORE_WARNING, errorMessage } from '../api/errors'
import type {
  DisplacementBracket,
  DisplacementBracketOption,
  ProblemDetails,
  UpdateVehicleRequest,
  Vehicle,
  VehicleInput,
  VehicleType,
} from '../api/types'
import DateSelect from '../components/DateSelect.vue'
import Disclaimer from '../components/Disclaimer.vue'
import InspectionBadge from '../components/InspectionBadge.vue'
import { useCurrentUser } from '../composables/useCurrentUser'
import {
  LICENCE_DATE_HELP,
  VEHICLE_TYPES,
  VEHICLE_TYPE_LABELS,
  bracketAllowed,
  licenceDateAllowed,
} from '../labels'

const props = defineProps<{ id?: string }>()
const router = useRouter()
const { calendar } = useCurrentUser()

interface FormState {
  plate: string
  brand: string
  model: string
  nickname: string
  color: string
  vehicleType: VehicleType | null
  displacementBracket: DisplacementBracket | null
  manufactureDate: string | null
  originalIssueDate: string | null
  licenceInspectionDate: string | null
}

const emptyForm = (): FormState => ({
  plate: '',
  brand: '',
  model: '',
  nickname: '',
  color: '',
  vehicleType: null,
  displacementBracket: null,
  manufactureDate: null,
  originalIssueDate: null,
  licenceInspectionDate: null,
})

const FIELD_LABELS: Record<string, string> = {
  plate: '車牌',
  brand: '廠牌',
  model: '型號',
  nickname: '暱稱',
  color: '顏色',
  vehicleType: '車種',
  displacementBracket: '排氣量級距',
  manufactureDate: '出廠日期',
  originalIssueDate: '原發照日期',
  licenceInspectionDate: '行照上的指定檢驗日期',
  version: '版本',
}

const form = reactive<FormState>(emptyForm())
const formRef = ref<FormInstance>()
const vehicle = ref<Vehicle | null>(null)
const brackets = ref<DisplacementBracketOption[]>([])
const loading = ref(false)
const saving = ref(false)
const loadError = ref<string | null>(null)
const problem = ref<ProblemDetails | null>(null)

const isNew = computed(() => !props.id)
const showBracket = computed(() => form.vehicleType !== null && bracketAllowed(form.vehicleType))
const showLicenceDate = computed(() => form.vehicleType !== null && licenceDateAllowed(form.vehicleType))
const bracketOptions = computed(() =>
  form.vehicleType ? brackets.value.filter((b) => b.vehicleTypes.includes(form.vehicleType as VehicleType)) : [],
)

const submitError = computed(() => (problem.value ? errorMessage(problem.value) : null))
const fieldErrors = computed(() =>
  Object.entries(problem.value?.errors ?? {})
    .filter(([field]) => field !== '')
    .map(([field]) => FIELD_LABELS[field] ?? field),
)

const required = (message: string) => ({ required: true, message, trigger: 'change' })
const maxLength = (max: number) => ({ max, message: `最多 ${max} 個字`, trigger: 'blur' })
const rules = computed<FormRules<FormState>>(() => ({
  plate: [{ ...required('請輸入車牌'), whitespace: true, trigger: 'blur' }, maxLength(16)],
  brand: [{ ...required('請輸入廠牌'), whitespace: true, trigger: 'blur' }, maxLength(50)],
  model: [{ ...required('請輸入型號'), whitespace: true, trigger: 'blur' }, maxLength(50)],
  nickname: [maxLength(50)],
  color: [maxLength(50)],
  vehicleType: [required('請選擇車種')],
  displacementBracket: showBracket.value ? [required('請選擇排氣量級距')] : [],
  manufactureDate: [required('請選擇出廠日期')],
  originalIssueDate: [required('請選擇原發照日期')],
}))

function fill(source: Vehicle) {
  Object.assign(form, {
    plate: source.plate,
    brand: source.brand,
    model: source.model,
    nickname: source.nickname ?? '',
    color: source.color ?? '',
    vehicleType: source.vehicleType,
    displacementBracket: source.displacementBracket,
    manufactureDate: source.manufactureDate,
    originalIssueDate: source.originalIssueDate,
    licenceInspectionDate: source.licenceInspectionDate,
  } satisfies FormState)
}

function isUnauthorized(error: unknown) {
  return error instanceof ApiError && error.status === 401
}

async function loadBrackets() {
  if (brackets.value.length > 0) return
  brackets.value = await api.get<DisplacementBracketOption[]>('/api/reference/displacement-brackets')
}

async function load() {
  loading.value = true
  loadError.value = null
  problem.value = null
  try {
    const [, loaded] = await Promise.all([
      loadBrackets(),
      props.id ? api.get<Vehicle>(`/api/vehicles/${encodeURIComponent(props.id)}`) : Promise.resolve(null),
    ])
    vehicle.value = loaded
    if (loaded) fill(loaded)
    else Object.assign(form, emptyForm())
    formRef.value?.clearValidate()
  } catch (error) {
    if (!isUnauthorized(error)) loadError.value = errorMessage(error instanceof ApiError ? error.problem : null)
  } finally {
    loading.value = false
  }
}

watch(() => props.id, load, { immediate: true })

function onVehicleTypeChange(type: VehicleType) {
  // Hidden fields are cleared so that the request matches the type table (Technical Plan §2).
  if (!bracketOptions.value.some((b) => b.code === form.displacementBracket)) form.displacementBracket = null
  if (!bracketAllowed(type)) form.displacementBracket = null
  if (!licenceDateAllowed(type)) form.licenceInspectionDate = null
}

const optional = (value: string) => (value.trim() === '' ? null : value.trim())

function toInput(): VehicleInput {
  const type = form.vehicleType as VehicleType
  return {
    plate: form.plate.trim(),
    brand: form.brand.trim(),
    model: form.model.trim(),
    nickname: optional(form.nickname),
    color: optional(form.color),
    vehicleType: type,
    displacementBracket: bracketAllowed(type) ? form.displacementBracket : null,
    manufactureDate: form.manufactureDate as string,
    originalIssueDate: form.originalIssueDate as string,
    licenceInspectionDate: licenceDateAllowed(type) ? form.licenceInspectionDate : null,
  }
}

async function save() {
  problem.value = null
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  saving.value = true
  try {
    if (isNew.value) {
      const created = await api.post<Vehicle>('/api/vehicles', toInput())
      ElMessage.success('已新增車輛')
      await router.replace(`/vehicles/${created.id}`)
    } else if (vehicle.value) {
      const request: UpdateVehicleRequest = { ...toInput(), version: vehicle.value.version }
      const updated = await api.put<Vehicle>(`/api/vehicles/${encodeURIComponent(vehicle.value.id)}`, request)
      vehicle.value = updated
      fill(updated)
      ElMessage.success('已儲存')
    }
  } catch (error) {
    if (!isUnauthorized(error)) problem.value = error instanceof ApiError ? error.problem : { code: undefined }
  } finally {
    saving.value = false
  }
}

async function moveToTrash() {
  if (!vehicle.value) return
  try {
    await ElMessageBox.confirm(`要把 ${vehicle.value.plate} 丟進垃圾桶嗎？30 天內可以從垃圾桶還原。`, '丟進垃圾桶', {
      confirmButtonText: '丟進垃圾桶',
      cancelButtonText: '取消',
      type: 'warning',
    })
  } catch {
    return
  }
  try {
    await api.post<void>(`/api/vehicles/${encodeURIComponent(vehicle.value.id)}/trash`)
    ElMessage.success('已丟進垃圾桶')
    await router.push('/vehicles')
  } catch (error) {
    if (!isUnauthorized(error)) ElMessage.error(errorMessage(error instanceof ApiError ? error.problem : null))
  }
}
</script>

<template>
  <div v-loading="loading" class="page">
    <div class="page-header">
      <h1>{{ isNew ? '新增車輛' : (vehicle?.plate ?? '車輛資料') }}</h1>
      <router-link to="/vehicles"><el-button link>回到列表</el-button></router-link>
    </div>

    <el-alert v-if="loadError" type="error" :closable="false" show-icon :title="loadError" />

    <template v-if="!loadError">
      <section v-if="vehicle" class="inspections">
        <Disclaimer />
        <el-card>
          <template #header>檢驗期間</template>
          <InspectionBadge
            v-for="inspection in vehicle.inspections"
            :key="inspection.kind"
            :inspection="inspection"
            :calendar="calendar"
          />
        </el-card>
      </section>

      <el-card>
        <template v-if="!isNew" #header>修改車輛資料</template>
        <el-form ref="formRef" :model="form" :rules="rules" label-position="top" @submit.prevent="save">
          <el-form-item label="車牌" prop="plate">
            <el-input v-model="form.plate" maxlength="16" placeholder="例如 ABC-1234" />
          </el-form-item>
          <el-form-item label="廠牌" prop="brand">
            <el-input v-model="form.brand" maxlength="50" />
          </el-form-item>
          <el-form-item label="型號" prop="model">
            <el-input v-model="form.model" maxlength="50" />
          </el-form-item>
          <el-form-item label="暱稱（選填）" prop="nickname">
            <el-input v-model="form.nickname" maxlength="50" />
          </el-form-item>
          <el-form-item label="顏色（選填）" prop="color">
            <el-input v-model="form.color" maxlength="50" />
          </el-form-item>
          <el-form-item label="車種" prop="vehicleType">
            <el-select v-model="form.vehicleType" placeholder="請選擇車種" @change="onVehicleTypeChange">
              <el-option v-for="type in VEHICLE_TYPES" :key="type" :label="VEHICLE_TYPE_LABELS[type]" :value="type" />
            </el-select>
          </el-form-item>
          <el-form-item v-if="showBracket" label="排氣量級距" prop="displacementBracket">
            <el-select v-model="form.displacementBracket" placeholder="請選擇排氣量級距">
              <el-option v-for="option in bracketOptions" :key="option.code" :label="option.label" :value="option.code" />
            </el-select>
          </el-form-item>
          <el-form-item label="出廠日期" prop="manufactureDate">
            <DateSelect v-model="form.manufactureDate" field="manufactureDate" :calendar="calendar" />
            <div class="help">行照只有出廠年月時，請選該月 1 日。</div>
          </el-form-item>
          <el-form-item label="原發照日期" prop="originalIssueDate">
            <DateSelect v-model="form.originalIssueDate" field="originalIssueDate" :calendar="calendar" />
          </el-form-item>
          <el-form-item v-if="showLicenceDate" label="行照上的指定檢驗日期（選填）" prop="licenceInspectionDate">
            <DateSelect
              v-model="form.licenceInspectionDate"
              field="licenceInspectionDate"
              :calendar="calendar"
              clearable
            />
            <div class="help">{{ LICENCE_DATE_HELP }}</div>
          </el-form-item>

          <el-alert v-if="submitError" type="error" :closable="false" show-icon class="submit-error" :title="submitError">
            <template v-if="problem?.code === 'PLATE_EXISTS_IN_TRASH'">
              <p>{{ RESTORE_WARNING }}</p>
              <router-link to="/trash">前往垃圾桶</router-link>
            </template>
            <template v-else-if="problem?.code === 'VEHICLE_MODIFIED'">
              <el-button size="small" @click="load">重新整理</el-button>
            </template>
            <template v-else-if="fieldErrors.length > 0">
              <p>請檢查：{{ fieldErrors.join('、') }}</p>
            </template>
          </el-alert>

          <div class="form-actions">
            <el-button type="primary" native-type="submit" :loading="saving">{{ isNew ? '新增' : '儲存' }}</el-button>
            <el-button v-if="vehicle" type="danger" plain @click="moveToTrash">丟進垃圾桶</el-button>
          </div>
        </el-form>
      </el-card>
    </template>
  </div>
</template>

<style scoped>
.inspections {
  margin-bottom: 16px;
}

.help {
  width: 100%;
  font-size: 0.8rem;
  color: var(--vg-muted);
  line-height: 1.4;
  margin-top: 4px;
}

.submit-error {
  margin-bottom: 16px;
}

.submit-error p {
  margin: 4px 0;
}

.form-actions {
  display: flex;
  gap: 12px;
}
</style>
