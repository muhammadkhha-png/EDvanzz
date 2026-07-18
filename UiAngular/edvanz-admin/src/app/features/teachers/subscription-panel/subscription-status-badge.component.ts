import { Component, computed, input } from '@angular/core';

/** Maps a subscription status string to a coloured Bootstrap badge. */
@Component({
  selector: 'app-subscription-status-badge',
  standalone: true,
  template: `
    <span class="badge" [style.background-color]="color()" [style.color]="'#fff'">
      {{ status() || '—' }}
    </span>
  `,
})
export class SubscriptionStatusBadgeComponent {
  readonly status = input.required<string>();

  protected readonly color = computed(() => {
    switch (this.status()) {
      case 'Active':       return '#16a34a';
      case 'ExpiringSoon': return '#d97706';
      case 'Expired':      return '#dc2626';
      case 'Cancelled':    return '#6b7280';
      case 'Pending':      return '#2563eb';
      default:             return '#9ca3af';
    }
  });
}
