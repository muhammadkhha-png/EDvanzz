import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import {
  StudentCapacityPackageDto,
  SubjectDto,
  TeacherProfile,
  UpdateTeacherProfileRequest,
} from '../../../core/models/teacher.model';
import { TeacherService } from '../../../core/services/teacher.service';
import { ToastService } from '../../../core/services/toast.service';

type UiLang = 'en' | 'ar';

const MSG: Record<UiLang, Record<string, string>> = {
  en: {
    edit: 'Edit teacher', save: 'Save changes', saving: 'Saving…', cancel: 'Cancel',
    fullName: 'Full name', email: 'Email', phone: 'Phone', teacherCode: 'Teacher code',
    subject: 'Subject', customSubject: 'Custom subject', language: 'App language',
    capacity: 'Student capacity', package: 'Capacity package', accountStatus: 'Account status',
    createdAt: 'Created', subscription: 'Subscription', none: '—', notSet: 'Not set',
    subjectPlaceholder: 'Select a subject', packagePlaceholder: 'Select a package',
    reqFullName: 'Full name is required.',
    reqSubject: 'Select a subject or enter a custom subject.',
    savedOk: 'Teacher updated.',
    packageOnboardingNote: 'Set at onboarding. Use "Adjust capacity" to change the live limit.',
    adjust: 'Adjust', adjustSave: 'Set', adjustCancel: 'Cancel',
    capacityIncreaseOnly: 'New capacity must be greater than the current capacity.',
    capacityUpdated: 'Capacity updated.',
    langEn: 'English', langAr: 'Arabic',
  },
  ar: {
    edit: 'تعديل المدرّس', save: 'حفظ التعديلات', saving: 'جارٍ الحفظ…', cancel: 'إلغاء',
    fullName: 'الاسم بالكامل', email: 'البريد الإلكتروني', phone: 'رقم الموبايل', teacherCode: 'كود المدرّس',
    subject: 'المادة', customSubject: 'مادة مخصّصة', language: 'لغة التطبيق',
    capacity: 'سعة الطلاب', package: 'باقة السعة', accountStatus: 'حالة الحساب',
    createdAt: 'تاريخ الإنشاء', subscription: 'الاشتراك', none: '—', notSet: 'غير محدّد',
    subjectPlaceholder: 'اختر المادة', packagePlaceholder: 'اختر الباقة',
    reqFullName: 'الاسم بالكامل مطلوب.',
    reqSubject: 'اختر مادة أو أدخل مادة مخصّصة.',
    savedOk: 'تم تحديث المدرّس.',
    packageOnboardingNote: 'تُحدَّد عند الإعداد. استخدم "تعديل السعة" لتغيير الحد الفعلي.',
    adjust: 'تعديل السعة', adjustSave: 'حفظ', adjustCancel: 'إلغاء',
    capacityIncreaseOnly: 'السعة الجديدة يجب أن تكون أكبر من السعة الحالية.',
    capacityUpdated: 'تم تحديث السعة.',
    langEn: 'الإنجليزية', langAr: 'العربية',
  },
};

/**
 * Details tab: read-only view + inline edit (PUT /api/teacher/{id}/profile).
 * Capacity/package are read-only once the teacher is configured — capacity then has its
 * own "Adjust capacity" action hitting the admin capacity endpoint, so every billing
 * side effect stays in one place and the profile PUT never touches capacity.
 */
