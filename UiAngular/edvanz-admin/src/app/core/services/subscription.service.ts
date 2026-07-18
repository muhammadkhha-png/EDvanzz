import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResult } from '../models/api-result.model';
import {
  AdminActivateRequest,
  AdminExtendRequest,
  AdminPendingQueueItem,
  AdminSetEndDateRequest,
  CancelSubscriptionRequest,
  CurrentSubscriptionDto,
} from '../models/subscription.model';
import { TeacherSubscriptionDto } from '../models/teacher.model';
import { PaginatedResponse } from '../models/paginated-response.model';

@Injectable({ providedIn: 'root' })
export class SubscriptionService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  /**
   * GET /api/teacher/{teacherId}/subscription
   * Returns the teacher's current subscription (or null if none).
   */
  cancel(request: CancelSubscriptionRequest) {
  return this.http.post<CurrentSubscriptionDto>(
    `${this.base}/admin/subscriptions/cancel`,
    request
  );
}
  getByTeacher(teacherId: number): Observable<TeacherSubscriptionDto | null> {
    return this.http
      .get<ApiResult<TeacherSubscriptionDto | null>>(
        `${this.base}/teacher/${teacherId}/subscription`,
      )
      .pipe(map((r) => r.data));
  }

  /**
   * POST /api/admin/subscriptions/activate
   * Manually activates a subscription (SuperAdminOverride, no payment).
   */
  activate(request: AdminActivateRequest): Observable<CurrentSubscriptionDto> {
    return this.http
      .post<ApiResult<CurrentSubscriptionDto>>(
        `${this.base}/admin/subscriptions/activate`,
        request,
      )
      .pipe(map((r) => r.data));
  }

  /**
   * POST /api/admin/subscriptions/extend
   * Extends the current subscription end date by N days.
   */
  extend(request: AdminExtendRequest): Observable<CurrentSubscriptionDto> {
    return this.http
      .post<ApiResult<CurrentSubscriptionDto>>(
        `${this.base}/admin/subscriptions/extend`,
        request,
      )
      .pipe(map((r) => r.data));
  }

  /**
   * PUT /api/admin/subscriptions/end-date
   * Overrides the end date on a specific subscription row.
   */
  setEndDate(request: AdminSetEndDateRequest): Observable<CurrentSubscriptionDto> {
    return this.http
      .put<ApiResult<CurrentSubscriptionDto>>(
        `${this.base}/admin/subscriptions/end-date`,
        request,
      )
      .pipe(map((r) => r.data));
  }

  /**
   * GET /api/admin/subscriptions/pending
   * Paginated list of subscriptions awaiting SuperAdmin approval.
   */
  getPendingQueue(
    page = 1,
    pageSize = 20,
  ): Observable<PaginatedResponse<AdminPendingQueueItem[]>> {
    return this.http
      .get<ApiResult<PaginatedResponse<AdminPendingQueueItem[]>>>(
        `${this.base}/admin/subscriptions/pending`,
        { params: { page, pageSize } },
      )
      .pipe(map((r) => r.data));
  }

  /**
   * POST /api/admin/subscriptions/pending/{id}/approve
   * Approves a pending manual payment.
   */
  approvePending(pendingPaymentId: number): Observable<unknown> {
    return this.http
      .post<ApiResult<unknown>>(
        `${this.base}/admin/subscriptions/pending/${pendingPaymentId}/approve`,
        {},
      )
      .pipe(map((r) => r.data));
  }
}
