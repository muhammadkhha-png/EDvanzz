import { Component, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';

/** Super Admin sign-in. Delegates the HTTP exchange entirely to AuthService. */
@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <div class="login-wrapper">
      <div class="login-card">
        <div class="login-brand">
          <span class="brand-mark">E</span>
          <div>
            <h1 class="h4 mb-0">Edvanz Admin</h1>
            <small class="text-muted">Subscription Management Portal</small>
          </div>
        </div>

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <div class="mb-3">
            <label class="form-label" for="userName">Username</label>
            <input
              id="userName"
              type="text"
              class="form-control"
              formControlName="userName"
              autocomplete="username"
              [class.is-invalid]="isInvalid('userName')"
            />
            @if (isInvalid('userName')) {
              <div class="invalid-feedback">Username is required.</div>
            }
          </div>

          <div class="mb-3">
            <label class="form-label" for="password">Password</label>
            <input
              id="password"
              type="password"
              class="form-control"
              formControlName="password"
              autocomplete="current-password"
              [class.is-invalid]="isInvalid('password')"
            />
            @if (isInvalid('password')) {
              <div class="invalid-feedback">Password is required.</div>
            }
          </div>

          <button
            type="submit"
            class="btn btn-primary w-100"
            [disabled]="submitting()"
          >
            {{ submitting() ? 'Signing in…' : 'Sign in' }}
          </button>
        </form>
      </div>
    </div>
  `,
  styles: [
    `
      .login-wrapper {
        min-height: 100vh;
        display: grid;
        place-items: center;
        background: linear-gradient(135deg, #1e3a8a, #2563eb);
        padding: 1rem;
      }
      .login-card {
        width: min(420px, 100%);
        background: #fff;
        border-radius: 16px;
        padding: 2rem;
        box-shadow: 0 24px 60px rgba(15, 23, 42, 0.28);
      }
      .login-brand {
        display: flex;
        align-items: center;
        gap: 0.85rem;
        margin-bottom: 1.75rem;
      }
      .brand-mark {
        display: grid;
        place-items: center;
        width: 46px;
        height: 46px;
        border-radius: 12px;
        background: var(--edvanz-primary, #2563eb);
        color: #fff;
        font-weight: 700;
        font-size: 1.3rem;
      }
    `,
  ],
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly toast = inject(ToastService);

  protected readonly submitting = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    userName: ['', [Validators.required]],
    password: ['', [Validators.required]],
  });

  protected isInvalid(control: 'userName' | 'password'): boolean {
    const c = this.form.controls[control];
    return c.invalid && (c.touched || c.dirty);
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.auth.login(this.form.getRawValue()).subscribe({
      next: (user) => {
        this.toast.success(`Welcome back, ${user.displayName}.`);
        const returnUrl =
          this.route.snapshot.queryParamMap.get('returnUrl') ?? '/dashboard';
        void this.router.navigateByUrl(returnUrl);
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        // HTTP errors are already toasted by the error interceptor; this
        // covers non-HTTP failures thrown in the auth pipe (e.g. a login
        // response with no usable token) so they don't fail silently.
        if (!(err instanceof HttpErrorResponse)) {
          this.toast.error(
            err instanceof Error ? err.message : 'Login failed.',
          );
        }
      },
    });
  }
}
