import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { NavbarComponent } from '../navbar/navbar.component';
import { SidebarComponent } from '../sidebar/sidebar.component';

/**
 * Shell for all authenticated pages. Owns sidebar collapse state and composes
 * sidebar + navbar around the routed content. Renders nothing business-specific.
 */
@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent, NavbarComponent],
  template: `
    <div class="layout">
      <app-sidebar [collapsed]="collapsed()" />
      <div class="content-area">
        <app-navbar (toggleSidebar)="collapsed.set(!collapsed())" />
        <main class="page-scroll">
          <router-outlet />
        </main>
      </div>
    </div>
  `,
  styles: [
    `
      .layout {
        display: flex;
        height: 100vh;
        overflow: hidden;
      }
      .content-area {
        flex: 1;
        display: flex;
        flex-direction: column;
        min-width: 0;
      }
      .page-scroll {
        flex: 1;
        overflow-y: auto;
        padding: 1.5rem;
        background: var(--edvanz-bg, #f1f5f9);
      }
    `,
  ],
})
export class MainLayoutComponent {
  protected readonly collapsed = signal(false);
}
