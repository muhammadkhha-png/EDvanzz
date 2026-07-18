import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TeacherProfile } from '../../../core/models/teacher.model';
import { TeacherService } from '../../../core/services/teacher.service';

/** Read-only teacher facts. Default tab of the details shell. */
@Component({
  selector: 'app-teacher-info',
  standalone: true,
  imports: [RouterLink],
  template: `
    @if (teacher(); as t) {
      <dl class="info-grid">
        <div><dt>Full name</dt><dd>{{ t.fullName }}</dd></div>
        <div><dt>Email</dt><dd>{{ t.email }}</dd></div>
        <div><dt>Phone</dt><dd>{{ t.phoneNumber }}</dd></div>
        <div><dt>Center</dt><dd>{{ t.teacherCode || '—' }}</dd></div>
        <div><dt>Address</dt><dd>{{ t.languagePreference || '—' }}</dd></div>
        <div>
          <dt>Account status</dt>
          <dd>{{ t.accountStatus ? 'Active' : 'Inactive' }}</dd>
        </div>
        <div>
          <dt>Granted modules</dt>
          <dd>{{ t.subjects.length }} module(s)</dd>
        </div>
      </dl>

      <div class="pt-2">
        <a [routerLink]="['/teachers', t.id, 'edit']" class="btn btn-outline-primary">
          Edit teacher
        </a>
      </div>
    }
  `,
  styles: [
    `
      .info-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
        gap: 1rem 2rem;
        margin-bottom: 1rem;
      }
      dt {
        font-size: 0.8rem;
        color: var(--edvanz-muted, #6b7280);
        text-transform: uppercase;
        letter-spacing: 0.03em;
      }
      dd {
        font-weight: 500;
        margin: 0.15rem 0 0;
      }
    `,
  ],
})
export class TeacherInfoComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly teacherService = inject(TeacherService);
  protected readonly teacher = signal<TeacherProfile | null>(null);

  ngOnInit(): void {
    // Parent holds the :id param; read it from the parent route snapshot.
    const id = this.route.parent?.snapshot.paramMap.get('id');
    if (id) {
      this.teacherService.getTeacherById(+id).subscribe((t) => this.teacher.set(t));
    }
  }
}
