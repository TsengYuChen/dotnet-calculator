// Display labels (G2) and the G5 status table. The frontend only maps `status`, `basis` and `estimated`
// returned by the API; it never computes inspection dates.
import type { Inspection, InspectionKind, VehicleType } from './api/types'

export const VEHICLE_TYPES: VehicleType[] = ['CAR', 'FUEL_MC', 'HEAVY_MC', 'ELECTRIC_MC']

export const VEHICLE_TYPE_LABELS: Record<VehicleType, string> = {
  CAR: '自用小客車（非營業用）',
  FUEL_MC: '一般燃油機車',
  HEAVY_MC: '大型重型機車',
  ELECTRIC_MC: '電動機車（40 馬力以下）',
}

export const INSPECTION_KIND_LABELS: Record<InspectionKind, string> = {
  PERIODIC: '定期檢驗',
  EXHAUST: '排氣定檢',
}

export const ESTIMATED_NOTE = '推算日期，請以行照為準'
export const LICENCE_NOTE = '依行照指定檢驗日期'
export const LICENCE_DATE_HELP = '每次驗完車，請改成新行照上的指定檢驗日期。'

export interface InspectionDisplay {
  statusLabel: string
  showDates: boolean
  /** IN_WINDOW only: the dates change colour. */
  highlighted: boolean
  note: string | null
}

export function describeInspection(inspection: Inspection): InspectionDisplay {
  switch (inspection.status) {
    case 'EXEMPT':
      return { statusLabel: '免檢', showDates: false, highlighted: false, note: null }
    case 'UNCOMPUTABLE':
      return {
        statusLabel: '無法計算，請確認車種與日期是否正確；若仍有問題請回報車牌',
        showDates: false,
        highlighted: false,
        note: null,
      }
    case 'UPCOMING':
    case 'IN_WINDOW': {
      const note = inspection.estimated ? ESTIMATED_NOTE : inspection.basis === 'LICENCE' ? LICENCE_NOTE : null
      const inWindow = inspection.status === 'IN_WINDOW'
      return { statusLabel: inWindow ? '檢驗期間內' : '下次檢驗期間', showDates: true, highlighted: inWindow, note }
    }
  }
}

/** Technical Plan §2 type table: every type except ELECTRIC_MC has a (required) bracket. */
export function bracketAllowed(type: VehicleType): boolean {
  return type !== 'ELECTRIC_MC'
}

/** Only CAR and HEAVY_MC may have a licence inspection date. */
export function licenceDateAllowed(type: VehicleType): boolean {
  return type === 'CAR' || type === 'HEAVY_MC'
}
