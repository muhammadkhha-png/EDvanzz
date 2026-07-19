import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { PaginatedResponse } from '../../../core/models/paginated-response.model';
import { TeacherListItem } from '../../../core/models/teacher.model';
import { ConfirmDialogService } from '../../../shared/components/confirm-dialog/confirm-dialog.service';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { TeacherService } from '../../../core/services/teacher.service';
import { ToastService } from '../../../core/services/toast.service';
import { SubscriptionStatusBadgeComponent } from '../subscription-panel/subscription-status-badge.component';

const DEFAULT_PAGE_SIZE = 10;

/** Teacher directory: search, paginate, navigate to details/edit, delete. */
@Component({
  selector: 'app-teacher-list',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    EmptyStateComponent,
    SubscriptionStatusBadgeComponent,
  ],
  template: `
    <div class="page-header">
      <div>
        <h2 class="h4 mb-1">Teachers</h2>
        <p class="text-muted mb-0">Manage teacher accounts and access.</p>
      </div>
      <a routerLink="/teachers/new" class="btn btn-primary">+ New teacher</a>
    </div>

    <div class="card">
      <div class="card-body">
        <div class="mb-3">
          <input
            type="search"
            class="form-control"
            placeholder="Search by name, email or phone…"
            [formControl]="searchControl"
          />
        </div>

        @if (page(); as p) {
          @if (p.data.length === 0) {
            <app-empty-state
              icon="🧑‍🏫"
              title="No teachers found"
              message="Try a different search, or add a new teacher."
            />
          } @else {
            <div class="table-responsive">
              <table class="table align-middle">
                <thead>
                  <tr>
                    <th>Name</th>
                    <th>Code</th>
                    <th>Phone</th>
                    <th>Subscription</th>
                    <th>Status</th>
                    <th class="text-end">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  @for (teacher of p.data; track teacher.id) {
                    <tr>
                      <td class="fw-medium">{{ teacher.fullName }}</td>
                      <td class="text-muted font-monospace">{{ teacher.teacherCode }}</td>
                      <td>{{ teacher.phoneNumber ?? '—' }}</td>
                      <td>
                        <app-subscription-status-badge
                          [status]="teacher.subscriptionStatus ?? ''"
                        />
                      </td>
                      <td>
                        <span
                          class="badge"
                          [class.text-bg-success]="teacher.accountStatus === 'Active'"
                          [class.text-bg-secondary]="teacher.accountStatus !== 'Active'"
                        >
                          {{ teacher.accountStatus }}
                        </span>
                      </td>
                      <td class="text-end text-nowrap">
                        <a
                          [routerLink]="['/teachers', teacher.id]"
                          class="btn btn-sm btn-outline-secondary"
                        >
                          View
                        </a>
                        <a
[routerLink]="['/teachers', teacher.id]"
  [queryParams]="{ edit: 1 }"
  class="btn btn-sm btn-outline-primary ms-1"
>
  Update
</a>
                       
                        @if (teacher.accountStatus === 'Active') {
  <button type="button" class="btn btn-sm btn-outline-warning ms-1" (click)="deactivate(teacher)">Deactivate</button>
} @else {
  <button type="button" class="btn btn-sm btn-outline-success ms-1" (click)="activate(teacher)">Activate</button>
}
<button type="button" class="btn btn-sm btn-outline-danger ms-1" (click)="softDelete(teacher)">Delete</button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>

            <div class="d-flex justify-content-between align-items-center pt-2">
              <small class="text-muted">
                Showing {{ p.data.length }} of {{ p.totalCount }} teachers
              </small>
              <div class="btn-group">
                <button
                  type="button"
                  class="btn btn-outline-secondary btn-sm"
                  [disabled]="p.page <= 1"
                  (click)="goToPage(p.page - 1)"
                >
                  Previous
                </button>
                <span class="btn btn-outline-secondary btn-sm disabled">
                  {{ p.page }} / {{ p.totalPages || 1 }}
                </span>
                <button
                  type="button"
                  class="btn btn-outline-secondary btn-sm"
                  [disabled]="p.page >= p.totalPages"
                  (click)="goToPage(p.page + 1)"
                >
                  Next
                </button>
              </div>
            </div>
          }
        }
      </div>
    </div>
  `,
  styles: [
    `
      .page-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 1rem;
        margin-bottom: 1.5rem;
        flex-wrap: wrap;
      }
      .card {
        border: 1px solid var(--edvanz-border, #e5e7eb);
        border-radius: 14px;
      }
    `,
  ],
})
export class TeacherListComponent implements OnInit {
  private readonly teacherService = inject(TeacherService);
  private readonly confirm = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  protected readonly searchControl = new FormControl<string>('', {
    nonNullable: true,
  });
  protected readonly page = signal<PaginatedResponse<TeacherListItem[]> | null>(null);
  private currentPage = 1;

  ngOnInit(): void {
    this.load();
    this.searchControl.valueChanges
      .pipe(debounceTime(350), distinctUntilChanged())
      .subscribe(() => {
        this.currentPage = 1;
        this.load();
      });
  }

  protected goToPage(page: number): void {
    this.currentPage = page;
    this.load();
  }

  protected async confirmDelete(teacher: TeacherListItem): Promise<void> {
    const ok = await this.confirm.open({
      title: 'Delete teacher',
      message: `Delete "${teacher.fullName}"? This action cannot be undone.`,
      confirmText: 'Delete',
      danger: true,
    });
    if (!ok) {
      return;
    }
this.toast.error('Delete endpoint not yet available on the backend.');
  }
protected async deactivate(t: TeacherListItem): Promise<void> {
  const ok = await this.confirm.open({
    title: 'Deactivate teacher',
    message: `Deactivate ${t.fullName}? They'll be signed out and unable to log in.`,
    confirmText: 'Deactivate', cancelText: 'Cancel',
  });
  if (!ok) return;
  this.teacherService.deactivateTeacher(t.id).subscribe(() => { this.toast.success('Teacher deactivated.'); this.load(); });
}

protected activate(t: TeacherListItem): void {
  this.teacherService.activateTeacher(t.id).subscribe(() => { this.toast.success('Teacher activated.'); this.load(); });
}

protected async softDelete(t: TeacherListItem): Promise<void> {
  const ok = await this.confirm.open({
    title: 'Delete teacher',
    message: `Soft-delete ${t.fullName}? They'll be removed from the list and signed out. Reversible via Activate.`,
    confirmText: 'Delete', cancelText: 'Cancel',
  });
  if (!ok) return;
  this.teacherService.softDeleteTeacher(t.id).subscribe(() => { this.toast.success('Teacher deleted.'); this.load(); });
}
  private load(): void {
    this.teacherService
      .getTeachers({
        page: this.currentPage,
        pageSize: DEFAULT_PAGE_SIZE,
        search: this.searchControl.value,
      })
      .subscribe((result) => this.page.set(result));
  }
}
