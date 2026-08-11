using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yolla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BoundaryOsmIdsAndSpatialIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "osm_relation_id",
                table: "districts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "osm_relation_id",
                table: "cities",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_districts_boundary",
                table: "districts",
                column: "boundary")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_districts_osm_relation_id",
                table: "districts",
                column: "osm_relation_id",
                unique: true,
                filter: "osm_relation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_cities_boundary",
                table: "cities",
                column: "boundary")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_cities_osm_relation_id",
                table: "cities",
                column: "osm_relation_id",
                unique: true,
                filter: "osm_relation_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_districts_boundary",
                table: "districts");

            migrationBuilder.DropIndex(
                name: "ix_districts_osm_relation_id",
                table: "districts");

            migrationBuilder.DropIndex(
                name: "ix_cities_boundary",
                table: "cities");

            migrationBuilder.DropIndex(
                name: "ix_cities_osm_relation_id",
                table: "cities");

            migrationBuilder.DropColumn(
                name: "osm_relation_id",
                table: "districts");

            migrationBuilder.DropColumn(
                name: "osm_relation_id",
                table: "cities");
        }
    }
}
