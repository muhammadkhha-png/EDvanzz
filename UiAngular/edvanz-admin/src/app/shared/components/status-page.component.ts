import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

/** Reusable full-page status screen (403 / 404). Content comes from route data. */
@Component({
  selector: 'app-status-page',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="status-wrapper">
      <div class="status-code">{{ data['code'] }}</div>
      <h1 class="h4">{{ data['title'] }}</h1>
      <p class="text-muted">{{ data['message'] }}</p>
      <a routerLink="/dashboard" class="btn btn-primary">Back to dashboard</a>
    </div>
  `,
  styles: [
    `
      .status-wrapper {
        min-height: 100vh;
        display: grid;
        place-content: center;
        text-align: center;
        gap: 0.5rem;
        padding: 2rem;
      }
      .status-code {
        font-size: 4.5rem;
        font-weight: 800;
        color: var(--edvanz-primary, #2563eb);
        line-height: 1;
      }
    `,
  ],
})
export class StatusPageComponent {
  private readonly route = inject(ActivatedRoute);
  protected readonly data = this.route.snapshot.data as Record<string, string>;
}
