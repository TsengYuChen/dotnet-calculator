import { describe, expect, it } from 'vitest'
import type { Inspection } from './api/types'
import {
  ESTIMATED_NOTE,
  INSPECTION_KIND_LABELS,
  LICENCE_NOTE,
  VEHICLE_TYPE_LABELS,
  bracketAllowed,
  describeInspection,
  licenceDateAllowed,
} from './labels'

const base: Inspection = {
  kind: 'PERIODIC',
  status: 'UPCOMING',
  earliestDate: '2027-01-20',
  deadline: '2027-03-20',
  ruleId: 'RTS44-CAR-AGE10UP',
  estimated: false,
  basis: 'FORMULA',
}

describe('labels (G2, G5)', () => {
  it('vehicle type labels match the G2 menu', () => {
    expect(VEHICLE_TYPE_LABELS).toEqual({
      CAR: '自用小客車（非營業用）',
      FUEL_MC: '一般燃油機車',
      HEAVY_MC: '大型重型機車',
      ELECTRIC_MC: '電動機車（40 馬力以下）',
    })
    expect(INSPECTION_KIND_LABELS).toEqual({ PERIODIC: '定期檢驗', EXHAUST: '排氣定檢' })
  })

  it('bracket and licence date availability follow the type table', () => {
    expect(['CAR', 'FUEL_MC', 'HEAVY_MC', 'ELECTRIC_MC'].map((t) => bracketAllowed(t as never))).toEqual([
      true, true, true, false,
    ])
    expect(['CAR', 'FUEL_MC', 'HEAVY_MC', 'ELECTRIC_MC'].map((t) => licenceDateAllowed(t as never))).toEqual([
      true, false, true, false,
    ])
  })
})

describe('describeInspection (G5 status table)', () => {
  it('EXEMPT: 免檢, no dates, not highlighted', () => {
    expect(describeInspection({ ...base, kind: 'EXHAUST', status: 'EXEMPT', earliestDate: null, deadline: null })).toEqual({
      statusLabel: '免檢',
      showDates: false,
      highlighted: false,
      note: null,
    })
  })

  it('UPCOMING: 下次檢驗期間 with dates, not highlighted', () => {
    expect(describeInspection(base)).toMatchObject({ statusLabel: '下次檢驗期間', showDates: true, highlighted: false })
  })

  it('IN_WINDOW: 檢驗期間內 with dates, highlighted', () => {
    expect(describeInspection({ ...base, status: 'IN_WINDOW' })).toMatchObject({
      statusLabel: '檢驗期間內',
      showDates: true,
      highlighted: true,
    })
  })

  it('UNCOMPUTABLE: the fixed message, no dates', () => {
    expect(describeInspection({ ...base, status: 'UNCOMPUTABLE', earliestDate: null, deadline: null, ruleId: null })).toEqual({
      statusLabel: '無法計算，請確認車種與日期是否正確；若仍有問題請回報車牌',
      showDates: false,
      highlighted: false,
      note: null,
    })
  })

  it('estimated always shows the estimate note, even with basis LICENCE (CUJ 3, 4c)', () => {
    expect(ESTIMATED_NOTE).toBe('推算日期，請以行照為準')
    expect(describeInspection({ ...base, estimated: true }).note).toBe(ESTIMATED_NOTE)
    expect(describeInspection({ ...base, estimated: true, basis: 'LICENCE' }).note).toBe(ESTIMATED_NOTE)
  })

  it('LICENCE basis without estimate shows 依行照指定檢驗日期 (CUJ 4)', () => {
    expect(LICENCE_NOTE).toBe('依行照指定檢驗日期')
    expect(describeInspection({ ...base, basis: 'LICENCE' }).note).toBe(LICENCE_NOTE)
    expect(describeInspection(base).note).toBeNull()
  })
})
