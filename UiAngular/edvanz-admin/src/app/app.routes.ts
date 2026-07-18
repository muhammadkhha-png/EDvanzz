import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { loginGuard } from './core/guards/login.guard';

/**
 * Top-level routing. Public login sits outside the layout; everything else is
 * wrapped in the authenticated shell behind `authGuard`. Features are lazy.
 */
export const APP_ROUTES: Routes = [
  {
    path: 'login',
    canActivate: [loginGuard],
    loadComponent: () =>
      import('./features/auth/login/login.component').then(
        (m) => m.LoginComponent,
      ),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./layout/main-layout/main-layout.component').then(
        (m) => m.MainLayoutComponent,
      ),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        data: { breadcrumb: 'Dashboard' },
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then(
            (m) => m.DashboardComponent,
          ),
      },
      {
        path: 'teachers',
        loadChildren: () =>
          import('./features/teachers/teachers.routes').then(
            (m) => m.TEACHERS_ROUTES,
          ),
      },
    ],
  },
  {
    path: 'forbidden',
    loadComponent: () =>
      import('./shared/components/status-page.component').then(
        (m) => m.StatusPageComponent,
      ),
    data: {
      code: '403',
      title: 'Access denied',
      message: 'You do not have permission to view this page.',
    },
  },
  {
    path: '**',
    loadComponent: () =>
      import('./shared/components/status-page.component').then(
        (m) => m.StatusPageComponent,
      ),
    data: {
      code: '404',
      title: 'Page not found',
      message: 'The page you are looking for does not exist.',
    },
  },
];
