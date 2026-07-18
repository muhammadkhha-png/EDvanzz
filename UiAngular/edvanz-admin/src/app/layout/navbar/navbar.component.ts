import { Component, inject, output } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

/** Top bar: sidebar toggle, breadcrumb, current user, logout. */
@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [BreadcrumbComponent],
  template: `
    <header class="navbar-shell">
      <div class="left">
        <button
          type="button"
          class="toggle-btn"
          aria-label="Toggle sidebar"
          (click)="toggleSidebar.emit()"
        >
          ☰
        </button>
        <app-breadcrumb />
      </div>
      <div class="right">
        <div class="user-block">
          <span class="user-avatar">{{ initials }}</span>
          <span class="user-name">{{ auth.currentUser()?.displayName }}</span>
        </div>
        <button type="button" class="btn btn-outline-secondary btn-sm" (click)="auth.logout()">
          Logout
        </button>
      </div>
    </header>
  `,
  styles: [
    `
      .navbar-shell {
        height: 60px;
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 0 1.25rem;
        background: #fff;
        border-bottom: 1px solid var(--edvanz-border, #e5e7eb);
      }
      .left,
      .right {
        display: flex;
        align-items: center;
        gap: 1rem;
      }
      .toggle-btn {
        background: none;
        border: none;
        font-size: 1.35rem;
        line-height: 1;
        cursor: pointer;
        color: var(--edvanz-muted, #475569);
      }
      .user-block {
        display: flex;
        align-items: center;
        gap: 0.5rem;
      }
      .user-avatar {
        display: grid;
        place-items: center;
        width: 34px;
        height: 34px;
        border-radius: 50%;
        background: var(--edvanz-primary, #2563eb);
        color: #fff;
        font-size: 0.85rem;
        font-weight: 600;
      }
      .user-name {
        font-weight: 500;
        font-size: 0.9rem;
      }
      @media (max-width: 576px) {
        .user-name {
          display: none;
        }
      }
    `,
  ],
})
export class NavbarComponent {
  protected readonly auth = inject(AuthService);
  readonly toggleSidebar = output<void>();

  protected get initials(): string {
    const name = this.auth.currentUser()?.displayName ?? 'A';
    return name
      .split(' ')
      .map((p) => p.charAt(0))
      .slice(0, 2)
      .join('')
      .toUpperCase();
  }
}
