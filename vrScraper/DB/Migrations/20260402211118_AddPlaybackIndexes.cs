using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace vrScraper.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaybackIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PlaybackEvents indexes for common query patterns
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS IX_PlaybackEvents_VideoId ON PlaybackEvents(VideoId);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS IX_PlaybackEvents_SessionId ON PlaybackEvents(SessionId);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS IX_PlaybackEvents_VideoId_UtcTimestamp ON PlaybackEvents(VideoId, UtcTimestamp);");

            // Junction table indexes for IsAutoDetected queries
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS IX_VideoStars_IsAutoDetected ON VideoStars(IsAutoDetected);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS IX_VideoTags_IsAutoDetected ON VideoTags(IsAutoDetected);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_PlaybackEvents_VideoId;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_PlaybackEvents_SessionId;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_PlaybackEvents_VideoId_UtcTimestamp;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_VideoStars_IsAutoDetected;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_VideoTags_IsAutoDetected;");
        }
    }
}
