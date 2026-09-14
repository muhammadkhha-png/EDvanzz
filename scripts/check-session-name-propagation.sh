#!/usr/bin/env bash
# Guard: renaming a session must rewrite the denormalized copies of its name.
#
# Eight tables keep a copy of the session name, written once when the row is created:
#
#   StudentSessionAssignment.SessionNameAtAssignment   AttendanceRecord.SessionNameAtRecording
#   PaymentPeriod.SessionNameAtGeneration              AttendanceRecord.CrossSessionNameAtRecording
#   PaymentTransaction.SessionNameAtCollection         StudentAbsenceCounter.LastAbsenceSessionNameAtRecording
#   StudentDeparture.SessionNameAtDeparture            PaymentForgiveness.SessionNameAtForgiveness
#
# They exist for ONE reason (BR-ATT-005): sessions are HARD-deleted, so a history row must keep a
# readable name after its session is gone. `SessionId` is NULLed on that delete and nothing writes
# the row again, so the last-known name is what survives.
#
# Because reads go straight to those columns — no join, no lookup, on paths that run per mark and
# per page — a rename that does NOT rewrite them is invisible until a teacher notices one class
# listed under two names. That happened: 90 of session 78's 126 students read «المجموعة» while 36
# read «مجموعه الساعه ١٠», and /api/v1/payments/students served «المجموعة» and «مجموع الحامول» —
# names no session had had for months — to 118 students a month and growing, because periods are
# pre-generated one per month to the session's end date and that leg never self-healed.
#
# So: ANY write to a Session entity's SessionName must be followed by
# ISessionRepo.PropagateSessionNameAsync in the same method. That repo method is the single place
# that lists the eight tables — ADDING A NINTH DENORMALIZED COLUMN MEANS ADDING A STATEMENT THERE.
# Nothing else can catch that, because every read site is a plain column read by design.
#
# See CLAUDE.md §7.10.
set -uo pipefail

PROJECTS=(Edvanz.Domain Edvanz.Application Edvanz.Infrastructure Edvanz.API)
PROPAGATOR='PropagateSessionNameAsync'

# Several files in this repo have SPACES in their names, so file lists are always NUL-delimited.
list_sources() {
  find "${PROJECTS[@]}" -name '*.cs' \
    -not -path '*/obj/*' -not -path '*/bin/*' -not -path '*/Migrations/*' -print0 2>/dev/null
}

if [ -z "$(list_sources | tr '\0' '\n' | head -n 1)" ]; then
  echo "Session-name propagation guard: no source files found — run me from the repo root."
  exit 1
fi

failed=0
report() {
  if [ "$failed" -eq 0 ]; then
    echo "Session-name propagation guard failed (CLAUDE.md §7.10):"
    echo
  fi
  failed=1
  echo "  $1"
}

# ── 1. Every rename must be paired with a propagation ────────────────────────────────────
# A rename looks like `<something>.SessionName = <value>` where <something> is a Session entity.
# DTO writes (`SessionName = x` with no receiver) and the archival columns (`SessionNameAt…`) are
# not renames and are skipped. The pairing is checked per FILE, which is enough: there is exactly
# one rename in this codebase and splitting it across files would be a deliberate act.
WRITERS=$(list_sources | xargs -0 grep -lE '^[^/]*\b[A-Za-z_][A-Za-z0-9_]*\.SessionName[ \t]*=[^=]' 2>/dev/null)

while IFS= read -r file; do
  [ -z "$file" ] && continue
  # Reject only writes to a Session ENTITY. The receiver name is the signal: `session`, `s`,
  # `existing`, `entity`, `sess`, `dup`, `duplicate`, `target`. A DTO write is `SessionName = x`
  # with no receiver and never matches; `dto.SessionName = x` is a request object, not the row.
  hits=$(grep -nE '^[^/]*\b(session|sess|s|entity|existing|dup|duplicate|target)\.SessionName[ \t]*=[^=]' "$file" 2>/dev/null)
  [ -z "$hits" ] && continue
  if ! grep -q "$PROPAGATOR" "$file"; then
    while IFS= read -r hit; do
      [ -z "$hit" ] && continue
      report "$file:$hit"
    done <<< "$hits"
  fi
done <<< "$WRITERS"

# ── 2. The propagator itself must still cover all eight columns ──────────────────────────
# Cheap insurance against a statement being deleted during a refactor. Each entry is the property
# the repo method must SetProperty on.
REQUIRED=(
  SessionNameAtRecording
  CrossSessionNameAtRecording
  SessionNameAtAssignment
  LastAbsenceSessionNameAtRecording
  SessionNameAtGeneration
  SessionNameAtCollection
  SessionNameAtDeparture
  SessionNameAtForgiveness
)
IMPL=$(list_sources | xargs -0 grep -l "public async Task<int> $PROPAGATOR" 2>/dev/null | head -n 1)
if [ -z "$IMPL" ]; then
  report "$PROPAGATOR has no implementation — a rename can no longer update anything."
else
  for prop in "${REQUIRED[@]}"; do
    if ! grep -q "SetProperty(.*$prop" "$IMPL"; then
      report "$IMPL: $PROPAGATOR no longer writes $prop — that column will go stale on every rename."
    fi
  done
fi

if [ "$failed" -ne 0 ]; then
  echo
  echo "A session rename must rewrite the denormalized copies of its name, in the same"
  echo "transaction and AFTER every SaveChangesAsync in the method (ExecuteUpdate bypasses the"
  echo "change tracker, so a row still tracked with the old name would write it back):"
  echo
  echo "    session.SessionName = trimmedName;"
  echo "    …"
  echo "    await _unitOfWork.SessionsRepo.PropagateSessionNameAsync(teacherId, sessionId, trimmedName);"
  echo
  echo "If you added a NEW table that stores a session name, add its statement to"
  echo "SessionRepo.$PROPAGATOR and its property to REQUIRED in this script."
  exit 1
fi

echo "Session-name propagation guard passed: every rename rewrites the stored copies."
