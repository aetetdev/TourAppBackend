using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yolla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommonsRef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "commons_ref",
                table: "places",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "commons_ref",
                table: "places");
        }
    }
}
