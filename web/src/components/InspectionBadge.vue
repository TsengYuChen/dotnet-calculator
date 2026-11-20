<script setup lang="ts">
// G5: text label for every status (not colour only); IN_WINDOW changes colour; estimate / licence notes.
import { computed } from 'vue'
import type { CalendarPreference, Inspection } from '../api/types'
import { formatDate } from '../dates/calendar'
import { ESTIMATED_NOTE, INSPECTION_KIND_LABELS, describeInspection } from '../labels'

const props = defineProps<{ inspection: Inspection; calendar: CalendarPreference }>()

const display = computed(() => describeInspection(props.inspection))
const dates = computed(() => {
  const { earliestDate, deadline } = props.inspection
  if (!display.value.showDates || !earliestDate || !deadline) return null
  return `${formatDate(earliestDate, props.calendar)} – ${formatDate(deadline, props.calendar)}`
})
</script>

<template>
  <div class="inspection" :class="{ highlighted: display.highlighted }">
    <span class="kind">{{ INSPECTION_KIND_LABELS[inspection.kind] }}：</span>
    <span class="status">{{ display.statusLabel }}</span>
    <span v-if="dates" class="dates">{{ dates }}</span>
    <el-tag
      v-if="display.note"
      class="note"
      size="small"
      :type="display.note === ESTIMATED_NOTE ? 'warning' : 'info'"
      effect="plain"
    >
      {{ display.note }}
    </el-tag>
  </div>
</template>

<style scoped>
.inspection {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 4px 8px;
  padding: 2px 6px;
  border-radius: 4px;
}

.kind {
  font-weight: 600;
}

.dates {
  font-variant-numeric: tabular-nums;
}

.highlighted {
  background: var(--vg-highlight-bg);
}

.highlighted .status,
.highlighted .dates {
  color: var(--vg-highlight);
  font-weight: 700;
}
</style>
