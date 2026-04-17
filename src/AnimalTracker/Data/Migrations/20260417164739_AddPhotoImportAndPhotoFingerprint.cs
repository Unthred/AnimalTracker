using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimalTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoImportAndPhotoFingerprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentSha256Hex",
                table: "SightingPhotos",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OriginalCaptureUtc",
                table: "SightingPhotos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OriginalLatitude",
                table: "SightingPhotos",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OriginalLongitude",
                table: "SightingPhotos",
                type: "REAL",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PhotoImportBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TotalItems = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessedItems = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedSightings = table.Column<int>(type: "INTEGER", nullable: false),
                    SkippedDuplicates = table.Column<int>(type: "INTEGER", nullable: false),
                    NeedsReviewCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoImportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PhotoImportItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BatchId = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    ContentSha256Hex = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SpeciesId = table.Column<int>(type: "INTEGER", nullable: true),
                    CandidateConfidence = table.Column<double>(type: "REAL", nullable: true),
                    RecognizedLabel = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    ClusterId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SightingId = table.Column<int>(type: "INTEGER", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoImportItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhotoImportItems_PhotoImportBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "PhotoImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SightingPhotos_ContentSha256Hex",
                table: "SightingPhotos",
                column: "ContentSha256Hex");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoImportBatches_OwnerUserId_CreatedAtUtc",
                table: "PhotoImportBatches",
                columns: new[] { "OwnerUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PhotoImportItems_BatchId",
                table: "PhotoImportItems",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoImportItems_ContentSha256Hex",
                table: "PhotoImportItems",
                column: "ContentSha256Hex");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoImportItems_SightingId",
                table: "PhotoImportItems",
                column: "SightingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhotoImportItems");

            migrationBuilder.DropTable(
                name: "PhotoImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_SightingPhotos_ContentSha256Hex",
                table: "SightingPhotos");

            migrationBuilder.DropColumn(
                name: "ContentSha256Hex",
                table: "SightingPhotos");

            migrationBuilder.DropColumn(
                name: "OriginalCaptureUtc",
                table: "SightingPhotos");

            migrationBuilder.DropColumn(
                name: "OriginalLatitude",
                table: "SightingPhotos");

            migrationBuilder.DropColumn(
                name: "OriginalLongitude",
                table: "SightingPhotos");
        }
    }
}
