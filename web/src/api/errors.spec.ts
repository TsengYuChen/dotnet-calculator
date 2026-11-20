import { describe, expect, it } from 'vitest'
import { ERROR_MESSAGES, GENERIC_ERROR_MESSAGE, RESTORE_WARNING, errorMessage } from './errors'
import type { ErrorCode } from './types'

const allCodes: ErrorCode[] = [
  'VALIDATION_FAILED',
  'ISSUE_BEFORE_MANUFACTURE',
  'DATE_IN_FUTURE',
  'BRACKET_TYPE_MISMATCH',
  'LICENCE_DATE_NOT_ALLOWED',
  'LICENCE_DATE_BEFORE_ISSUE',
  'LICENCE_DATE_TOO_FAR',
  'CSRF_INVALID',
  'VEHICLE_NOT_FOUND',
  'PLATE_EXISTS_ACTIVE',
  'PLATE_EXISTS_IN_TRASH',
  'VEHICLE_LIMIT_REACHED',
  'VEHICLE_MODIFIED',
  'VEHICLE_IN_TRASH',
  'VEHICLE_NOT_IN_TRASH',
]

describe('error code → zh-TW message', () => {
  it('has a non-empty message for every §1.5 code', () => {
    for (const code of allCodes) {
      expect(ERROR_MESSAGES[code], code).toMatch(/\S/)
      expect(errorMessage({ code }), code).not.toBe(GENERIC_ERROR_MESSAGE)
    }
  })

  it('uses the exact texts of G2 and CUJ 7', () => {
    expect(errorMessage({ code: 'VEHICLE_MODIFIED' })).toBe('資料已被修改，請重新整理')
    expect(errorMessage({ code: 'PLATE_EXISTS_IN_TRASH' })).toBe('此車牌已在垃圾桶中，請至垃圾桶還原')
    expect(RESTORE_WARNING).toContain('舊資料')
  })

  it('includes the counts for VEHICLE_LIMIT_REACHED (CUJ 8)', () => {
    expect(errorMessage({ code: 'VEHICLE_LIMIT_REACHED', activeCount: 47, trashCount: 3 })).toBe(
      '已達 50 台車輛上限（其中垃圾桶 3 台）。請永久刪除不需要的車輛',
    )
  })

  it('falls back to a generic message for unknown codes and missing problems', () => {
    expect(errorMessage({ code: 'SOMETHING_ELSE' })).toBe(GENERIC_ERROR_MESSAGE)
    expect(errorMessage(null)).toBe(GENERIC_ERROR_MESSAGE)
    expect(GENERIC_ERROR_MESSAGE).toMatch(/\S/)
  })
})
