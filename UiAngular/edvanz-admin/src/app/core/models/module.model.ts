// ── Module models (/api/admin/tutor-modules/*) ────────────────────────────────

/** A module row from the Models table. */
export interface ModuleInfo {
  id: number;
  name: string;
}

/** POST /api/admin/tutor-modules/grant */
export interface ModuleGrantRequest {
  teacherId: number;
  moduleId: number;
}

/** POST /api/admin/tutor-modules/revoke */
export interface ModuleRevokeRequest {
  teacherId: number;
  moduleId: number;
}

/** PUT /api/admin/tutor-modules/replace */
export interface TutorModulesReplaceRequest {
  teacherId: number;
  moduleIds: number[];
}
