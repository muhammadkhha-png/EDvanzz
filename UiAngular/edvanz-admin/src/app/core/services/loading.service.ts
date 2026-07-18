import { computed, Injectable, signal } from '@angular/core';

/**
 * Ref-counted loading state. The auth/error interceptors bump the counter on
 * each in-flight HTTP request; the global spinner shows while count > 0.
 * Counting (rather than a boolean) keeps concurrent requests correct.
 */
@Injectable({ providedIn: 'root' })
export class LoadingService {
  private readonly inFlight = signal(0);
  readonly isLoading = computed(() => this.inFlight() > 0);

  start(): void {
    this.inFlight.update((n) => n + 1);
  }

  stop(): void {
    this.inFlight.update((n) => Math.max(0, n - 1));
  }
}
