// Data types of the API contract (design §1.1, §1.4, §1.5). Calendar dates are Gregorian `YYYY-MM-DD`
// strings; instants (`deletedAt`, `purgeAt`) are ISO 8601 UTC strings.

export type VehicleType = 'CAR' | 'FUEL_MC' | 'HEAVY_MC' | 'ELECTRIC_MC'

export type DisplacementBracket =
  | 'CAR_LE_500'
  | 'CAR_501_600'
  | 'CAR_601_1200'
  | 'CAR_1201_1800'
  | 'CAR_1801_2400'
  | 'CAR_2401_3000'
  | 'CAR_3001_4200'
  | 'CAR_4201_5400'
  | 'CAR_5401_6600'
  | 'CAR_6601_7800'
  | 'CAR_GT_7800'
  | 'CAR_ELECTRIC'
  | 'MC_LE_50'
  | 'MC_51_250'
  | 'MC_251_550'
  | 'MC_GT_550'

export type InspectionKind = 'PERIODIC' | 'EXHAUST'
export type InspectionStatus = 'EXEMPT' | 'UPCOMING' | 'IN_WINDOW' | 'UNCOMPUTABLE'
export type InspectionBasis = 'FORMULA' | 'LICENCE'
export type CalendarPreference = 'ROC' | 'GREGORIAN'
export type VehicleSort = 'deadline' | 'manufactureDate'

export interface Inspection {
  kind: InspectionKind
  status: InspectionStatus
  earliestDate: string | null
  deadline: string | null
  ruleId: string | null
  estimated: boolean
  basis: InspectionBasis
}

export interface VehicleInput {
  plate: string
  brand: string
  model: string
  nickname: string | null
  color: string | null
  vehicleType: VehicleType
  displacementBracket: DisplacementBracket | null
  manufactureDate: string
  originalIssueDate: string
  licenceInspectionDate: string | null
}

export interface UpdateVehicleRequest extends VehicleInput {
  version: number
}

export interface Vehicle extends VehicleInput {
  id: string
  version: number
  inspections: Inspection[]
}

export interface TrashItem {
  id: string
  plate: string
  brand: string
  model: string
  nickname: string | null
  vehicleType: VehicleType
  deletedAt: string
  purgeAt: string
}

export interface Me {
  email: string
  calendar: CalendarPreference
}

export interface UpdatePreferencesRequest {
  calendar: CalendarPreference
}

export interface DisplacementBracketOption {
  code: DisplacementBracket
  label: string
  vehicleTypes: VehicleType[]
}

export type ErrorCode =
  | 'VALIDATION_FAILED'
  | 'ISSUE_BEFORE_MANUFACTURE'
  | 'DATE_IN_FUTURE'
  | 'BRACKET_TYPE_MISMATCH'
  | 'LICENCE_DATE_NOT_ALLOWED'
  | 'LICENCE_DATE_BEFORE_ISSUE'
  | 'LICENCE_DATE_TOO_FAR'
  | 'CSRF_INVALID'
  | 'VEHICLE_NOT_FOUND'
  | 'PLATE_EXISTS_ACTIVE'
  | 'PLATE_EXISTS_IN_TRASH'
  | 'VEHICLE_LIMIT_REACHED'
  | 'VEHICLE_MODIFIED'
  | 'VEHICLE_IN_TRASH'
  | 'VEHICLE_NOT_IN_TRASH'

/** ProblemDetails with the extension fields of design §1.4. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  traceId?: string
  code?: ErrorCode | string
  errors?: Record<string, string[]>
  trashedVehicleId?: string
  activeCount?: number
  trashCount?: number
}
