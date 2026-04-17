using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimalTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSightingGeoAndTerritoryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Behavior",
                table: "Sightings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IndividualConfidence",
                table: "Sightings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Sightings",
                type: "REAL",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LocationAccuracyMeters",
                table: "Sightings",
                type: "REAL",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Sightings",
                type: "REAL",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ObservedUntilUtc",
                table: "Sightings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpeciesConfidence",
                table: "Sightings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sightings_OwnerUserId_Latitude_Longitude",
                table: "Sightings",
                columns: new[] { "OwnerUserId", "Latitude", "Longitude" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sightings_OwnerUserId_Latitude_Longitude",
                table: "Sightings");

            migrationBuilder.DropColumn(
                name: "Behavior",
                table: "Sightings");

            migrationBuilder.DropColumn(
                name: "IndividualConfidence",
                table: "Sightings");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Sightings");

            migrationBuilder.DropColumn(
                name: "LocationAccuracyMeters",
                table: "Sightings");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Sightings");

            migrationBuilder.DropColumn(
                name: "ObservedUntilUtc",
                table: "Sightings");

            migrationBuilder.DropColumn(
                name: "SpeciesConfidence",
                table: "Sightings");
        }
    }
}
