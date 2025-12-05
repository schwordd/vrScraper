using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace vrScraper.DB.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDeprecatedFavTab : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Entferne veraltete "Fav" und "Favorite" Tabs aus der Datenbank
            migrationBuilder.Sql("DELETE FROM Tabs WHERE Name = 'Fav' OR Name = 'Favorite'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Keine Wiederherstellung der veralteten Tabs
        }
    }
}
