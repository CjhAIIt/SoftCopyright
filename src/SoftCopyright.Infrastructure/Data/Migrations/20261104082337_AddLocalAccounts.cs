using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftCopyright.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "ApplicationCases",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "ApplicationUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    UserName = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    DisplayName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    OnboardingCompleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AiAssistantEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    EncryptedAiBaseUrl = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    EncryptedAiModel = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    EncryptedAiApiKey = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationUsers", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationCases_UserId",
                table: "ApplicationCases",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUsers_UserName",
                table: "ApplicationUsers",
                column: "UserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationUsers");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationCases_UserId",
                table: "ApplicationCases");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ApplicationCases");
        }
    }
}
