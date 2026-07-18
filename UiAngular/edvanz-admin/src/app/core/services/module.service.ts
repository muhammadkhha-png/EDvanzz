import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResult } from '../models/api-result.model';
import {
  ModuleGrantRequest,
  ModuleInfo,
  ModuleRevokeRequest,
  TutorModulesReplaceRequest,
} from '../models/module.model';

@Injectable({ providedIn: 'root' })
export class ModuleService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  /**
   * GET /api/admin/tutor-modules/catalogue
   * All available modules in the platform.
   * ⚠ FLAG: This endpoint does not yet exist on the backend.
   * Ask Belal to create: GET /api/admin/tutor-modules/catalogue → Result<ModuleInfo[]>
   */
  getAllModules(): Observable<ModuleInfo[]> {
    return this.http
      .get<ApiResult<ModuleInfo[]>>(`${this.base}/admin/tutor-modules/catalogue`)
      .pipe(map((r) => r.data));
  }

  /**
   * GET /api/admin/tutor-modules/{teacherId}
   * Modules currently granted to a specific teacher.
   * ⚠ FLAG: This endpoint does not yet exist on the backend.
   * Ask Belal to create: GET /api/admin/tutor-modules/{teacherId} → Result<ModuleInfo[]>
   */
  getTeacherModules(teacherId: number): Observable<ModuleInfo[]> {
    return this.http
      .get<ApiResult<ModuleInfo[]>>(`${this.base}/admin/tutor-modules/${teacherId}`)
      .pipe(map((r) => r.data));
  }

  /**
   * POST /api/admin/tutor-modules/grant
   * Grants a single module to a teacher. Idempotent.
   */
  grant(request: ModuleGrantRequest): Observable<string> {
    return this.http
      .post<ApiResult<string>>(`${this.base}/admin/tutor-modules/grant`, request)
      .pipe(map((r) => r.data));
  }

  /**
   * POST /api/admin/tutor-modules/revoke
   * Revokes a single module from a teacher. Idempotent.
   */
  revoke(request: ModuleRevokeRequest): Observable<string> {
    return this.http
      .post<ApiResult<string>>(`${this.base}/admin/tutor-modules/revoke`, request)
      .pipe(map((r) => r.data));
  }

  /**
   * PUT /api/admin/tutor-modules/replace
   * Replaces the teacher's full module set (diff semantics: adds missing, removes extras).
   */
  replace(request: TutorModulesReplaceRequest): Observable<string> {
    return this.http
      .put<ApiResult<string>>(`${this.base}/admin/tutor-modules/replace`, request)
      .pipe(map((r) => r.data));
  }
}
