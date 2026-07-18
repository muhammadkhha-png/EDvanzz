import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DashboardSummary } from '../../core/models/teacher.model';
import { TeacherService } from '../../core/services/teacher.service';

interface StatCard {
  label: string;
  value: number;
  icon: string;
  accent: string;
}

/** Landing dashboard: four responsive KPI cards from a single summary call. */
@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="page-header">
      <div>
        <h2 class="h4 mb-1">Dashboard</h2>
        <p class="text-muted mb-0">Overview of teachers and subscriptions.</p>
      </div>
      <a routerLink="/teachers" class="btn btn-primary">Manage teachers</a>
    </div>

    <div class="stat-grid">
      @for (card of cards(); track card.label) {
        <div class="stat-card">
          <div class="stat-icon" [style.background]="card.accent">
            {{ card.icon }}
          </div>
          <div>
            <div class="stat-value">{{ card.value }}</div>
            <div class="stat-label">{{ card.label }}</div>
          </div>
        </div>
      }
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
      .stat-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
        gap: 1rem;
      }
      .stat-card {
        display: flex;
        align-items: center;
        gap: 1rem;
        background: #fff;
        border: 1px solid var(--edvanz-border, #e5e7eb);
        border-radius: 14px;
        padding: 1.25rem;
      }
      .stat-icon {
        display: grid;
        place-items: center;
        width: 52px;
        height: 52px;
        border-radius: 12px;
        font-size: 1.4rem;
      }
      .stat-value {
        font-size: 1.75rem;
        font-weight: 700;
        line-height: 1;
      }
      .stat-label {
        color: var(--edvanz-muted, #6b7280);
        font-size: 0.9rem;
        margin-top: 0.25rem;
      }
    `,
  ],
})
export class DashboardComponent implements OnInit {
  private readonly teacherService = inject(TeacherService);
  private readonly summary = signal<DashboardSummary>({
    totalTeachers: 0,
    activeTeachers: 0,
    expiredSubscriptions: 0,
    expiringSoon: 0,
  });

  protected readonly cards = signal<StatCard[]>([]);

  ngOnInit(): void {
    this.teacherService.getDashboardSummary().subscribe((data) => {
      this.summary.set(data);
      this.cards.set(this.toCards(data));
    });
  }

  private toCards(s: DashboardSummary): StatCard[] {
    return [
      { label: 'Total Teachers', value: s.totalTeachers, icon: '🧑‍🏫', accent: '#dbeafe' },
      { label: 'Active Teachers', value: s.activeTeachers, icon: '✅', accent: '#dcfce7' },
      { label: 'Expired Subscriptions', value: s.expiredSubscriptions, icon: '⛔', accent: '#fee2e2' },
      { label: 'Expiring Soon', value: s.expiringSoon, icon: '⏳', accent: '#fef3c7' },
    ];
  }
}
