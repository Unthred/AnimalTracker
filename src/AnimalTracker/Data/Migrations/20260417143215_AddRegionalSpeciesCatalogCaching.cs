using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimalTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRegionalSpeciesCatalogCaching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActiveSpeciesRegionKey",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActiveSpeciesRegionName",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SpeciesRegionCaches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RegionKey = table.Column<string>(type: "TEXT", nullable: false),
                    RegionName = table.Column<string>(type: "TEXT", nullable: false),
                    SpeciesId = table.Column<int>(type: "INTEGER", nullable: false),
                    SyncedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpeciesRegionCaches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpeciesRegionCaches_Species_SpeciesId",
                        column: x => x.SpeciesId,
                        principalTable: "Species",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpeciesRegionCaches_RegionKey",
                table: "SpeciesRegionCaches",
                column: "RegionKey");

            migrationBuilder.CreateIndex(
                name: "IX_SpeciesRegionCaches_RegionKey_SpeciesId",
                table: "SpeciesRegionCaches",
                columns: new[] { "RegionKey", "SpeciesId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpeciesRegionCaches_SpeciesId",
                table: "SpeciesRegionCaches",
                column: "SpeciesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpeciesRegionCaches");

            migrationBuilder.DropColumn(
                name: "ActiveSpeciesRegionKey",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ActiveSpeciesRegionName",
                table: "AppSettings");
        }
    }
}
