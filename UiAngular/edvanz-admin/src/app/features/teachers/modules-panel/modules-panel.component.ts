import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ModuleInfo } from '../../../core/models/module.model';
import { ModuleService } from '../../../core/services/module.service';
import { ToastService } from '../../../core/services/toast.service';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';

type ModuleRow = ModuleInfo & { isGranted: boolean };

/**
 * Module assignment for one teacher.
 * Loads catalogue + teacher's current grants from two backend calls
 * (GET /api/admin/tutor-modules/catalogue + GET /api/admin/tutor-modules/{id}).
 * Saves via PUT /api/admin/tutor-modules/replace (diff semantics, idempotent).
 *
 * NOTE: Both GET endpoints must be created on the backend first.
 * Until then the component renders an empty state rather than erroring.
 */
@Component({
  selector: 'app-modules-panel',
  standalone: true,
  imports: [EmptyStateComponent],
  template: `
    @if (notAvailable()) {
      <app-empty-state
        icon="🧩"
        title="Module catalogue not yet available"
        message="The GET /api/admin/tutor-modules/catalogue and GET /api/admin/tutor-modules/{id} endpoints need to be created on the backend."
      />
    } @else {
      <div class="d-flex justify-content-between align-items-center mb-3">
        <p class="text-muted mb-0">Grant or revoke module access for this teacher.</p>
        <button
          type="button"
          class="btn btn-primary btn-sm"
          [disabled]="!isDirty()"
          (click)="saveAll()"
        >
          Save changes
        </button>
      </div>
      <div class="module-grid">
        @for (mod of assignments(); track mod.id) {
          <label class="module-item" [class.granted]="mod.isGranted">
            <input
              type="checkbox"
              class="form-check-input"
              [checked]="mod.isGranted"
              (change)="toggle(mod.id)"
            />
            <div class="module-name">{{ mod.name }}</div>
          </label>
        }
      </div>
    }
  `,
  styles: [`
    .module-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px,1fr)); gap:.75rem; }
    .module-item { display:flex; align-items:center; gap:.65rem; border:1px solid var(--edvanz-border,#e5e7eb); border-radius:10px; padding:.85rem 1rem; cursor:pointer; transition:border-color .15s; }
    .module-item.granted { border-color:var(--edvanz-primary,#2563eb); background:#eff6ff; }
    .module-name { font-weight:500; }
  `],
})
export class ModulesPanelComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly moduleService = inject(ModuleService);
  private readonly toast = inject(ToastService);

  private teacherId!: number;
  private originalGranted = new Set<number>();

  protected readonly assignments = signal<ModuleRow[]>([]);
  protected readonly notAvailable = signal(false);

  protected readonly isDirty = computed(() => {
    const current = new Set(this.assignments().filter((m) => m.isGranted).map((m) => m.id));
    if (current.size !== this.originalGranted.size) return true;
    for (const id of current) if (!this.originalGranted.has(id)) return true;
    return false;
  });

  ngOnInit(): void {
    this.teacherId = +(this.route.parent!.snapshot.paramMap.get('id') ?? '0');
    this.load();
  }

  protected toggle(id: number): void {
    this.assignments.update((list) =>
      list.map((m) => (m.id === id ? { ...m, isGranted: !m.isGranted } : m)),
    );
  }

  protected saveAll(): void {
    const moduleIds = this.assignments().filter((m) => m.isGranted).map((m) => m.id);
    this.moduleService.replace({ teacherId: this.teacherId, moduleIds }).subscribe({
      next: () => {
        this.originalGranted = new Set(moduleIds);
        this.toast.success('Modules updated.');
      },
    });
  }

  private load(): void {
    forkJoin({
      catalogue: this.moduleService.getAllModules(),
      granted: this.moduleService.getTeacherModules(this.teacherId),
    }).subscribe({
      next: ({ catalogue, granted }) => {
        const grantedIds = new Set(granted.map((g) => g.id));
        this.originalGranted = new Set(grantedIds);
        this.assignments.set(catalogue.map((m) => ({ ...m, isGranted: grantedIds.has(m.id) })));
      },
      error: () => {
        this.notAvailable.set(true);
      },
    });
  }
}
