using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftCopyright.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicantOptionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RightsAcquisitionMethod",
                table: "ApplicantInfos",
                type: "varchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "DevelopmentMode",
                table: "ApplicantInfos",
                type: "varchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<string>(
                name: "ApplicationHandlingMethod",
                table: "ApplicantInfos",
                type: "varchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ClassificationCode",
                table: "ApplicantInfos",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepositMode",
                table: "ApplicantInfos",
                type: "varchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FirstPublicationPlace",
                table: "ApplicantInfos",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModificationDescription",
                table: "ApplicantInfos",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalRegistrationNumber",
                table: "ApplicantInfos",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerType",
                table: "ApplicantInfos",
                type: "varchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PartialRightsDescription",
                table: "ApplicantInfos",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RightsScope",
                table: "ApplicantInfos",
                type: "varchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SoftwareSource",
                table: "ApplicantInfos",
                type: "varchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicationHandlingMethod",
                table: "ApplicantInfos");

            migrationBuilder.DropColumn(
                name: "ClassificationCode",
                table: "ApplicantInfos");

            migrationBuilder.DropColumn(
                name: "DepositMode",
                table: "ApplicantInfos");

            migrationBuilder.DropColumn(
                name: "FirstPublicationPlace",
                table: "ApplicantInfos");

            migrationBuilder.DropColumn(
                name: "ModificationDescription",
                table: "ApplicantInfos");

            migrationBuilder.DropColumn(
                name: "OriginalRegistrationNumber",
                table: "ApplicantInfos");

            migrationBuilder.DropColumn(
                name: "OwnerType",
                table: "ApplicantInfos");

            migrationBuilder.DropColumn(
                name: "PartialRightsDescription",
                table: "ApplicantInfos");

            migrationBuilder.DropColumn(
                name: "RightsScope",
                table: "ApplicantInfos");

            migrationBuilder.DropColumn(
                name: "SoftwareSource",
                table: "ApplicantInfos");

            migrationBuilder.AlterColumn<string>(
                name: "RightsAcquisitionMethod",
                table: "ApplicantInfos",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "DevelopmentMode",
                table: "ApplicantInfos",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(40)",
                oldMaxLength: 40);
        }
    }
}
