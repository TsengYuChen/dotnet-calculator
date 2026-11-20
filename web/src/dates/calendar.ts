// ROC / Gregorian conversion and the logic behind the year/month/day dropdowns (G6, design §2.8).
// Dates are handled as three numbers (Gregorian year, month, day); the JavaScript Date type is only used
// to read "now" and to convert UTC instants to the Taipei calendar date. No inspection date arithmetic here.
import type { CalendarPreference } from '../api/types'

/** A complete Gregorian calendar date. */
export interface DateParts {
  year: number
  month: number
  day: number
}

/** The dropdowns' state: any part may still be unchosen. `year` is always Gregorian. */
export interface PartialDateParts {
  year: number | null
  month: number | null
  day: number | null
}

export type DateField = 'manufactureDate' | 'originalIssueDate' | 'licenceInspectionDate'

export interface YearRange {
  min: number
  max: number
}

/** A year dropdown entry: `value` is the Gregorian year, `label` is in the user's calendar. */
export interface YearOption {
  value: number
  label: string
}

export const MIN_YEAR = 1950
/** ROC year 1 = 1912. */
const ROC_OFFSET = 1911
/** LICENCE_DATE_TOO_FAR: the licence inspection date may be at most this year + 2 (12/31). */
const LICENCE_YEARS_AHEAD = 2
const TAIPEI = 'Asia/Taipei'

export function rocToGregorianYear(rocYear: number): number {
  return rocYear + ROC_OFFSET
}

export function gregorianToRocYear(year: number): number {
  return year - ROC_OFFSET
}

export function toDisplayYear(year: number, calendar: CalendarPreference): number {
  return calendar === 'ROC' ? gregorianToRocYear(year) : year
}

export function fromDisplayYear(displayYear: number, calendar: CalendarPreference): number {
  return calendar === 'ROC' ? rocToGregorianYear(displayYear) : displayYear
}

export function isLeapYear(year: number): boolean {
  return (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0
}

export function daysInMonth(year: number, month: number): number {
  if (month === 2) return isLeapYear(year) ? 29 : 28
  return [4, 6, 9, 11].includes(month) ? 30 : 31
}

export function daysInDisplayMonth(displayYear: number, month: number, calendar: CalendarPreference): number {
  return daysInMonth(fromDisplayYear(displayYear, calendar), month)
}

/** Largest day the dropdown may offer; while the year is unknown February allows 29. */
function maxDay(year: number | null, month: number | null): number {
  if (month === null) return 31
  return daysInMonth(year ?? 2000, month)
}

function clamp(parts: PartialDateParts): PartialDateParts {
  if (parts.day === null) return parts
  return { ...parts, day: Math.min(parts.day, maxDay(parts.year, parts.month)) }
}

/** Changes the year; a day that no longer exists becomes the last day of the month. */
export function changeYear(parts: PartialDateParts, year: number | null): PartialDateParts {
  return clamp({ ...parts, year })
}

/** Changes the month; a day that no longer exists becomes the last day of the month (3/31 → 2/28 or 2/29). */
export function changeMonth(parts: PartialDateParts, month: number | null): PartialDateParts {
  return clamp({ ...parts, month })
}

export function dayOptions(parts: PartialDateParts): number[] {
  return Array.from({ length: maxDay(parts.year, parts.month) }, (_, i) => i + 1)
}

const ISO_DATE = /^(\d{4})-(\d{2})-(\d{2})$/

export function parseIsoDate(iso: string): DateParts {
  const match = ISO_DATE.exec(iso)
  if (!match) throw new Error(`Not a YYYY-MM-DD date: ${iso}`)
  const parts = { year: Number(match[1]), month: Number(match[2]), day: Number(match[3]) }
  if (parts.month < 1 || parts.month > 12 || parts.day < 1 || parts.day > daysInMonth(parts.year, parts.month)) {
    throw new Error(`Not an existing date: ${iso}`)
  }
  return parts
}

const pad2 = (n: number) => String(n).padStart(2, '0')

export function toIsoDate(parts: DateParts): string {
  return `${String(parts.year).padStart(4, '0')}-${pad2(parts.month)}-${pad2(parts.day)}`
}

export function toIsoDateOrNull(parts: PartialDateParts): string | null {
  if (parts.year === null || parts.month === null || parts.day === null) return null
  return toIsoDate({ year: parts.year, month: parts.month, day: parts.day })
}

/** `115/11/19`, `99/03/05` (ROC: year not padded) or `2026/11/19` (Gregorian). */
export function formatDate(value: string | DateParts, calendar: CalendarPreference): string {
  const parts = typeof value === 'string' ? parseIsoDate(value) : value
  return `${toDisplayYear(parts.year, calendar)}/${pad2(parts.month)}/${pad2(parts.day)}`
}

const taipeiFormat = new Intl.DateTimeFormat('en-US', {
  timeZone: TAIPEI,
  year: 'numeric',
  month: 'numeric',
  day: 'numeric',
})

/** The Taipei calendar date of an instant (e.g. `purgeAt`). */
export function taipeiDateOf(instant: Date | string): DateParts {
  const date = typeof instant === 'string' ? new Date(instant) : instant
  const parts = taipeiFormat.formatToParts(date)
  const get = (type: Intl.DateTimeFormatPartTypes) => Number(parts.find((p) => p.type === type)?.value)
  return { year: get('year'), month: get('month'), day: get('day') }
}

/** This year in Taipei time; `now` is injectable for tests. */
export function currentTaipeiYear(now: Date = new Date()): number {
  return taipeiDateOf(now).year
}

export function yearRange(field: DateField, thisYear: number): YearRange {
  const max = field === 'licenceInspectionDate' ? thisYear + LICENCE_YEARS_AHEAD : thisYear
  return { min: MIN_YEAR, max }
}

/** Year dropdown entries, newest first. */
export function yearOptions(field: DateField, calendar: CalendarPreference, thisYear: number): YearOption[] {
  const { min, max } = yearRange(field, thisYear)
  return Array.from({ length: max - min + 1 }, (_, i) => {
    const year = max - i
    return { value: year, label: String(toDisplayYear(year, calendar)) }
  })
}
