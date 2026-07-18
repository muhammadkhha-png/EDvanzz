import { Component, inject } from '@angular/core';
import { ToastService } from '../../../core/services/toast.service';

/** Renders active toasts (bottom-right). Purely reactive to ToastService. */
@Component({
  selector: 'app-toast-container',
  standalone: true,
  template: `
    <div class="toast-stack" aria-live="polite" aria-atomic="true">
      @for (toast of toastService.toasts(); track toast.id) {
        <div class="toast-item" [class]="'toast-' + toast.variant" role="alert">
          <span class="toast-message">{{ toast.message }}</span>
          <button
            type="button"
            class="toast-close"
            aria-label="Dismiss"
            (click)="toastService.dismiss(toast.id)"
          >
            ×
          </button>
        </div>
      }
    </div>
  `,
  styles: [
    `
      .toast-stack {
        position: fixed;
        bottom: 1.25rem;
        right: 1.25rem;
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
        z-index: 1090;
        max-width: min(360px, 90vw);
      }
      .toast-item {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        padding: 0.75rem 1rem;
        border-radius: 10px;
        color: #fff;
        box-shadow: 0 8px 24px rgba(15, 23, 42, 0.18);
        animation: slide-in 0.2s ease-out;
      }
      .toast-message {
        flex: 1;
        font-size: 0.9rem;
      }
      .toast-close {
        background: none;
        border: none;
        color: inherit;
        font-size: 1.25rem;
        line-height: 1;
        cursor: pointer;
        opacity: 0.85;
      }
      .toast-success {
        background: #16a34a;
      }
      .toast-error {
        background: #dc2626;
      }
      .toast-warning {
        background: #d97706;
      }
      .toast-info {
        background: #2563eb;
      }
      @keyframes slide-in {
        from {
          transform: translateX(12px);
          opacity: 0;
        }
      }
    `,
  ],
})
export class ToastContainerComponent {
  protected readonly toastService = inject(ToastService);
}
