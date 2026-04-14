using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimalTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class UseUtcDateTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Sightings",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OccurredAt",
                table: "Sightings",
                newName: "OccurredAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Sightings",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_Sightings_OwnerUserId_SpeciesId_OccurredAt",
                table: "Sightings",
                newName: "IX_Sightings_OwnerUserId_SpeciesId_OccurredAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_Sightings_OwnerUserId_OccurredAt",
                table: "Sightings",
                newName: "IX_Sightings_OwnerUserId_OccurredAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_Sightings_OwnerUserId_AnimalId_OccurredAt",
                table: "Sightings",
                newName: "IX_Sightings_OwnerUserId_AnimalId_OccurredAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "SightingPhotos",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Locations",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Locations",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Animals",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Animals",
                newName: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "Sightings",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "OccurredAtUtc",
                table: "Sightings",
                newName: "OccurredAt");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "Sightings",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_Sightings_OwnerUserId_SpeciesId_OccurredAtUtc",
                table: "Sightings",
                newName: "IX_Sightings_OwnerUserId_SpeciesId_OccurredAt");

            migrationBuilder.RenameIndex(
                name: "IX_Sightings_OwnerUserId_OccurredAtUtc",
                table: "Sightings",
                newName: "IX_Sightings_OwnerUserId_OccurredAt");

            migrationBuilder.RenameIndex(
                name: "IX_Sightings_OwnerUserId_AnimalId_OccurredAtUtc",
                table: "Sightings",
                newName: "IX_Sightings_OwnerUserId_AnimalId_OccurredAt");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "SightingPhotos",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "Locations",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "Locations",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "Animals",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "Animals",
                newName: "CreatedAt");
        }
    }
}
