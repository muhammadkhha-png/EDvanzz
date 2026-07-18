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
  /** Current capacity package id — added to TeacherProfileDto (D2-B) so edit can preselect/re-send it. */
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
//
// SINGLE CALL. Sign-up with userType=Teacher already creates BOTH the User and
// the Teacher record: UserService.AddUser opens one transaction, creates the User,
// then calls TeacherService.InitializeTeacherAsync (generates TeacherCode, links
// subjects, seeds TeacherConfiguration + prorated tiers) inside it.
// => Do NOT also POST /api/teacher/initialize — the teacher already exists and
//    that endpoint would hit the duplicate-teacher guard and return 409 Conflict.
export interface CreateTeacherSignUpRequest {
  userType: 'Teacher';
  fullName: string;
  username: string;
  email?: string;
  password: string;
  confirmedPassword: string;
  /** Required. Egyptian mobile: 010/011/012/015 + 8 digits (^01[0125]\d{8}$). */
  phoneNumber: string;
  /** Exactly one id per current UX (single-subject select). */
  subjectIds: number[];
  /** Teacher's app language. InitializeTeacherAsync rejects anything but 'en'/'ar'. */
  languagePreference: 'en' | 'ar';
  /** Backend defaults to 500 when omitted. */
  studentCapacity?: number;
  /** Optional free-text subject (alternative/addition to subjectIds). */
  customSubject?: string;
  /** Optional ID image. */
  idImage?: File | null;
}

// ── Update teacher profile (PUT /api/teacher/{id}/profile) ───────────────────
//
// Editable via this endpoint ONLY. TeacherCode, AccountStatus, Email, Username,
// Password and PhoneNumber are NOT updatable here (managed elsewhere / immutable).
// subjectIds REPLACES all existing subject associations. At least one subjectId
// OR a customSubject is required.
//
// Capacity rule: for an already-configured teacher, sending a studentCapacityPackageId
// DIFFERENT from the current one returns 400 CapacityChangeRequiresApproval — that path
// must go through the capacity-increase request flow. Re-sending the current id is a no-op.
export interface UpdateTeacherProfileRequest {
  fullName: string;
  languagePreference: 'en' | 'ar';
  subjectIds: number[];
  customSubject?: string;
  studentCapacityPackageId?: number | null;
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
