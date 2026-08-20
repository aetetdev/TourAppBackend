using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Yolla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class YerOnerileri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "place_suggestion_id",
                table: "coin_entries",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "place_suggestions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    device_id = table.Column<int>(type: "integer", nullable: true),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    location = table.Column<Point>(type: "geography (Point, 4326)", nullable: false),
                    city_id = table.Column<int>(type: "integer", nullable: false),
                    district_id = table.Column<int>(type: "integer", nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    address = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    place_id = table.Column<int>(type: "integer", nullable: true),
                    coins_awarded = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_place_suggestions", x => x.id);
                    table.ForeignKey(
                        name: "fk_place_suggestions_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_place_suggestions_cities_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_place_suggestions_districts_district_id",
                        column: x => x.district_id,
                        principalTable: "districts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_place_suggestions_places_place_id",
                        column: x => x.place_id,
                        principalTable: "places",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_coin_entries_place_suggestion_id",
                table: "coin_entries",
                column: "place_suggestion_id",
                unique: true,
                filter: "place_suggestion_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_place_suggestions_category_id",
                table: "place_suggestions",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_place_suggestions_city_id",
                table: "place_suggestions",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_place_suggestions_district_id",
                table: "place_suggestions",
                column: "district_id");

            migrationBuilder.CreateIndex(
                name: "ix_place_suggestions_location",
                table: "place_suggestions",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_place_suggestions_place_id",
                table: "place_suggestions",
                column: "place_id");

            migrationBuilder.CreateIndex(
                name: "ix_place_suggestions_status_created_at",
                table: "place_suggestions",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_place_suggestions_user_id_status",
                table: "place_suggestions",
                columns: new[] { "user_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "place_suggestions");

            migrationBuilder.DropIndex(
                name: "ix_coin_entries_place_suggestion_id",
                table: "coin_entries");

            migrationBuilder.DropColumn(
                name: "place_suggestion_id",
                table: "coin_entries");
        }
    }
}
