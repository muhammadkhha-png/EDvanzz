import { PagedQuery } from './paginated-response.model';

// ── Teacher list item (GET /api/teacher/list) ────────────────────────────────
export interface TeacherListItem {
  id: number;
  fullName: string;
  username: string;
  teacherCode: string;
  phoneNumber?: string;
  studentCapacity: number;
  accountStatus: string;
  isConfigurationCompleted: boolean;
  subscriptionStatus?: string;
  subscriptionEndDate?: string;
  createdAt: string;
}

// ── Teacher detail (GET /api/teacher/{id}/profile) ───────────────────────────
export interface TeacherProfile {
  id: number;
  userId: number;
  teacherCode: string;
  fullName: string;
  email?: string;
  phoneNumber?: string;
  studentCapacity: number;
  languagePreference: string;
  customSubject?: string;
  accountStatus: string;
  isConfigurationCompleted: boolean;
  createdAt: string;
  subjects: SubjectDto[];
  capacityPackageName?: string;
  /** Current capacity package id (added to TeacherProfileDto) so edit can preselect it. */
  studentCapacityPackageId?: number | null;
  activeSubscription?: TeacherSubscriptionDto;
}

// ── Subject lookup (GET /api/teacher/subjects) ───────────────────────────────
export interface SubjectDto {
  id: number;
  nameEn: string;
  nameAr: string;
  displayOrder: number;
}

// ── Capacity package lookup (GET /api/teacher/capacity-packages) ─────────────
export interface StudentCapacityPackageDto {
  id: number;
  name: string;
  minStudents: number;
  maxStudents: number | null;
  displayOrder: number;
}

export interface TeacherSubscriptionDto {
  id: number;
  subscriptionStatus: string;
  startDate: string;
  endDate: string;
  daysRemaining: number;
}

// ── Create teacher (POST /api/Auth/sign-up — multipart/form-data) ────────────
// SINGLE CALL — sign-up with userType=Teacher creates User + Teacher in one server
// transaction. Do NOT also call /api/teacher/initialize (would 409).
export interface CreateTeacherSignUpRequest {
  userType: 'Teacher';
  fullName: string;
  username: string;
  email?: string;
  password: string;
  confirmedPassword: string;
  /** Required. Egyptian mobile: 010/011/012/015 + 8 digits (^01[0125]\d{8}$). */
  phoneNumber: string;
  subjectIds: number[];
  languagePreference: 'en' | 'ar';
  studentCapacity?: number;
  customSubject?: string;
  idImage?: File | null;
}

// ── Update teacher profile (PUT /api/teacher/{id}/profile) ───────────────────
// Editable: fullName, languagePreference, subjectIds (replaces all), customSubject,
// and studentCapacityPackageId ONLY while the teacher is not yet configured. Once
// isConfigurationCompleted, capacity is managed via the admin capacity endpoint below,
// so the client omits studentCapacityPackageId for configured teachers.
export interface UpdateTeacherProfileRequest {
  fullName: string;
  languagePreference: 'en' | 'ar';
  subjectIds: number[];
  customSubject?: string;
  studentCapacityPackageId?: number | null;
}

// ── Admin capacity adjust (PUT /api/admin/subscriptions/teachers/{id}/capacity) ─
// SuperAdmin-only, increase-only. Raises Teacher.StudentCapacity directly (no prior
// teacher request), writes an Approved audit row, notifies the teacher; new price from
// the next renewal. teacherId is in the route.
export interface AdminSetCapacityRequest {
  newCapacity: number;
  note?: string;
}

/** Subset of CapacityRequestDto returned by the admin capacity endpoint. */
export interface CapacityAdjustResult {
  id: number;
  requestedCapacity: number;
  capacityAtRequest: number;
  status: string;
}

// ── Teacher list query params ─────────────────────────────────────────────────
export interface TeacherListQuery extends PagedQuery {
  accountStatus?: string;
  subscriptionStatus?: string;
}

// ── Dashboard derived from teacher/list totalCount ───────────────────────────
export interface DashboardSummary {
  totalTeachers: number;
  activeTeachers: number;
  expiredSubscriptions: number;
  expiringSoon: number;
}
