using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace vrScraper.DB.Migrations
{
    /// <inheritdoc />
    public partial class Liking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Disliked",
                table: "VideoItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Favorite",
                table: "VideoItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Liked",
                table: "VideoItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Disliked",
                table: "VideoItems");

            migrationBuilder.DropColumn(
                name: "Favorite",
                table: "VideoItems");

            migrationBuilder.DropColumn(
                name: "Liked",
                table: "VideoItems");
        }
    }
}
