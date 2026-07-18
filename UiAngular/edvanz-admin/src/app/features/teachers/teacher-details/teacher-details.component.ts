import { Component, inject, OnInit, signal } from '@angular/core';
import {
  ActivatedRoute,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from '@angular/router';
import { TeacherProfile } from '../../../core/models/teacher.model';
import { TeacherService } from '../../../core/services/teacher.service';
import { SubscriptionStatusBadgeComponent } from '../subscription-panel/subscription-status-badge.component';

/**
 * Shell for a single teacher. Loads the header once and hosts three tabbed
 * child routes (info / subscription / modules), each an independently guarded
 * URL. Child panels own their own data + actions — including the inline edit
 * on the Details (info) tab, so the shell holds no form of its own.
 */
@Component({
  selector: 'app-teacher-details',
  standalone: true,
  imports: [
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    SubscriptionStatusBadgeComponent,
  ],
  template: `
    <div class="page-header">
      <a routerLink="/teachers" class="btn btn-outline-secondary btn-sm mb-2">
        ← Back to teachers
      </a>
    </div>

    @if (teacher(); as t) {
      <div class="detail-card">
        <div class="detail-head">
          <div class="avatar">{{ initials(t.fullName) }}</div>
          <div class="flex-grow-1">
            <h2 class="h4 mb-1">{{ t.fullName }}</h2>
            <div class="text-muted">{{ t.email }} · {{ t.phoneNumber }}</div>
          </div>
          <app-subscription-status-badge [status]="t.activeSubscription?.subscriptionStatus ?? ''" />
        </div>

        <ul class="nav nav-tabs px-3">
          <li class="nav-item">
            <a
              class="nav-link"
              [routerLink]="['/teachers', t.id]"
              routerLinkActive="active"
              [routerLinkActiveOptions]="{ exact: true }"
            >
              Details
            </a>
          </li>
          <li class="nav-item">
            <a
              class="nav-link"
              [routerLink]="['/teachers', t.id, 'subscription']"
              routerLinkActive="active"
            >
              Subscription
            </a>
          </li>
          <li class="nav-item">
            <a
              class="nav-link"
              [routerLink]="['/teachers', t.id, 'modules']"
              routerLinkActive="active"
            >
              Modules
            </a>
          </li>
        </ul>

        <div class="tab-body">
          <router-outlet />
        </div>
      </div>
    }
  `,
  styles: [
    `
      .detail-card {
        background: #fff;
        border: 1px solid var(--edvanz-border, #e5e7eb);
        border-radius: 14px;
        overflow: hidden;
      }
      .detail-head {
        display: flex;
        align-items: center;
        gap: 1rem;
        padding: 1.5rem;
        border-bottom: 1px solid var(--edvanz-border, #e5e7eb);
      }
      .avatar {
        display: grid;
        place-items: center;
        width: 56px;
        height: 56px;
        border-radius: 50%;
        background: var(--edvanz-primary, #2563eb);
        color: #fff;
        font-weight: 700;
        font-size: 1.2rem;
      }
      .tab-body {
        padding: 1.5rem;
      }
    `,
  ],
})
export class TeacherDetailsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly teacherService = inject(TeacherService);

  protected readonly teacher = signal<TeacherProfile | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.teacherService.getTeacherById(+id).subscribe((t) => this.teacher.set(t));
    }
  }

  protected initials(name: string): string {
    return name
      .split(' ')
      .map((p) => p.charAt(0))
      .slice(0, 2)
      .join('')
      .toUpperCase();
  }
}
