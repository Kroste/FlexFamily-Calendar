using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlexFamilyCalendar.Api.Migrations
{
    /// <inheritdoc />
    public partial class RecurringFreeTextTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bisher war die Kategorie der Name einer Serie; der Titel blieb leer, weil der Dialog
            // ihn nie gesetzt hat. Bevor die Verknüpfung fällt, wandert der Kategoriename deshalb in
            // den Titel — sonst stünden alle bestehenden Serien danach namenlos im Plan.
            // Nur leere Titel werden gefüllt; die Client-Id ist die Guid der Kategorie als Text
            // (lower() gegen abweichende Schreibweise).
            migrationBuilder.Sql("""
                UPDATE "RecurringActivities" AS r
                SET "Title" = a."Name"
                FROM "ActivityTypes" AS a
                WHERE lower(r."ActivityTypeId") = a."Id"::text
                  AND coalesce(r."Title", '') = '';
                """);

            migrationBuilder.DropColumn(
                name: "ActivityTypeId",
                table: "RecurringActivities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActivityTypeId",
                table: "RecurringActivities",
                type: "text",
                nullable: true);
        }
    }
}
