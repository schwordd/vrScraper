using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace vrScraper.DB.Migrations
{
    /// <inheritdoc />
    public partial class LocalRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Rating",
                table: "VideoItems",
                newName: "SiteRating");

            migrationBuilder.AddColumn<DateTime>(
                name: "AddedUTC",
                table: "VideoItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LocalRating",
                table: "VideoItems",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddedUTC",
                table: "VideoItems");

            migrationBuilder.DropColumn(
                name: "LocalRating",
                table: "VideoItems");

            migrationBuilder.RenameColumn(
                name: "SiteRating",
                table: "VideoItems",
                newName: "Rating");
        }
    }
}
