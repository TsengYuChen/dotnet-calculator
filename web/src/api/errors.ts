// Error code (design §1.5) → zh-TW message.
import type { ErrorCode, ProblemDetails } from './types'

/** G2: at most 50 vehicles per user, trash included. */
export const VEHICLE_LIMIT = 50

export const GENERIC_ERROR_MESSAGE = '發生錯誤，請稍後再試'

/** Shown with PLATE_EXISTS_IN_TRASH (CUJ 7). */
export const RESTORE_WARNING = '還原會帶回垃圾桶裡的舊資料，剛填的內容不會保留。'

export const ERROR_MESSAGES: Record<ErrorCode, string> = {
  VALIDATION_FAILED: '輸入的資料有誤，請檢查各欄位',
  ISSUE_BEFORE_MANUFACTURE: '原發照日期不能早於出廠日期',
  DATE_IN_FUTURE: '出廠日期和原發照日期不能是未來的日期',
  BRACKET_TYPE_MISMATCH: '排氣量級距和車種不符，請重新選擇',
  LICENCE_DATE_NOT_ALLOWED: '只有自用小客車和大型重型機車可以填指定檢驗日期',
  LICENCE_DATE_BEFORE_ISSUE: '指定檢驗日期不能早於原發照日期',
  LICENCE_DATE_TOO_FAR: '指定檢驗日期最晚只能到後年的 12 月 31 日，請確認是否輸入錯誤',
  CSRF_INVALID: '安全驗證失敗，請重新整理頁面後再試',
  VEHICLE_NOT_FOUND: '找不到這台車輛，可能已被刪除',
  PLATE_EXISTS_ACTIVE: '此車牌已經登錄過了',
  PLATE_EXISTS_IN_TRASH: '此車牌已在垃圾桶中，請至垃圾桶還原',
  VEHICLE_LIMIT_REACHED: `已達 ${VEHICLE_LIMIT} 台車輛上限。請永久刪除不需要的車輛`,
  VEHICLE_MODIFIED: '資料已被修改，請重新整理',
  VEHICLE_IN_TRASH: '這台車已經在垃圾桶中',
  VEHICLE_NOT_IN_TRASH: '這台車不在垃圾桶中，請重新整理',
}

function isKnownCode(code: string | undefined): code is ErrorCode {
  return code !== undefined && Object.hasOwn(ERROR_MESSAGES, code)
}

export function errorMessage(problem: ProblemDetails | null | undefined): string {
  const code = problem?.code
  if (!isKnownCode(code)) return GENERIC_ERROR_MESSAGE
  if (code === 'VEHICLE_LIMIT_REACHED' && typeof problem?.trashCount === 'number') {
    return `已達 ${VEHICLE_LIMIT} 台車輛上限（其中垃圾桶 ${problem.trashCount} 台）。請永久刪除不需要的車輛`
  }
  return ERROR_MESSAGES[code]
}
