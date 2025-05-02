using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace vrScraper.DB.Migrations
{
    /// <inheritdoc />
    public partial class EnsureFavoritesAreLiked : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Stelle sicher, dass alle Videos mit Favorite=true auch Liked=true haben
            migrationBuilder.Sql("UPDATE VideoItems SET Liked = 1 WHERE Favorite = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
