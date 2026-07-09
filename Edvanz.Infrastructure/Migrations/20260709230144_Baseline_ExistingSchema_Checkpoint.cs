using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Baseline_ExistingSchema_Checkpoint : Migration
    {
        // REVERSE-ENGINEERED BASELINE (checkpoint).
        // The repo had NO migration files, but the databases already contain the full schema as of
        // the April-2026 migration history (last row: 20260403215127_PaymentsPhase2). This migration
        // re-establishes the EF migrations pipeline by capturing the current model in its snapshot
        // (see EdvanzDbContextModelSnapshot) WITHOUT re-creating any objects — Up/Down are deliberately
        // empty so `dotnet ef database update` is a safe no-op against every existing database
        // (local + prod), then records this checkpoint in __EFMigrationsHistory.
        //
        // NOTE: because Up() is empty, this migration set cannot provision a brand-new database from
        // scratch. If scratch provisioning is ever needed, generate a fresh full-schema baseline from
        // an empty database. Subsequent schema changes (starting with the phone unique indexes) are
        // authored as normal migrations layered on top of this checkpoint.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
