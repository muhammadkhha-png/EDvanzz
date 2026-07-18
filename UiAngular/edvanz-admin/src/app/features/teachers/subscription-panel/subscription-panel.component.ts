import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { CurrentSubscriptionDto } from '../../../core/models/subscription.model';
import { TeacherSubscriptionDto } from '../../../core/models/teacher.model';
import { SubscriptionService } from '../../../core/services/subscription.service';
import { ToastService } from '../../../core/services/toast.service';
import { ConfirmDialogService } from '../../../shared/components/confirm-dialog/confirm-dialog.service';
import { SubscriptionStatusBadgeComponent } from './subscription-status-badge.component';

/**
 * Subscription tab for one teacher. Every mutation returns the fresh
 * subscription from the server, so `remainingDays` and `status` are never
 * recomputed client-side — the backend stays the single source of truth.
 */
@Component({
  selector: 'app-subscription-panel',
  standalone: true,
  imports: [ReactiveFormsModule, DatePipe, SubscriptionStatusBadgeComponent],
  template: `
    @if (subscription(); as sub) {
      <div class="row g-4">
        <div class="col-lg-5">
          <div class="summary">
            <div class="summary-row">
              <span class="label">Status</span>
              <app-subscription-status-badge [status]="sub.subscriptionStatus" />
            </div>
            <div class="summary-row">
              <span class="label">Start date</span>
              <span>{{ sub.startDate ? (sub.startDate | date: 'mediumDate') : '—' }}</span>
            </div>
            <div class="summary-row">
              <span class="label">End date</span>
              <span>{{ sub.endDate ? (sub.endDate | date: 'mediumDate') : '—' }}</span>
            </div>
            <div class="summary-row">
              <span class="label">Remaining days</span>
              <span class="fw-semibold">{{ sub.daysRemaining }}</span>
            </div>
          </div>
        </div>

        <div class="col-lg-7">
          <div class="actions">
            @if (sub.subscriptionStatus === 'Pending' || sub.subscriptionStatus === 'Cancelled' || sub.subscriptionStatus === 'Expired') {
              <form [formGroup]="activateForm" (ngSubmit)="activate()" class="action-card">
                <h6>Activate subscription</h6>
                <div class="row g-2">
                  <div class="col-sm-6">
                    <label class="form-label">Start date</label>
                    <input type="date" class="form-control" formControlName="startDate" />
                  </div>
                  <div class="col-sm-6">
                    <label class="form-label">End date</label>
                    <input type="date" class="form-control" formControlName="endDate" />
                  </div>
                </div>
                <button type="submit" class="btn btn-success btn-sm mt-2" [disabled]="activateForm.invalid">
                  Activate
                </button>
              </form>
            }

            @if (sub.subscriptionStatus === 'Active' || sub.subscriptionStatus === 'ExpiringSoon') {
              <form [formGroup]="extendForm" (ngSubmit)="extend()" class="action-card">
                <h6>Extend by days</h6>
                <div class="input-group">
                  <input type="number" min="1" class="form-control" formControlName="days" />
                  <button type="submit" class="btn btn-primary" [disabled]="extendForm.invalid">
                    Extend
                  </button>
                </div>
              </form>

              <form [formGroup]="endDateForm" (ngSubmit)="updateEndDate()" class="action-card">
                <h6>Update end date</h6>
                <div class="input-group">
                  <input type="date" class="form-control" formControlName="endDate" />
                  <button type="submit" class="btn btn-primary" [disabled]="endDateForm.invalid">
                    Update
                  </button>
                </div>
              </form>

              <div class="action-card">
                <h6>Cancel subscription</h6>
                <p class="text-muted small mb-2">
                  Immediately revokes access for this teacher.
                </p>
                <button type="button" class="btn btn-outline-danger btn-sm" (click)="cancel()">
                  Cancel subscription
                </button>
              </div>
            }
          </div>
        </div>
      </div>
    }
  `,
  styles: [
    `
      .summary {
        border: 1px solid var(--edvanz-border, #e5e7eb);
        border-radius: 12px;
        padding: 1.25rem;
      }
      .summary-row {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 0.6rem 0;
        border-bottom: 1px solid var(--edvanz-border, #eef2f7);
      }
      .summary-row:last-child {
        border-bottom: none;
      }
      .label {
        color: var(--edvanz-muted, #6b7280);
        font-size: 0.9rem;
      }
      .actions {
        display: flex;
        flex-direction: column;
        gap: 1rem;
      }
      .action-card {
        border: 1px solid var(--edvanz-border, #e5e7eb);
        border-radius: 12px;
        padding: 1rem 1.25rem;
      }
      .action-card h6 {
        margin-bottom: 0.75rem;
      }
    `,
  ],
})
export class SubscriptionPanelComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly subscriptionService = inject(SubscriptionService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmDialogService);

  private teacherId!: number;
  private subscriptionId: number | null = null;

  protected readonly subscription = signal<TeacherSubscriptionDto | null>(null);

  protected readonly activateForm = this.fb.nonNullable.group({
    startDate: [''],
    endDate: [''],
  });
  protected readonly extendForm = this.fb.nonNullable.group({
    days: [30, [Validators.required, Validators.min(1)]],
  });
  protected readonly endDateForm = this.fb.nonNullable.group({
    endDate: ['', Validators.required],
  });

  ngOnInit(): void {
    this.teacherId = +(this.route.parent!.snapshot.paramMap.get('id') ?? '0');
    this.load();
  }

  protected activate(): void {
    const { startDate, endDate } = this.activateForm.getRawValue();
    this.subscriptionService
      .activate({
        teacherId: this.teacherId,
        startDate: startDate ? this.toUtc(startDate) : null,
        endDate:   endDate   ? this.toUtc(endDate)   : null,
      })
      .subscribe((sub) => this.applyResult(sub, 'Subscription activated.'));
  }

  protected extend(): void {
    if (this.extendForm.invalid) return;
    this.subscriptionService
      .extend({ teacherId: this.teacherId, extensionDays: this.extendForm.getRawValue().days })
      .subscribe((sub) => this.applyResult(sub, 'Subscription extended.'));
  }

  protected updateEndDate(): void {
    if (this.endDateForm.invalid || !this.subscriptionId) return;
    this.subscriptionService
      .setEndDate({
        subscriptionId: this.subscriptionId,
        newEndDate: this.toUtc(this.endDateForm.getRawValue().endDate),
      })
      .subscribe((sub) => this.applyResult(sub, 'End date updated.'));
  }

  protected async cancel(): Promise<void> {
    // ⚠ POST /api/admin/subscriptions/cancel does not exist yet on the backend.
    this.toast.error('Cancel endpoint not yet available on the backend.');
  }

  private applyResult(sub: CurrentSubscriptionDto, message: string): void {
    this.subscription.set(sub);
    this.subscriptionId = sub?.id ?? null;
    this.toast.success(message);
  }

  private load(): void {
    this.subscriptionService
      .getByTeacher(this.teacherId)
      .subscribe((sub) => {
        this.subscription.set(sub);
        this.subscriptionId = sub?.id ?? null;
      });
  }

  /** Treats the date-input value (yyyy-MM-dd) as a UTC instant. */
  private toUtc(dateInput: string): string {
    return new Date(`${dateInput}T00:00:00Z`).toISOString();
  }
}
