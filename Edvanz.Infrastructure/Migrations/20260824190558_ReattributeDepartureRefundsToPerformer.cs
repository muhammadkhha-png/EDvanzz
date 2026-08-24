using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edvanz.Infrastructure.Migrations
{
    /// <summary>
    /// Data-only: departure refunds are now charged to the PERFORMER who confirmed the departure
    /// (they physically hand the cash back), no longer the original collector of the refunded
    /// month. Flips the display-attribution column on existing RefundDue rows so history matches
    /// the new rule; existing columns only, so a bare Sql() is batch-safe (BUG-10). Stored wallet
    /// balances for affected collectors are trued up post-deploy via the admin
    /// recompute-assistant-wallet endpoint (it rebuilds from the re-attributed ledger).
    /// </summary>
    public partial class ReattributeDepartureRefundsToPerformer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE [StudentDepartures]
SET [CollectedByUserId] = [ConfirmedByUserId]
WHERE [DepartureOutcome] = 1 -- RefundDue
  AND [FinalAmount] > 0
  AND [ConfirmedByUserId] IS NOT NULL
  AND ([CollectedByUserId] IS NULL OR [CollectedByUserId] <> [ConfirmedByUserId]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible data fix: the pre-flip original-collector value is not preserved.
        }
    }
}
