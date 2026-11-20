import { describe, expect, it } from 'vitest'
import {
  changeMonth,
  changeYear,
  currentTaipeiYear,
  dayOptions,
  daysInDisplayMonth,
  daysInMonth,
  formatDate,
  fromDisplayYear,
  gregorianToRocYear,
  isLeapYear,
  parseIsoDate,
  rocToGregorianYear,
  taipeiDateOf,
  toDisplayYear,
  toIsoDate,
  toIsoDateOrNull,
  yearOptions,
  yearRange,
} from './calendar'

describe('ROC ↔ Gregorian years', () => {
  it('ROC 1 is 1912', () => {
    expect(rocToGregorianYear(1)).toBe(1912)
    expect(gregorianToRocYear(1912)).toBe(1)
  })

  it('ROC 115 is 2026 and ROC 99 is 2010', () => {
    expect(rocToGregorianYear(115)).toBe(2026)
    expect(gregorianToRocYear(2010)).toBe(99)
  })

  it('round-trips in both directions', () => {
    for (let year = 1912; year <= 2100; year++) {
      expect(rocToGregorianYear(gregorianToRocYear(year))).toBe(year)
    }
    for (let roc = 1; roc <= 190; roc++) {
      expect(gregorianToRocYear(rocToGregorianYear(roc))).toBe(roc)
    }
  })

  it('converts display years per calendar preference and back', () => {
    expect(toDisplayYear(2026, 'ROC')).toBe(115)
    expect(toDisplayYear(2026, 'GREGORIAN')).toBe(2026)
    expect(fromDisplayYear(115, 'ROC')).toBe(2026)
    expect(fromDisplayYear(2026, 'GREGORIAN')).toBe(2026)
    for (const calendar of ['ROC', 'GREGORIAN'] as const) {
      expect(fromDisplayYear(toDisplayYear(1950, calendar), calendar)).toBe(1950)
    }
  })
})

describe('days in month', () => {
  it('ROC 113 February has 29 days and ROC 114 February has 28 days', () => {
    expect(daysInDisplayMonth(113, 2, 'ROC')).toBe(29)
    expect(daysInDisplayMonth(114, 2, 'ROC')).toBe(28)
  })

  it('follows the Gregorian leap-year rules', () => {
    expect(isLeapYear(2024)).toBe(true)
    expect(isLeapYear(2025)).toBe(false)
    expect(isLeapYear(2000)).toBe(true)
    expect(isLeapYear(1900)).toBe(false)
    expect(daysInMonth(2000, 2)).toBe(29)
    expect(daysInMonth(2100, 2)).toBe(28)
  })

  it('knows the 30- and 31-day months', () => {
    expect([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12].map((m) => daysInMonth(2026, m))).toEqual([
      31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31,
    ])
  })

  it('offers 29 days for February while the year is not chosen yet', () => {
    expect(dayOptions({ year: null, month: 2, day: null })).toHaveLength(29)
    expect(dayOptions({ year: 2025, month: 2, day: null })).toHaveLength(28)
    expect(dayOptions({ year: null, month: null, day: null })).toHaveLength(31)
  })
})

describe('changing year or month clamps the day to the month end', () => {
  it('3/31 changed to February becomes 2/29 in a leap year and 2/28 otherwise', () => {
    expect(changeMonth({ year: 2024, month: 3, day: 31 }, 2)).toEqual({ year: 2024, month: 2, day: 29 })
    expect(changeMonth({ year: 2025, month: 3, day: 31 }, 2)).toEqual({ year: 2025, month: 2, day: 28 })
  })

  it('2/29 changed to a non-leap year becomes 2/28', () => {
    expect(changeYear({ year: 2024, month: 2, day: 29 }, 2025)).toEqual({ year: 2025, month: 2, day: 28 })
  })

  it('3/31 changed to April becomes 4/30', () => {
    expect(changeMonth({ year: 2026, month: 3, day: 31 }, 4)).toEqual({ year: 2026, month: 4, day: 30 })
  })

  it('keeps a day that exists and keeps unset parts unset', () => {
    expect(changeMonth({ year: 2026, month: 3, day: 15 }, 2)).toEqual({ year: 2026, month: 2, day: 15 })
    expect(changeYear({ year: null, month: null, day: null }, 2026)).toEqual({ year: 2026, month: null, day: null })
    expect(changeMonth({ year: null, month: 1, day: 31 }, 2)).toEqual({ year: null, month: 2, day: 29 })
  })
})

