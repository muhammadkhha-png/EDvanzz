import { Component, input } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

interface NavItem {
  label: string;
  icon: string;
  route: string;
}

/** Responsive left navigation. Collapse state is owned by the parent layout. */
@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <aside class="sidebar" [class.collapsed]="collapsed()">
      <div class="brand">
        <span class="brand-mark">E</span>
        @if (!collapsed()) {
          <span class="brand-text">Edvanz Admin</span>
        }
      </div>
      <nav class="nav-list">
        @for (item of items; track item.route) {
          <a
            class="nav-link"
            [routerLink]="item.route"
            routerLinkActive="active"
            [title]="item.label"
          >
            <span class="nav-icon">{{ item.icon }}</span>
            @if (!collapsed()) {
              <span class="nav-label">{{ item.label }}</span>
            }
          </a>
        }
      </nav>
    </aside>
  `,
  styles: [
    `
      .sidebar {
        width: 240px;
        background: var(--edvanz-sidebar, #0f172a);
        color: #e2e8f0;
        display: flex;
        flex-direction: column;
        transition: width 0.2s ease;
        flex-shrink: 0;
      }
      .sidebar.collapsed {
        width: 72px;
      }
      .brand {
        display: flex;
        align-items: center;
        gap: 0.65rem;
        padding: 1.15rem 1rem;
        font-weight: 700;
        font-size: 1.05rem;
      }
      .brand-mark {
        display: grid;
        place-items: center;
        width: 32px;
        height: 32px;
        border-radius: 8px;
        background: var(--edvanz-primary, #2563eb);
        color: #fff;
        flex-shrink: 0;
      }
      .nav-list {
        display: flex;
        flex-direction: column;
        gap: 0.15rem;
        padding: 0.5rem;
      }
      .nav-link {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        padding: 0.65rem 0.75rem;
        border-radius: 8px;
        color: #cbd5e1;
        text-decoration: none;
        white-space: nowrap;
      }
      .nav-link:hover {
        background: rgba(255, 255, 255, 0.06);
        color: #fff;
      }
      .nav-link.active {
        background: var(--edvanz-primary, #2563eb);
        color: #fff;
      }
      .nav-icon {
        font-size: 1.1rem;
        width: 1.25rem;
        text-align: center;
      }
    `,
  ],
})
export class SidebarComponent {
  readonly collapsed = input(false);

  // Emoji icons keep the shell dependency-free (no icon-font package).
  protected readonly items: NavItem[] = [
    { label: 'Dashboard', icon: '📊', route: '/dashboard' },
    { label: 'Teachers', icon: '🧑‍🏫', route: '/teachers' },
  ];
}
