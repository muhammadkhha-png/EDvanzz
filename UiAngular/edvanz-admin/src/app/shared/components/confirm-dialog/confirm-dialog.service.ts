import { Injectable, signal } from '@angular/core';

export interface ConfirmOptions {
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  /** Danger styling for destructive actions (delete/cancel). */
  danger?: boolean;
}

interface ConfirmState extends ConfirmOptions {
  open: boolean;
  resolve?: (value: boolean) => void;
}

/**
 * Imperative confirm dialog. Components call `open(...)` and `await` a boolean,
 * keeping call sites linear (no template wiring per confirmation).
 */
@Injectable({ providedIn: 'root' })
export class ConfirmDialogService {
  readonly state = signal<ConfirmState>({ open: false, title: '', message: '' });

  open(options: ConfirmOptions): Promise<boolean> {
    return new Promise<boolean>((resolve) => {
      this.state.set({
        open: true,
        confirmText: 'Confirm',
        cancelText: 'Cancel',
        danger: false,
        ...options,
        resolve,
      });
    });
  }

  respond(confirmed: boolean): void {
    const current = this.state();
    current.resolve?.(confirmed);
    this.state.set({ open: false, title: '', message: '' });
  }
}
