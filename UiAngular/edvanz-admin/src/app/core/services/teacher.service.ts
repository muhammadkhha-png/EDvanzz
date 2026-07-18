import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResult } from '../models/api-result.model';
import { PaginatedResponse } from '../models/paginated-response.model';
import {
  DashboardSummary,
  InitializeTeacherRequest,
  SignUpRequest,
  TeacherListItem,
  TeacherListQuery,
  TeacherProfile,
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
   * Full teacher detail including active subscription.
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
   * Create teacher: step 1 — POST /api/Auth/sign-up (creates the User record).
   * Returns the new userId needed for step 2.
   */
  signUp(request: SignUpRequest): Observable<{ userId: number }> {
    return this.http
      .post<ApiResult<{ userId: number }>>(`${this.base}/Auth/sign-up`, request)
      .pipe(map((r) => r.data));
  }

  /**
   * Create teacher: step 2 — POST /api/teacher/initialize
   * Creates Teacher + subjects + configuration row.
   */
  initializeTeacher(request: InitializeTeacherRequest): Observable<TeacherProfile> {
    return this.http
      .post<ApiResult<TeacherProfile>>(`${this.base}/teacher/initialize`, request)
      .pipe(map((r) => r.data));
  }
}