@Component({
  selector: 'app-teacher-info',
  standalone: true,
  imports: [ReactiveFormsModule, DatePipe],
  template: `
    @if (teacher()) {
      <div [attr.dir]="uiLang() === 'ar' ? 'rtl' : 'ltr'">
        <div class="d-flex justify-content-end align-items-center gap-2 mb-3">
          <div class="btn-group btn-group-sm" role="group" aria-label="Interface language">
            <button type="button" class="btn"
              [class.btn-primary]="uiLang() === 'en'" [class.btn-outline-secondary]="uiLang() !== 'en'"
              (click)="setUiLang('en')">EN</button>
            <button type="button" class="btn"
              [class.btn-primary]="uiLang() === 'ar'" [class.btn-outline-secondary]="uiLang() !== 'ar'"
              (click)="setUiLang('ar')">ع</button>
          </div>
          @if (!editing()) {
            <button type="button" class="btn btn-outline-primary" (click)="startEdit()">{{ t('edit') }}</button>
          }
        </div>

        @if (!editing()) {
          <!-- ── VIEW MODE ─────────────────────────────────────────────────── -->
          <dl class="info-grid">
            <div><dt>{{ t('fullName') }}</dt><dd>{{ current.fullName }}</dd></div>
            <div><dt>{{ t('email') }}</dt><dd>{{ current.email || t('none') }}</dd></div>
            <div><dt>{{ t('phone') }}</dt><dd>{{ current.phoneNumber || t('none') }}</dd></div>
            <div><dt>{{ t('teacherCode') }}</dt><dd class="font-monospace">{{ current.teacherCode || t('none') }}</dd></div>
            <div><dt>{{ t('subject') }}</dt><dd>{{ subjectsLabel() }}</dd></div>
            <div><dt>{{ t('customSubject') }}</dt><dd>{{ current.customSubject || t('none') }}</dd></div>
            <div><dt>{{ t('language') }}</dt><dd>{{ current.languagePreference === 'ar' ? t('langAr') : t('langEn') }}</dd></div>
            <div><dt>{{ t('package') }}</dt><dd>{{ current.capacityPackageName || t('notSet') }}</dd></div>
            <div>
              <dt>{{ t('capacity') }}</dt>
              <dd>
                @if (!adjusting()) {
                  {{ current.studentCapacity }}
                  @if (current.isConfigurationCompleted) {
                    <button type="button" class="btn btn-sm btn-outline-primary ms-2" (click)="startAdjustCapacity()">
                      {{ t('adjust') }}
                    </button>
                  }
                } @else {
                  <div class="d-flex align-items-center gap-2">
                    <input type="number" class="form-control form-control-sm" style="max-width:130px"
                      [formControl]="capacityControl" [min]="current.studentCapacity + 1" />
                    <button type="button" class="btn btn-sm btn-primary" [disabled]="savingCapacity()"
                      (click)="saveCapacity()">{{ savingCapacity() ? t('saving') : t('adjustSave') }}</button>
                    <button type="button" class="btn btn-sm btn-outline-secondary"
                      (click)="cancelAdjustCapacity()">{{ t('adjustCancel') }}</button>
                  </div>
                }
              </dd>
            </div>
            <div><dt>{{ t('accountStatus') }}</dt><dd>{{ current.accountStatus }}</dd></div>
            <div><dt>{{ t('createdAt') }}</dt><dd>{{ current.createdAt | date: 'mediumDate' }}</dd></div>
            <div><dt>{{ t('subscription') }}</dt><dd>{{ current.activeSubscription?.subscriptionStatus || t('none') }}</dd></div>
          </dl>
        } @else {
          <!-- ── EDIT MODE (profile only — never capacity) ─────────────────── -->
          <form [formGroup]="form" (ngSubmit)="save()" novalidate>
            <div class="row g-3">
              <div class="col-md-6">
                <label class="form-label" for="fullName">{{ t('fullName') }} *</label>
                <input id="fullName" class="form-control" formControlName="fullName"
                  [class.is-invalid]="invalid('fullName')" />
                @if (invalid('fullName')) { <div class="invalid-feedback">{{ t('reqFullName') }}</div> }
              </div>

              <div class="col-md-6">
                <label class="form-label" for="languagePreference">{{ t('language') }} *</label>
                <select id="languagePreference" class="form-select" formControlName="languagePreference">
                  <option value="en">{{ t('langEn') }}</option>
                  <option value="ar">{{ t('langAr') }}</option>
                </select>
              </div>

              <div class="col-md-6">
                <label class="form-label" for="subjectId">{{ t('subject') }}</label>
                <select id="subjectId" class="form-select" formControlName="subjectId"
                  [class.is-invalid]="subjectInvalid()">
                  <option [ngValue]="null">{{ t('subjectPlaceholder') }}</option>
                  @for (s of subjects(); track s.id) {
                    <option [ngValue]="s.id">{{ uiLang() === 'ar' ? s.nameAr : s.nameEn }}</option>
                  }
                </select>
                @if (subjectInvalid()) { <div class="invalid-feedback">{{ t('reqSubject') }}</div> }
              </div>

              <div class="col-md-6">
                <label class="form-label" for="customSubject">{{ t('customSubject') }}</label>
                <input id="customSubject" class="form-control" formControlName="customSubject" />
              </div>

              <div class="col-md-6">
                <label class="form-label" for="studentCapacityPackageId">{{ t('package') }}</label>
                @if (!current.isConfigurationCompleted) {
                  <select id="studentCapacityPackageId" class="form-select" formControlName="studentCapacityPackageId">
                    <option [ngValue]="null">{{ t('packagePlaceholder') }}</option>
                    @for (p of packages(); track p.id) { <option [ngValue]="p.id">{{ p.name }}</option> }
                  </select>
                } @else {
                  <input class="form-control" [value]="current.capacityPackageName || t('notSet')" disabled />
                  <div class="form-text">{{ t('packageOnboardingNote') }}</div>
                }
              </div>
            </div>

            <div class="d-flex justify-content-end gap-2 pt-4">
              <button type="button" class="btn btn-outline-secondary" (click)="cancelEdit()">{{ t('cancel') }}</button>
              <button type="submit" class="btn btn-primary" [disabled]="saving()">
                {{ saving() ? t('saving') : t('save') }}
              </button>
            </div>
          </form>
        }
      </div>
    }
  `,
  styles: [`
    .info-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(240px, 1fr)); gap:1rem 2rem; margin-bottom:1rem; }
    dt { font-size:0.8rem; color:var(--edvanz-muted,#6b7280); text-transform:uppercase; letter-spacing:0.03em; }
    dd { font-weight:500; margin:0.15rem 0 0; }
  `],
})
export class TeacherInfoComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly teacherService = inject(TeacherService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  protected readonly teacher = signal<TeacherProfile | null>(null);
  protected readonly subjects = signal<SubjectDto[]>([]);
  protected readonly packages = signal<StudentCapacityPackageDto[]>([]);
  protected readonly editing = signal(false);
  protected readonly saving = signal(false);
  protected readonly adjusting = signal(false);
  protected readonly savingCapacity = signal(false);
  protected readonly uiLang = signal<UiLang>('en');

  private teacherId!: number;

  protected readonly form = this.fb.nonNullable.group(
    {
      fullName: ['', [Validators.required, Validators.maxLength(120)]],
      languagePreference: ['en' as UiLang, [Validators.required]],
      subjectId: this.fb.control<number | null>(null),
      customSubject: [''],
      studentCapacityPackageId: this.fb.control<number | null>(null),
    },
    { validators: [TeacherInfoComponent.subjectOrCustom] },
  );

  protected readonly capacityControl = this.fb.nonNullable.control(0, [Validators.required]);

  protected get current(): TeacherProfile {
    return this.teacher()!;
  }

  ngOnInit(): void {
    const id = this.route.parent?.snapshot.paramMap.get('id');
    if (!id) return;
    this.teacherId = +id;

    const openInEdit = this.route.snapshot.queryParamMap.get('edit') === '1';

    this.teacherService.getTeacherById(this.teacherId).subscribe((t) => {
      this.teacher.set(t);
      if (openInEdit) this.startEdit();
    });
    this.loadLookups();
  }

  private loadLookups(): void {
    this.teacherService.getSubjects(this.uiLang()).subscribe({
      next: (list) => this.subjects.set([...list].sort((a, b) => a.displayOrder - b.displayOrder)),
      error: () => {},
    });
    this.teacherService.getCapacityPackages(this.uiLang()).subscribe({
      next: (list) => this.packages.set([...list].sort((a, b) => a.displayOrder - b.displayOrder)),
      error: () => {},
    });
  }

  private static subjectOrCustom(group: AbstractControl): ValidationErrors | null {
    const subjectId = group.get('subjectId')?.value;
    const custom = (group.get('customSubject')?.value ?? '').trim();
    return subjectId == null && !custom ? { subjectRequired: true } : null;
  }

  protected setUiLang(lang: UiLang): void {
    if (this.uiLang() === lang) return;
    this.uiLang.set(lang);
    this.loadLookups();
  }

  protected t(key: string): string {
    return MSG[this.uiLang()][key] ?? key;
  }

  protected subjectsLabel(): string {
    const t = this.teacher();
    if (!t) return this.t('none');
    const names = t.subjects.map((s) => (this.uiLang() === 'ar' ? s.nameAr : s.nameEn));
    if (t.customSubject) names.push(t.customSubject);
    return names.length ? names.join('، ') : this.t('none');
  }

  // ── Profile edit ──────────────────────────────────────────────────────────
  protected startEdit(): void {
    const t = this.teacher();
    if (!t) return;
    this.form.reset({
      fullName: t.fullName,
      languagePreference: t.languagePreference === 'ar' ? 'ar' : 'en',
      subjectId: t.subjects[0]?.id ?? null,          // single-subject (D3); save replaces all
      customSubject: t.customSubject ?? '',
      studentCapacityPackageId: t.studentCapacityPackageId ?? null,
    });
    this.editing.set(true);
  }

  protected cancelEdit(): void {
    this.editing.set(false);
  }

  protected invalid(control: string): boolean {
    const c = this.form.get(control);
    return !!c && c.invalid && (c.touched || c.dirty);
  }

  protected subjectInvalid(): boolean {
    const c = this.form.controls.subjectId;
    return (c.touched || c.dirty) && this.form.hasError('subjectRequired');
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    const raw = this.form.getRawValue();

    const request: UpdateTeacherProfileRequest = {
      fullName: raw.fullName.trim(),
      languagePreference: raw.languagePreference,
      subjectIds: raw.subjectId != null ? [raw.subjectId] : [],
      customSubject: raw.customSubject.trim() || undefined,
    };
    // Package is editable ONLY before configuration; for configured teachers the field is
    // read-only and capacity is changed via the Adjust capacity action, so omit it here.
    if (!this.current.isConfigurationCompleted) {
      request.studentCapacityPackageId = raw.studentCapacityPackageId;
    }

    this.teacherService.updateTeacher(this.teacherId, request, this.uiLang()).subscribe({
      next: (updated) => {
        this.teacher.set(updated);
        this.editing.set(false);
        this.saving.set(false);
        this.toast.success(this.t('savedOk'));
      },
      error: () => this.saving.set(false),
    });
  }

  // ── Capacity adjust (admin endpoint, increase-only) ───────────────────────
  protected startAdjustCapacity(): void {
    this.capacityControl.setValue(this.current.studentCapacity + 1);
    this.adjusting.set(true);
  }

  protected cancelAdjustCapacity(): void {
    this.adjusting.set(false);
  }

  protected saveCapacity(): void {
    const value = this.capacityControl.value;
    if (!value || value <= this.current.studentCapacity) {
      this.toast.error(this.t('capacityIncreaseOnly'));
      return;
    }
    this.savingCapacity.set(true);
    this.teacherService.adjustTeacherCapacity(this.teacherId, { newCapacity: value }, this.uiLang()).subscribe({
      next: () => {
        // Re-fetch so studentCapacity (and any derived fields) reflect the raise.
        this.teacherService.getTeacherById(this.teacherId).subscribe((t) => this.teacher.set(t));
        this.adjusting.set(false);
        this.savingCapacity.set(false);
        this.toast.success(this.t('capacityUpdated'));
      },
      error: () => this.savingCapacity.set(false),
    });
  }
}
