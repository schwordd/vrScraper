using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Data.Sqlite;

#nullable disable

namespace vrScraper.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoEngagementAndSessionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                table: "PlaybackEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VideoEngagements",
                columns: table => new
                {
                    VideoId = table.Column<long>(type: "INTEGER", nullable: false),
                    OpenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ScrubEventCount = table.Column<int>(type: "INTEGER", nullable: false),
                    BackwardScrubCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ScrubCoveragePercent = table.Column<double>(type: "REAL", nullable: false),
                    LastSessionUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoEngagements", x => x.VideoId);
                    table.ForeignKey(
                        name: "FK_VideoEngagements_VideoItems_VideoId",
                        column: x => x.VideoId,
                        principalTable: "VideoItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // C# Backfill: Assign SessionIds and compute VideoEngagement from existing PlaybackEvents
            BackfillData(migrationBuilder);
        }

        private void BackfillData(MigrationBuilder migrationBuilder)
        {
            // Use raw SQL via MigrationBuilder to execute the backfill as a single operation.
            // We use a data-migration approach: read events, compute in SQL where possible,
            // and use SQLite's hex(randomblob()) for GUID generation.

            // Step 1: Assign SessionIds - group events by video, each OPEN starts a new session
            // SQLite doesn't have real GUIDs, but we can use a subquery approach:
            // For each event, find the most recent OPEN event for the same video before it,
            // and use that OPEN event's Id as a session grouping key.
            // Then assign a pseudo-GUID based on that grouping.

            // Since SQLite can't generate proper GUIDs in a single UPDATE, we do a simpler approach:
            // Use the OPEN event's Id + randomblob to create unique session identifiers.
            migrationBuilder.Sql(@"
                UPDATE PlaybackEvents
                SET SessionId = (
                    SELECT lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' || substr(lower(hex(randomblob(2))),2) || '-' || substr('89ab', abs(random()) % 4 + 1, 1) || substr(lower(hex(randomblob(2))),2) || '-' || lower(hex(randomblob(6)))
                )
                WHERE SessionId IS NULL;
            ");

            // The above gives each event its own GUID which is wrong - we need events in the same
            // session to share a GUID. Let's fix this with a proper approach:
            // First, assign GUIDs only to OPEN events, then propagate to subsequent events.

            // Reset all SessionIds
            migrationBuilder.Sql("UPDATE PlaybackEvents SET SessionId = NULL;");

            // Assign unique IDs to OPEN events only
            migrationBuilder.Sql(@"
                UPDATE PlaybackEvents
                SET SessionId = lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' || substr(lower(hex(randomblob(2))),2) || '-' || substr('89ab', abs(random()) % 4 + 1, 1) || substr(lower(hex(randomblob(2))),2) || '-' || lower(hex(randomblob(6)))
                WHERE EventType = 0;
            ");

            // For non-OPEN events: find the nearest preceding OPEN event for the same video and copy its SessionId
            migrationBuilder.Sql(@"
                UPDATE PlaybackEvents
                SET SessionId = (
                    SELECT pe2.SessionId
                    FROM PlaybackEvents pe2
                    WHERE pe2.VideoId = PlaybackEvents.VideoId
                      AND pe2.EventType = 0
                      AND pe2.UtcTimestamp <= PlaybackEvents.UtcTimestamp
                    ORDER BY pe2.UtcTimestamp DESC
                    LIMIT 1
                )
                WHERE EventType != 0 AND SessionId IS NULL;
            ");

            // Events without a preceding OPEN get their own session
            migrationBuilder.Sql(@"
                UPDATE PlaybackEvents
                SET SessionId = lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' || substr(lower(hex(randomblob(2))),2) || '-' || substr('89ab', abs(random()) % 4 + 1, 1) || substr(lower(hex(randomblob(2))),2) || '-' || lower(hex(randomblob(6)))
                WHERE SessionId IS NULL;
            ");

            // Step 2: Compute and insert VideoEngagement rows from existing PlaybackEvents
            // OpenCount = number of OPEN events per video
            // ScrubEventCount = number of PLAY events per video
            // BackwardScrubCount = computed from consecutive PLAY events where position goes backward > 5 seconds
            // ScrubCoveragePercent = max position reached / video duration

            // Insert basic engagement data (OpenCount, ScrubEventCount, LastSessionUtc)
            // BackwardScrubCount and Coverage need more complex logic
            migrationBuilder.Sql(@"
                INSERT INTO VideoEngagements (VideoId, OpenCount, ScrubEventCount, BackwardScrubCount, ScrubCoveragePercent, LastSessionUtc)
                SELECT
                    pe.VideoId,
                    SUM(CASE WHEN pe.EventType = 0 THEN 1 ELSE 0 END) as OpenCount,
                    SUM(CASE WHEN pe.EventType = 1 THEN 1 ELSE 0 END) as ScrubEventCount,
                    0 as BackwardScrubCount,
                    CASE
                        WHEN vi.Duration = '00:00:00' THEN 0.0
                        ELSE MIN(1.0, MAX(pe.TimeMs) / 1000.0 / (
                            CAST(substr(vi.Duration,1,2) AS REAL)*3600 +
                            CAST(substr(vi.Duration,4,2) AS REAL)*60 +
                            CAST(substr(vi.Duration,7,2) AS REAL)
                        ))
                    END as ScrubCoveragePercent,
                    MAX(pe.UtcTimestamp) as LastSessionUtc
                FROM PlaybackEvents pe
                JOIN VideoItems vi ON pe.VideoId = vi.Id
                GROUP BY pe.VideoId;
            ");

            // Step 3: Update BackwardScrubCount using window function
            // Count events where the PLAY position decreased by more than 5 seconds compared to the previous PLAY event
            migrationBuilder.Sql(@"
                UPDATE VideoEngagements
                SET BackwardScrubCount = COALESCE((
                    SELECT COUNT(*)
                    FROM (
                        SELECT VideoId, TimeMs,
                            LAG(TimeMs) OVER (PARTITION BY VideoId ORDER BY UtcTimestamp) as PrevTimeMs
                        FROM PlaybackEvents
                        WHERE EventType = 1
                    ) sub
                    WHERE sub.VideoId = VideoEngagements.VideoId
                      AND sub.PrevTimeMs IS NOT NULL
                      AND sub.PrevTimeMs - sub.TimeMs > 5000
                ), 0);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoEngagements");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "PlaybackEvents");
        }
    }
}