describe('ISO dates', () => {
  it('parses and formats YYYY-MM-DD', () => {
    expect(parseIsoDate('2026-11-19')).toEqual({ year: 2026, month: 11, day: 19 })
    expect(toIsoDate({ year: 2010, month: 3, day: 5 })).toBe('2010-03-05')
    expect(toIsoDate(parseIsoDate('1950-01-01'))).toBe('1950-01-01')
  })

  it('rejects malformed or impossible dates', () => {
    expect(() => parseIsoDate('2026-02-30')).toThrow()
    expect(() => parseIsoDate('115/11/19')).toThrow()
  })

  it('is null until all three parts are chosen', () => {
    expect(toIsoDateOrNull({ year: 2026, month: 11, day: null })).toBeNull()
    expect(toIsoDateOrNull({ year: 2026, month: 11, day: 19 })).toBe('2026-11-19')
  })
})

describe('display formats', () => {
  it('ROC pads month and day but not the year', () => {
    expect(formatDate('2026-11-19', 'ROC')).toBe('115/11/19')
    expect(formatDate('2010-03-05', 'ROC')).toBe('99/03/05')
  })

  it('Gregorian is YYYY/MM/DD', () => {
    expect(formatDate('2026-11-19', 'GREGORIAN')).toBe('2026/11/19')
    expect(formatDate({ year: 2010, month: 3, day: 5 }, 'GREGORIAN')).toBe('2010/03/05')
  })
})

describe('Taipei dates', () => {
  it('converts a UTC instant to the Taipei calendar date', () => {
    expect(taipeiDateOf('2026-12-19T16:30:00Z')).toEqual({ year: 2026, month: 12, day: 20 })
    expect(taipeiDateOf('2026-12-19T15:59:59Z')).toEqual({ year: 2026, month: 12, day: 19 })
  })

  it('"this year" is the Taipei year of the injected now', () => {
    expect(currentTaipeiYear(new Date('2026-12-31T16:00:00Z'))).toBe(2027)
    expect(currentTaipeiYear(new Date('2026-12-31T15:59:59Z'))).toBe(2026)
    expect(currentTaipeiYear(new Date('2026-11-19T00:00:00Z'))).toBe(2026)
  })
})

describe('year ranges of the three date fields', () => {
  it('manufacture and original issue dates: 1950 to this year', () => {
    expect(yearRange('manufactureDate', 2026)).toEqual({ min: 1950, max: 2026 })
    expect(yearRange('originalIssueDate', 2026)).toEqual({ min: 1950, max: 2026 })
  })

  it('licence inspection date: 1950 to this year + 2', () => {
    expect(yearRange('licenceInspectionDate', 2026)).toEqual({ min: 1950, max: 2028 })
  })

  it('options are newest first with labels in the chosen calendar', () => {
    const roc = yearOptions('manufactureDate', 'ROC', 2026)
    expect(roc).toHaveLength(2026 - 1950 + 1)
    expect(roc[0]).toEqual({ value: 2026, label: '115' })
    expect(roc.at(-1)).toEqual({ value: 1950, label: '39' })

    const gregorian = yearOptions('licenceInspectionDate', 'GREGORIAN', 2026)
    expect(gregorian[0]).toEqual({ value: 2028, label: '2028' })
    expect(gregorian.at(-1)).toEqual({ value: 1950, label: '1950' })
  })
})
