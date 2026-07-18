import { Component, Input } from '@angular/core';

/** Reusable "nothing here" placeholder for empty lists / no search results. */
@Component({
  selector: 'app-empty-state',
  standalone: true,
  template: `
    <div class="empty-state text-center py-5">
      <div class="empty-icon">{{ icon }}</div>
      <h5 class="mt-3 mb-1">{{ title }}</h5>
      <p class="text-muted mb-3">{{ message }}</p>
      <ng-content></ng-content>
    </div>
  `,
  styles: [
    `
      .empty-icon {
        font-size: 2.75rem;
        line-height: 1;
      }
      .empty-state {
        color: var(--edvanz-muted, #6b7280);
      }
    `,
  ],
})
export class EmptyStateComponent {
  @Input() icon = '📭';
  @Input() title = 'Nothing to show';
  @Input() message = 'There is no data to display yet.';
}
