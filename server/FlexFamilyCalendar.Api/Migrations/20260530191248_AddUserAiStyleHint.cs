using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlexFamilyCalendar.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAiStyleHint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiStyleHint",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiStyleHint",
                table: "Users");
        }
    }
}
