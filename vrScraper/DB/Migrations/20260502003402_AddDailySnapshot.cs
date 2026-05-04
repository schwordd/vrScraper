using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace vrScraper.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddDailySnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailySnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SnapshotDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TotalCount = table.Column<int>(type: "INTEGER", nullable: false),
                    WatchedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UnwatchedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LikedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailySnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailySnapshots_SnapshotDate",
                table: "DailySnapshots",
                column: "SnapshotDate",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailySnapshots");
        }
    }
}
