using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimalTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSurfaceOpacityPreferenceToUserSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SurfaceOpacityPercent",
                table: "UserSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 93);

            migrationBuilder.AddColumn<int>(
                name: "DarkSurfaceOpacityPercent",
                table: "UserSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 50);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DarkSurfaceOpacityPercent",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "SurfaceOpacityPercent",
                table: "UserSettings");
        }
    }
}
