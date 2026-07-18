import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ConfirmDialogComponent } from './shared/components/confirm-dialog/confirm-dialog.component';
import { LoadingSpinnerComponent } from './shared/components/loading-spinner/loading-spinner.component';
import { ToastContainerComponent } from './shared/components/toast-container/toast-container.component';

/**
 * Application root. Hosts the router outlet plus the three app-level overlays
 * (spinner, toasts, confirm dialog) mounted exactly once above all pages.
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    RouterOutlet,
    LoadingSpinnerComponent,
    ToastContainerComponent,
    ConfirmDialogComponent,
  ],
  template: `
    <router-outlet />
    <app-loading-spinner />
    <app-toast-container />
    <app-confirm-dialog />
  `,
})
export class AppComponent {}
