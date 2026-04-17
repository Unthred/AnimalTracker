using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimalTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSpeciesCatalogMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CatalogSource",
                table: "Species",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CatalogSourceId",
                table: "Species",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageAttribution",
                table: "Species",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageLicense",
                table: "Species",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Species",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScientificName",
                table: "Species",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CatalogSource",
                table: "Species");

            migrationBuilder.DropColumn(
                name: "CatalogSourceId",
                table: "Species");

            migrationBuilder.DropColumn(
                name: "ImageAttribution",
                table: "Species");

            migrationBuilder.DropColumn(
                name: "ImageLicense",
                table: "Species");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Species");

            migrationBuilder.DropColumn(
                name: "ScientificName",
                table: "Species");
        }
    }
}
