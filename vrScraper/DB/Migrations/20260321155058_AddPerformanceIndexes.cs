using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace vrScraper.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_VideoItems_ErrorCount",
                table: "VideoItems",
                column: "ErrorCount");

            migrationBuilder.CreateIndex(
                name: "IX_VideoItems_Liked",
                table: "VideoItems",
                column: "Liked");

            migrationBuilder.CreateIndex(
                name: "IX_VideoItems_ParsedDetails",
                table: "VideoItems",
                column: "ParsedDetails");

            migrationBuilder.CreateIndex(
                name: "IX_Tabs_Type_Active",
                table: "Tabs",
                columns: new[] { "Type", "Active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VideoItems_ErrorCount",
                table: "VideoItems");

            migrationBuilder.DropIndex(
                name: "IX_VideoItems_Liked",
                table: "VideoItems");

            migrationBuilder.DropIndex(
                name: "IX_VideoItems_ParsedDetails",
                table: "VideoItems");

            migrationBuilder.DropIndex(
                name: "IX_Tabs_Type_Active",
                table: "Tabs");
        }
    }
}
