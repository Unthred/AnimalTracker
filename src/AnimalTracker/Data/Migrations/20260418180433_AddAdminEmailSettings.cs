using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimalTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminEmailSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailEnableSsl",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailEnabled",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EmailFromEmail",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailFromName",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 200,
                nullable: true,
                defaultValue: "AnimalTracker");

            migrationBuilder.AddColumn<string>(
                name: "EmailHost",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailPasswordProtected",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmailPort",
                table: "AppSettings",
                type: "INTEGER",
                nullable: true,
                defaultValue: 587);

            migrationBuilder.AddColumn<string>(
                name: "EmailUserNameProtected",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 1024,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailEnableSsl",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "EmailEnabled",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "EmailFromEmail",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "EmailFromName",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "EmailHost",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "EmailPasswordProtected",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "EmailPort",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "EmailUserNameProtected",
                table: "AppSettings");
        }
    }
}
