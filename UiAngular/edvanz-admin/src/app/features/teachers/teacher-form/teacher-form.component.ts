import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { SignUpRequest, InitializeTeacherRequest } from '../../../core/models/teacher.model';
import { TeacherService } from '../../../core/services/teacher.service';
import { ToastService } from '../../../core/services/toast.service';
import { switchMap } from 'rxjs';

/**
 * Create teacher form.
 * Flow: POST /api/Auth/sign-up → POST /api/teacher/initialize.
 * Edit mode is read-only for now — no confirmed backend update endpoint.
 *
 * ⚠ FLAG (deactivate / delete): PATCH /api/teacher/{id}/deactivate and
 *   DELETE /api/teacher/{id} do not exist yet. Ask Belal to confirm routes.
 *   When added, inject TeacherService here and add buttons in edit mode.
 */
@Component({
  selector: 'app-teacher-form',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="page-header">
      <h2 class="h4 mb-0">{{ isEdit() ? 'Teacher details' : 'New teacher' }}</h2>
      <a routerLink="/teachers" class="btn btn-outline-secondary">Back to list</a>
    </div>

    <div class="card">
      <div class="card-body">
        @if (isEdit()) {
          <p class="text-muted">
            Edit functionality requires a PUT /api/teacher/&#123;id&#125;/profile endpoint.
            Use the Subscription and Modules tabs to manage access.
          </p>
        }
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <div class="row g-3">
            <div class="col-md-6">
              <label class="form-label" for="fullName">Full name *</label>
              <input id="fullName" class="form-control" formControlName="fullName"
                [class.is-invalid]="invalid('fullName')" />
              @if (invalid('fullName')) {
                <div class="invalid-feedback">Full name is required.</div>
              }
            </div>

            <div class="col-md-6">
              <label class="form-label" for="userName">Username *</label>
              <input id="userName" class="form-control" formControlName="userName"
                [class.is-invalid]="invalid('userName')" />
              @if (invalid('userName')) {
                <div class="invalid-feedback">Username is required.</div>
              }
            </div>

            <div class="col-md-6">
              <label class="form-label" for="email">Email</label>
              <input id="email" type="email" class="form-control" formControlName="email"
                [class.is-invalid]="invalid('email')" />
              @if (invalid('email')) {
                <div class="invalid-feedback">Enter a valid email address.</div>
              }
            </div>

            <div class="col-md-6">
              <label class="form-label" for="phoneNumber">Phone</label>
              <input id="phoneNumber" class="form-control" formControlName="phoneNumber"
                placeholder="+2010xxxxxxxx" [class.is-invalid]="invalid('phoneNumber')" />
              @if (invalid('phoneNumber')) {
                <div class="invalid-feedback">Enter a valid phone number.</div>
              }
            </div>

            @if (!isEdit()) {
              <div class="col-md-6">
                <label class="form-label" for="password">Password *</label>
                <input id="password" type="password" class="form-control"
                  formControlName="password" autocomplete="new-password"
                  [class.is-invalid]="invalid('password')" />
                @if (invalid('password')) {
                  <div class="invalid-feedback">Password must be at least 8 characters.</div>
                }
              </div>
            }
          </div>

          @if (!isEdit()) {
            <div class="d-flex justify-content-end gap-2 pt-4">
              <a routerLink="/teachers" class="btn btn-outline-secondary">Cancel</a>
              <button type="submit" class="btn btn-primary" [disabled]="submitting()">
                {{ submitting() ? 'Creating…' : 'Create teacher' }}
              </button>
            </div>
          }
        </form>
      </div>
    </div>
  `,
  styles: [`
    .page-header { display:flex; align-items:center; justify-content:space-between; gap:1rem; margin-bottom:1.5rem; }
    .card { border:1px solid var(--edvanz-border,#e5e7eb); border-radius:14px; }
  `],
})
export class TeacherFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly teacherService = inject(TeacherService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  private static readonly PHONE_PATTERN = /^\+?\d{8,15}$/;

  protected readonly isEdit = signal(false);
  protected readonly submitting = signal(false);
  private teacherId: string | null = null;

  protected readonly form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(120)]],
    userName: ['', [Validators.required]],
    email: ['', [Validators.email]],
    phoneNumber: ['', [Validators.pattern(TeacherFormComponent.PHONE_PATTERN)]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  ngOnInit(): void {
    this.teacherId = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(!!this.teacherId);

    if (this.isEdit()) {
      this.form.controls.password.clearValidators();
      this.form.controls.password.updateValueAndValidity();
      this.teacherService.getTeacherById(+this.teacherId!).subscribe((t) => {
        this.form.patchValue({ fullName: t.fullName, phoneNumber: t.phoneNumber ?? '' });
      });
    }
  }

  protected invalid(control: string): boolean {
    const c = this.form.get(control);
    return !!c && c.invalid && (c.touched || c.dirty);
  }

  protected submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.submitting.set(true);
    this.create();
  }

  private create(): void {
    const raw = this.form.getRawValue();

    // Step 1: create the User account
    const signUpReq: SignUpRequest = {
      userName: raw.userName,
      password: raw.password,
      fullName: raw.fullName,
      email: raw.email || undefined,
      phoneNumber: raw.phoneNumber || undefined,
      userType: 'Teacher',
    };

    this.teacherService.signUp(signUpReq).pipe(
      // Step 2: initialize the Teacher record
      switchMap(({ userId }) => {
        const initReq: InitializeTeacherRequest = {
          userId,
          subjectIds: [],   // No subject picker on this form — add later if needed
        };
        return this.teacherService.initializeTeacher(initReq);
      }),
    ).subscribe({
      next: (teacher) => {
        this.toast.success('Teacher created.');
        void this.router.navigate(['/teachers', teacher.id]);
      },
      error: () => this.submitting.set(false),
    });
  }
}
