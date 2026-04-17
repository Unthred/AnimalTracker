using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimalTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLastLoginAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginAtUtc",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastLoginIpAddress",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastLoginAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastLoginIpAddress",
                table: "AspNetUsers");
        }
    }
}
