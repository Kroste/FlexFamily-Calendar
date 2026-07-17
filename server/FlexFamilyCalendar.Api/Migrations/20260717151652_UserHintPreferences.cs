using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlexFamilyCalendar.Api.Migrations
{
    /// <inheritdoc />
    public partial class UserHintPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OnboardingSeen",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowHints",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OnboardingSeen",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ShowHints",
                table: "Users");
        }
    }
}
