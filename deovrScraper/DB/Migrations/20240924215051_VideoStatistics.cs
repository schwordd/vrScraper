using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deovrScraper.DB.Migrations
{
    /// <inheritdoc />
    public partial class VideoStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PlayCount",
                table: "VideoItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "PlayDurationEst",
                table: "VideoItems",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlayCount",
                table: "VideoItems");

            migrationBuilder.DropColumn(
                name: "PlayDurationEst",
                table: "VideoItems");
        }
    }
}
