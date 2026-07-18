import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  ActivatedRoute,
  NavigationEnd,
  Router,
  RouterLink,
} from '@angular/router';
import { filter, map, startWith } from 'rxjs';

interface Crumb {
  label: string;
  url: string;
}

/**
 * Builds breadcrumbs from `route.data.breadcrumb` on each activated segment.
 * Purely presentational — reads route config, never business state.
 */
@Component({
  selector: 'app-breadcrumb',
  standalone: true,
  imports: [RouterLink],
  template: `
    <nav aria-label="breadcrumb">
      <ol class="breadcrumb mb-0">
        @for (crumb of crumbs(); track crumb.url; let last = $last) {
          @if (last) {
            <li class="breadcrumb-item active" aria-current="page">
              {{ crumb.label }}
            </li>
          } @else {
            <li class="breadcrumb-item">
              <a [routerLink]="crumb.url">{{ crumb.label }}</a>
            </li>
          }
        }
      </ol>
    </nav>
  `,
})
export class BreadcrumbComponent {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly crumbs = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      startWith(null),
      map(() => this.build(this.route.root)),
    ),
    { initialValue: [] as Crumb[] },
  );

  private build(root: ActivatedRoute, url = '', acc: Crumb[] = []): Crumb[] {
    const child = root.firstChild;
    // Guard: snapshot may be undefined during the very first render tick
    // before the router has fully populated the activated route tree.
    if (!child || !child.snapshot) {
      return acc;
    }
    const segment = (child.snapshot.url ?? []).map((s) => s.path).join('/');
    const nextUrl = segment ? `${url}/${segment}` : url;
    const label = child.snapshot.data?.['breadcrumb'] as string | undefined;
    if (label) {
      acc.push({ label, url: nextUrl });
    }
    return this.build(child, nextUrl, acc);
  }
}
