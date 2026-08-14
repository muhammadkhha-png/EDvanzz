using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillUnbindRemovalMarker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One-time backfill for the "removed vs. awaiting" status distinction.
            //
            // A link that is Active (LinkStatus = 1) with no roster binding
            // (TeacherStudentId IS NULL) is EITHER "the teacher unbound a linked
            // student" (should read RemovedByTeacher) OR "accepted but not bound
            // yet" (AwaitingLink). Going forward UnbindStudentLinkAsync stamps
            // UnlinkedAt to record the former — but links unbound before that code
            // shipped have no marker, so they would incorrectly surface as
            // AwaitingLink.
            //
            // Old data left no trace that distinguishes the two, so we take the
            // dominant, persistent case: treat every currently unbound Active link
            // as removed by stamping UnlinkedAt. (A genuinely mid-"awaiting" link is
            // rare and transient — it self-corrects to Active the moment the teacher
            // binds it — and both states show the student the same "contact your
            // teacher" call to action, so the temporary mislabel is harmless.)
            migrationBuilder.Sql(@"
                UPDATE [StudentTeacherLinks]
                SET [UnlinkedAt] = SYSUTCDATETIME()
                WHERE [LinkStatus] = 1
                  AND [TeacherStudentId] IS NULL
                  AND [UnlinkedAt] IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not precisely reversible: the backfill cannot be distinguished from
            // genuine unbinds after the fact, so undo is intentionally a no-op.
        }
    }
}
