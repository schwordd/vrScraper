using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace vrScraper.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddTitleNormalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add NormalizedTitle column
            migrationBuilder.AddColumn<string>(
                name: "NormalizedTitle",
                table: "VideoItems",
                type: "TEXT",
                nullable: true);

            // 2. Transform junction tables in-place (all instant operations, no data copy)
            // Stars junction: rename table, rename columns, add flag
            migrationBuilder.Sql("ALTER TABLE DbStarDbVideoItem RENAME TO VideoStars");
            migrationBuilder.Sql("ALTER TABLE VideoStars RENAME COLUMN VideosId TO VideoId");
            migrationBuilder.Sql("ALTER TABLE VideoStars RENAME COLUMN StarsId TO StarId");
            migrationBuilder.Sql("ALTER TABLE VideoStars ADD COLUMN IsAutoDetected INTEGER NOT NULL DEFAULT 0");

            // Tags junction: rename table, rename columns, add flag
            migrationBuilder.Sql("ALTER TABLE DbTagDbVideoItem RENAME TO VideoTags");
            migrationBuilder.Sql("ALTER TABLE VideoTags RENAME COLUMN VideosId TO VideoId");
            migrationBuilder.Sql("ALTER TABLE VideoTags RENAME COLUMN TagsId TO TagId");
            migrationBuilder.Sql("ALTER TABLE VideoTags ADD COLUMN IsAutoDetected INTEGER NOT NULL DEFAULT 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "NormalizedTitle", table: "VideoItems");

            // Reverse: rename back, drop IsAutoDetected column (SQLite can't drop columns pre-3.35, but 3.51 can)
            migrationBuilder.Sql("ALTER TABLE VideoStars DROP COLUMN IsAutoDetected");
            migrationBuilder.Sql("ALTER TABLE VideoStars RENAME COLUMN StarId TO StarsId");
            migrationBuilder.Sql("ALTER TABLE VideoStars RENAME COLUMN VideoId TO VideosId");
            migrationBuilder.Sql("ALTER TABLE VideoStars RENAME TO DbStarDbVideoItem");

            migrationBuilder.Sql("ALTER TABLE VideoTags DROP COLUMN IsAutoDetected");
            migrationBuilder.Sql("ALTER TABLE VideoTags RENAME COLUMN TagId TO TagsId");
            migrationBuilder.Sql("ALTER TABLE VideoTags RENAME COLUMN VideoId TO VideosId");
            migrationBuilder.Sql("ALTER TABLE VideoTags RENAME TO DbTagDbVideoItem");
        }
    }
}
