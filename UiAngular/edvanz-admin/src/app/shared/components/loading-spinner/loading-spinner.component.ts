import { Component, inject } from '@angular/core';
import { LoadingService } from '../../../core/services/loading.service';

/** Full-screen overlay spinner shown while any HTTP request is in flight. */
@Component({
  selector: 'app-loading-spinner',
  standalone: true,
  template: `
    @if (loading.isLoading()) {
      <div class="loading-overlay" role="status" aria-live="polite">
        <div class="spinner"></div>
        <span class="visually-hidden">Loading…</span>
      </div>
    }
  `,
  styles: [
    `
      .loading-overlay {
        position: fixed;
        inset: 0;
        display: grid;
        place-items: center;
        background: rgba(255, 255, 255, 0.55);
        backdrop-filter: blur(1px);
        z-index: 1080;
      }
      .spinner {
        width: 44px;
        height: 44px;
        border: 4px solid var(--edvanz-border, #dfe3ea);
        border-top-color: var(--edvanz-primary, #2563eb);
        border-radius: 50%;
        animation: spin 0.7s linear infinite;
      }
      @keyframes spin {
        to {
          transform: rotate(360deg);
        }
      }
    `,
  ],
})
export class LoadingSpinnerComponent {
  protected readonly loading = inject(LoadingService);
}
