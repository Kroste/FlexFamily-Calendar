using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlexFamilyCalendar.Api.Migrations
{
    /// <inheritdoc />
    public partial class DropCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Kategorien fallen weg. Bevor Tabelle und Verknüpfung verschwinden, wandert, was sie
            // einem Eintrag gegeben haben, an den Eintrag selbst — der Plan sieht danach gleich aus:
            //  - der Kategoriename wird zur Bezeichnung (nur, wo noch keine steht),
            //  - die Kategoriefarbe wird zur eigenen Kachelfarbe (nur, wo noch keine gewählt ist,
            //    und nur, wenn sie ein gültiges Hex ist — dieselbe Regel wie NormalizeColor).
            // Die Client-Id ist die Guid der Kategorie als Text (lower() gegen abweichende Schreibweise).
            migrationBuilder.Sql("""
                UPDATE "Entries" AS e
                SET "CategoryLabel" = a."Name"
                FROM "ActivityTypes" AS a
                WHERE lower(e."ActivityTypeId") = a."Id"::text
                  AND coalesce(e."CategoryLabel", '') = ''
                  AND coalesce(a."Name", '') <> '';

                UPDATE "Entries" AS e
                SET "Color" = upper(a."Color")
                FROM "ActivityTypes" AS a
                WHERE lower(e."ActivityTypeId") = a."Id"::text
                  AND coalesce(e."Color", '') = ''
                  AND a."Color" ~ '^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$';
                """);

            migrationBuilder.DropTable(
                name: "ActivityTypes");

            migrationBuilder.DropColumn(
                name: "ActivityTypeId",
                table: "Entries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActivityTypeId",
                table: "Entries",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActivityTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Categories = table.Column<List<string>>(type: "text[]", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityTypes", x => x.Id);
                });
        }
    }
}
