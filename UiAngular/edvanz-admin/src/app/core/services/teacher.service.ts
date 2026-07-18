import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResult } from '../models/api-result.model';
import { PaginatedResponse } from '../models/paginated-response.model';
import {
  CreateTeacherSignUpRequest,
  DashboardSummary,
  StudentCapacityPackageDto,
  SubjectDto,
  TeacherListItem,
  TeacherListQuery,
  TeacherProfile,
  UpdateTeacherProfileRequest,
} from '../models/teacher.model';

@Injectable({ providedIn: 'root' })
export class TeacherService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  /**
   * GET /api/teacher/list
   * SuperAdmin paginated teacher list with search, sort, and status filters.
   */
  getTeachers(
    query: TeacherListQuery,
  ): Observable<PaginatedResponse<TeacherListItem[]>> {
    let params = new HttpParams()
      .set('page', query.page ?? 1)
      .set('pageSize', query.pageSize ?? 20);
    if (query.search) params = params.set('search', query.search);
    if (query.sortBy) params = params.set('sortBy', query.sortBy);
    if (query.sortDirection) params = params.set('sortDirection', query.sortDirection);
    if (query.accountStatus) params = params.set('accountStatus', query.accountStatus);
    if (query.subscriptionStatus) params = params.set('subscriptionStatus', query.subscriptionStatus);

    return this.http
      .get<ApiResult<PaginatedResponse<TeacherListItem[]>>>(`${this.base}/teacher/list`, { params })
      .pipe(map((r) => r.data));
  }

  /**
   * GET /api/teacher/{teacherId}/profile
   * Full teacher detail including active subscription. For SuperAdmin the route id
   * is honoured server-side (support access), so this reads ANY teacher by id.
   */
  getTeacherById(teacherId: number): Observable<TeacherProfile> {
    return this.http
      .get<ApiResult<TeacherProfile>>(`${this.base}/teacher/${teacherId}/profile`)
      .pipe(map((r) => r.data));
  }

  /**
   * Derives dashboard KPIs from teacher/list by running four filtered calls.
   * Four parallel calls are fine at this scale; replace with a dedicated
   * stats endpoint if one is added later.
   */
  getDashboardSummary(): Observable<DashboardSummary> {
    const tiny = { page: 1, pageSize: 1 };
    const get = (extra: TeacherListQuery) =>
      this.http
        .get<ApiResult<PaginatedResponse<TeacherListItem[]>>>(
          `${this.base}/teacher/list`,
          {
            params: new HttpParams({ fromObject: { ...tiny, ...extra } as Record<string, string | number> }),
          },
        )
        .pipe(map((r) => r.data.totalCount));

    return new Observable<DashboardSummary>((obs) => {
      let done = 0;
      const counts = { total: 0, active: 0, expired: 0, expiringSoon: 0 };
      const check = () => {
        if (++done === 4) {
          obs.next({
            totalTeachers: counts.total,
            activeTeachers: counts.active,
            expiredSubscriptions: counts.expired,
            expiringSoon: counts.expiringSoon,
          });
          obs.complete();
        }
      };
      get({}).subscribe({ next: (n) => { counts.total = n; check(); }, error: (e) => obs.error(e) });
      get({ subscriptionStatus: 'Active' }).subscribe({ next: (n) => { counts.active = n; check(); }, error: (e) => obs.error(e) });
      get({ subscriptionStatus: 'Expired' }).subscribe({ next: (n) => { counts.expired = n; check(); }, error: (e) => obs.error(e) });
      get({ subscriptionStatus: 'ExpiringSoon' }).subscribe({ next: (n) => { counts.expiringSoon = n; check(); }, error: (e) => obs.error(e) });
    });
  }

  /**
   * GET /api/teacher/subjects
   * Ministry-defined subject lookup (bilingual names). `lang` sets Accept-Language.
   */
  getSubjects(lang: 'en' | 'ar' = 'en'): Observable<SubjectDto[]> {
    return this.http
      .get<ApiResult<SubjectDto[]>>(`${this.base}/teacher/subjects`, {
        headers: { 'Accept-Language': lang },
      })
      .pipe(map((r) => r.data ?? []));
  }

  /**
   * GET /api/teacher/capacity-packages
   * The 7 active student-capacity tiers, ordered by displayOrder. `lang` sets Accept-Language.
   */
  getCapacityPackages(lang: 'en' | 'ar' = 'en'): Observable<StudentCapacityPackageDto[]> {
    return this.http
      .get<ApiResult<StudentCapacityPackageDto[]>>(`${this.base}/teacher/capacity-packages`, {
        headers: { 'Accept-Language': lang },
      })
      .pipe(map((r) => r.data ?? []));
  }

  /**
   * Create teacher — SINGLE CALL: POST /api/Auth/sign-up (multipart/form-data).
   * Content-Type is intentionally NOT set — the browser adds the multipart boundary.
   */
  createTeacher(
    req: CreateTeacherSignUpRequest,
    lang: 'en' | 'ar' = 'en',
  ): Observable<ApiResult<string | null>> {
    const form = new FormData();
    form.append('userType', req.userType);
    form.append('fullName', req.fullName);
    form.append('username', req.username);
    form.append('password', req.password);
    form.append('confirmedPassword', req.confirmedPassword);
    form.append('phoneNumber', req.phoneNumber);
    form.append('languagePreference', req.languagePreference);

    if (req.email) form.append('email', req.email);
    if (req.studentCapacity != null) form.append('studentCapacity', String(req.studentCapacity));
    if (req.customSubject) form.append('customSubject', req.customSubject.trim());

    for (const id of req.subjectIds) form.append('subjectIds', String(id));

    if (req.idImage) form.append('idImage', req.idImage, req.idImage.name);

    return this.http.post<ApiResult<string | null>>(`${this.base}/Auth/sign-up`, form, {
      headers: { 'Accept-Language': lang },
    });
  }

  /**
   * PUT /api/teacher/{teacherId}/profile
   * Updates the editable profile fields (fullName, language, subjects, customSubject,
   * capacity package). SuperAdmin edits any teacher by route id. `lang` sets Accept-Language
   * so validation / capacity-approval messages come back in the interface language.
   */
  updateTeacher(
    teacherId: number,
    req: UpdateTeacherProfileRequest,
    lang: 'en' | 'ar' = 'en',
  ): Observable<TeacherProfile> {
    return this.http
      .put<ApiResult<TeacherProfile>>(`${this.base}/teacher/${teacherId}/profile`, req, {
        headers: { 'Accept-Language': lang },
      })
      .pipe(map((r) => r.data));
  }
}
