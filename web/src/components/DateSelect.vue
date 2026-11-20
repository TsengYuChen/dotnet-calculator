<script setup lang="ts">
// Year / month / day dropdowns (G6). The model is a Gregorian `YYYY-MM-DD` string, or null until all three
// parts are chosen; year labels follow the user's calendar. Changing year or month clamps the day.
import { computed, ref, watch } from 'vue'
import type { CalendarPreference } from '../api/types'
import {
  changeMonth,
  changeYear,
  currentTaipeiYear,
  dayOptions,
  parseIsoDate,
  toIsoDateOrNull,
  yearOptions,
  type DateField,
  type PartialDateParts,
} from '../dates/calendar'

const props = withDefaults(
  defineProps<{ field: DateField; calendar: CalendarPreference; clearable?: boolean; id?: string }>(),
  { clearable: false, id: undefined },
)
const model = defineModel<string | null>({ required: true })

const empty = (): PartialDateParts => ({ year: null, month: null, day: null })
const parts = ref<PartialDateParts>(model.value ? parseIsoDate(model.value) : empty())

watch(model, (value) => {
  if (value === toIsoDateOrNull(parts.value)) return
  // Set from outside (load or reset): replace the dropdowns' state.
  if (value) parts.value = parseIsoDate(value)
  else if (toIsoDateOrNull(parts.value) !== null) parts.value = empty()
})

function update(next: PartialDateParts) {
  parts.value = next
  model.value = toIsoDateOrNull(next)
}

const years = computed(() => yearOptions(props.field, props.calendar, currentTaipeiYear()))
const months = Array.from({ length: 12 }, (_, i) => i + 1)
const days = computed(() => dayOptions(parts.value))
const yearPlaceholder = computed(() => (props.calendar === 'ROC' ? '民國年' : '西元年'))

const year = computed({
  get: () => parts.value.year,
  set: (value: number | null | undefined) => update(changeYear(parts.value, value ?? null)),
})
const month = computed({
  get: () => parts.value.month,
  set: (value: number | null | undefined) => update(changeMonth(parts.value, value ?? null)),
})
const day = computed({
  get: () => parts.value.day,
  set: (value: number | null | undefined) => update({ ...parts.value, day: value ?? null }),
})

function clear() {
  update(empty())
}
</script>

<template>
  <div :id="id" class="date-select">
    <el-select v-model="year" class="year" :placeholder="yearPlaceholder" filterable aria-label="年">
      <el-option v-for="option in years" :key="option.value" :label="`${option.label} 年`" :value="option.value" />
    </el-select>
    <el-select v-model="month" class="part" placeholder="月" aria-label="月">
      <el-option v-for="m in months" :key="m" :label="`${m} 月`" :value="m" />
    </el-select>
    <el-select v-model="day" class="part" placeholder="日" aria-label="日">
      <el-option v-for="d in days" :key="d" :label="`${d} 日`" :value="d" />
    </el-select>
    <el-button v-if="clearable && (year !== null || month !== null || day !== null)" link @click="clear">清除</el-button>
  </div>
</template>

<style scoped>
.date-select {
  display: flex;
  gap: 8px;
  align-items: center;
  flex-wrap: wrap;
}

.year {
  width: 130px;
}

.part {
  width: 96px;
}
</style>
