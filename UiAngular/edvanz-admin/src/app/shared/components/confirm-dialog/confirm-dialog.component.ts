import { Component, inject } from '@angular/core';
import { ConfirmDialogService } from './confirm-dialog.service';

/** Single app-level modal wired to ConfirmDialogService. Mounted once. */
@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  template: `
    @if (dialog.state().open) {
      <div class="confirm-backdrop" (click)="dialog.respond(false)"></div>
      <div class="confirm-modal" role="dialog" aria-modal="true">
        <h5 class="confirm-title">{{ dialog.state().title }}</h5>
        <p class="confirm-message">{{ dialog.state().message }}</p>
        <div class="confirm-actions">
          <button
            type="button"
            class="btn btn-outline-secondary"
            (click)="dialog.respond(false)"
          >
            {{ dialog.state().cancelText }}
          </button>
          <button
            type="button"
            class="btn"
            [class.btn-danger]="dialog.state().danger"
            [class.btn-primary]="!dialog.state().danger"
            (click)="dialog.respond(true)"
          >
            {{ dialog.state().confirmText }}
          </button>
        </div>
      </div>
    }
  `,
  styles: [
    `
      .confirm-backdrop {
        position: fixed;
        inset: 0;
        background: rgba(15, 23, 42, 0.45);
        z-index: 1085;
      }
      .confirm-modal {
        position: fixed;
        top: 50%;
        left: 50%;
        transform: translate(-50%, -50%);
        background: #fff;
        border-radius: 14px;
        padding: 1.5rem;
        width: min(440px, 92vw);
        box-shadow: 0 24px 60px rgba(15, 23, 42, 0.28);
        z-index: 1086;
      }
      .confirm-title {
        margin: 0 0 0.5rem;
        font-weight: 600;
      }
      .confirm-message {
        color: var(--edvanz-muted, #6b7280);
        margin-bottom: 1.25rem;
      }
      .confirm-actions {
        display: flex;
        justify-content: flex-end;
        gap: 0.5rem;
      }
    `,
  ],
})
export class ConfirmDialogComponent {
  protected readonly dialog = inject(ConfirmDialogService);
}
