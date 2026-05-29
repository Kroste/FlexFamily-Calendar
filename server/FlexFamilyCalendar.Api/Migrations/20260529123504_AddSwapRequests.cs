using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlexFamilyCalendar.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSwapRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SwapRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<string>(type: "text", nullable: false),
                    RespondedAt = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    FromUserId = table.Column<string>(type: "text", nullable: false),
                    FromUserName = table.Column<string>(type: "text", nullable: false),
                    FromDate = table.Column<string>(type: "text", nullable: false),
                    FromEntryId = table.Column<string>(type: "text", nullable: false),
                    ToUserId = table.Column<string>(type: "text", nullable: false),
                    ToUserName = table.Column<string>(type: "text", nullable: false),
                    ToDate = table.Column<string>(type: "text", nullable: true),
                    ToEntryId = table.Column<string>(type: "text", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SwapRequests", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SwapRequests");
        }
    }
}
