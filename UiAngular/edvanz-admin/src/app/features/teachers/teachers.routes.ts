import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';

/**
 * Teacher area. The details shell nests three tabbed child routes; the
 * subscription and modules tabs are the spec's "Subscriptions" and "Modules"
 * protected pages, each independently permission-guarded.
 */
export const TEACHERS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./teacher-list/teacher-list.component').then(
        (m) => m.TeacherListComponent,
      ),
    data: { breadcrumb: 'Teachers' },
  },
  {
    path: 'new',
    loadComponent: () =>
      import('./teacher-form/teacher-form.component').then(
        (m) => m.TeacherFormComponent,
      ),
    data: { breadcrumb: 'New teacher' },
  },
  {
    path: ':id/edit',
    loadComponent: () =>
      import('./teacher-form/teacher-form.component').then(
        (m) => m.TeacherFormComponent,
      ),
    data: { breadcrumb: 'Edit teacher' },
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./teacher-details/teacher-details.component').then(
        (m) => m.TeacherDetailsComponent,
      ),
    data: { breadcrumb: 'Teacher details' },
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./teacher-details/teacher-info.component').then(
            (m) => m.TeacherInfoComponent,
          ),
      },
      {
        path: 'subscription',
        canActivate: [permissionGuard],
        data: { breadcrumb: 'Subscription', roles: ['SuperAdmin'] },
        loadComponent: () =>
          import('./subscription-panel/subscription-panel.component').then(
            (m) => m.SubscriptionPanelComponent,
          ),
      },
      {
        path: 'modules',
        canActivate: [permissionGuard],
        data: { breadcrumb: 'Modules', roles: ['SuperAdmin'] },
        loadComponent: () =>
          import('./modules-panel/modules-panel.component').then(
            (m) => m.ModulesPanelComponent,
          ),
      },
    ],
  },
];
