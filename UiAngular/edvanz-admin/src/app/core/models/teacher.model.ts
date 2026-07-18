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
  activeSubscription?: TeacherSubscriptionDto;
}

export interface SubjectDto {
  id: number;
  nameEn: string;
  nameAr: string;
  displayOrder: number;
}

export interface TeacherSubscriptionDto {
  id: number;
  subscriptionStatus: string;
  startDate: string;
  endDate: string;
  daysRemaining: number;
}

// ── Create teacher (POST /api/Auth/sign-up + POST /api/teacher/initialize) ───
export interface SignUpRequest {
  userName: string;
  password: string;
  fullName: string;
  email?: string;
  phoneNumber?: string;
  userType: 'Teacher';
}

export interface InitializeTeacherRequest {
  userId: number;
  subjectIds: number[];
  customSubject?: string;
  languagePreference?: string;
  createdByUserId?: number;
  studentCapacity?: number;
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
