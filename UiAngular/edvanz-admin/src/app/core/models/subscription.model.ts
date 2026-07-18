// ── Admin subscription endpoints (/api/admin/subscriptions/*) ────────────────

export interface CurrentSubscriptionDto {
  id: number;
  subscriptionStatus: string;
  startDate: string;
  endDate: string;
  daysRemaining: number;
}

/** POST /api/admin/subscriptions/activate */
export interface AdminActivateRequest {
  teacherId: number;
  startDate?: string | null;
  endDate?: string | null;
}

/** POST /api/admin/subscriptions/extend */
export interface AdminExtendRequest {
  teacherId: number;
  extensionDays: number;
}

/** PUT /api/admin/subscriptions/end-date */
export interface AdminSetEndDateRequest {
  subscriptionId: number;
  newEndDate: string;
}

// ── Pending payments queue ────────────────────────────────────────────────────
export interface AdminPendingQueueItem {
  id: number;
  teacherId: number;
  teacherName: string;
  amount: number;
  transactionReference?: string;
  phoneNumber?: string;
  createdAt: string;
}
