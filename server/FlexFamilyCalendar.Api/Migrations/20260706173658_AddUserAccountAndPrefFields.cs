using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlexFamilyCalendar.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAccountAndPrefFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "AccountStart",
                table: "Users",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<double>(
                name: "OpeningBalanceHours",
                table: "Users",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            // Bestehende Benutzer behalten sinnvolle Defaults: Feiertage sichtbar, System-Theme.
            migrationBuilder.AddColumn<bool>(
                name: "ShowHolidays",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ThemeVariant",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "System");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountStart",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OpeningBalanceHours",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ShowHolidays",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ThemeVariant",
                table: "Users");
        }
    }
}
