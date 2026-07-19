import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResult } from '../models/api-result.model';
import { PaginatedResponse } from '../models/paginated-response.model';
import {
  AdminSetCapacityRequest,
  CapacityAdjustResult,
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

  getTeachers(query: TeacherListQuery): Observable<PaginatedResponse<TeacherListItem[]>> {
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

  /** GET /api/teacher/{id}/profile — SuperAdmin reads any teacher by route id. */
  getTeacherById(teacherId: number): Observable<TeacherProfile> {
    return this.http
      .get<ApiResult<TeacherProfile>>(`${this.base}/teacher/${teacherId}/profile`)
      .pipe(map((r) => r.data));
  }

  getDashboardSummary(): Observable<DashboardSummary> {
    const tiny = { page: 1, pageSize: 1 };
    const get = (extra: TeacherListQuery) =>
      this.http
        .get<ApiResult<PaginatedResponse<TeacherListItem[]>>>(`${this.base}/teacher/list`, {
          params: new HttpParams({ fromObject: { ...tiny, ...extra } as Record<string, string | number> }),
        })
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

  /** GET /api/teacher/subjects — bilingual subject lookup. */
  getSubjects(lang: 'en' | 'ar' = 'en'): Observable<SubjectDto[]> {
    return this.http
      .get<ApiResult<SubjectDto[]>>(`${this.base}/teacher/subjects`, { headers: { 'Accept-Language': lang } })
      .pipe(map((r) => r.data ?? []));
  }

  /** GET /api/teacher/capacity-packages — the 7 active tiers. */
  getCapacityPackages(lang: 'en' | 'ar' = 'en'): Observable<StudentCapacityPackageDto[]> {
    return this.http
      .get<ApiResult<StudentCapacityPackageDto[]>>(`${this.base}/teacher/capacity-packages`, {
        headers: { 'Accept-Language': lang },
      })
      .pipe(map((r) => r.data ?? []));
  }

  /** POST /api/Auth/sign-up (multipart/form-data). Browser sets the boundary. */
  createTeacher(req: CreateTeacherSignUpRequest, lang: 'en' | 'ar' = 'en'): Observable<ApiResult<string | null>> {
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

  /** PUT /api/teacher/{id}/profile — editable profile fields (SuperAdmin edits any teacher). */
  updateTeacher(teacherId: number, req: UpdateTeacherProfileRequest, lang: 'en' | 'ar' = 'en'): Observable<TeacherProfile> {
    return this.http
      .put<ApiResult<TeacherProfile>>(`${this.base}/teacher/${teacherId}/profile`, req, {
        headers: { 'Accept-Language': lang },
      })
      .pipe(map((r) => r.data));
  }

  /** PUT /api/admin/subscriptions/teachers/{id}/capacity — SuperAdmin, increase-only. */
  adjustTeacherCapacity(
    teacherId: number,
    req: AdminSetCapacityRequest,
    lang: 'en' | 'ar' = 'en',
  ): Observable<CapacityAdjustResult> {
    return this.http
      .put<ApiResult<CapacityAdjustResult>>(
        `${this.base}/admin/subscriptions/teachers/${teacherId}/capacity`,
        req,
        { headers: { 'Accept-Language': lang } },
      )
      .pipe(map((r) => r.data));
  }

  // ── Account status (soft-delete via status; blocks login + forces sign-out) ────
  // All three PATCH endpoints flip Teacher.AccountStatus + User.IsActive and bump the
  // security stamp server-side, so any active session is invalidated immediately.

  /** PATCH /api/teacher/{id}/deactivate — AccountStatus = Inactive (reversible). */
  deactivateTeacher(teacherId: number, lang: 'en' | 'ar' = 'en'): Observable<string | null> {
    return this.http
      .patch<ApiResult<string | null>>(`${this.base}/teacher/${teacherId}/deactivate`, {}, {
        headers: { 'Accept-Language': lang },
      })
      .pipe(map((r) => r.data));
  }

  /** PATCH /api/teacher/{id}/activate — AccountStatus = Active (clears deactivation/soft-delete). */
  activateTeacher(teacherId: number, lang: 'en' | 'ar' = 'en'): Observable<string | null> {
    return this.http
      .patch<ApiResult<string | null>>(`${this.base}/teacher/${teacherId}/activate`, {}, {
        headers: { 'Accept-Language': lang },
      })
      .pipe(map((r) => r.data));
  }

  /** PATCH /api/teacher/{id}/delete — AccountStatus = Suspended (soft-delete; hidden from lists). */
  softDeleteTeacher(teacherId: number, lang: 'en' | 'ar' = 'en'): Observable<string | null> {
    return this.http
      .patch<ApiResult<string | null>>(`${this.base}/teacher/${teacherId}/delete`, {}, {
        headers: { 'Accept-Language': lang },
      })
      .pipe(map((r) => r.data));
  }
}
