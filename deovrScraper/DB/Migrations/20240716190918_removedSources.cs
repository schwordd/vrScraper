using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deovrScraper.DB.Migrations
{
    /// <inheritdoc />
    public partial class removedSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoSources");

            migrationBuilder.AddColumn<bool>(
                name: "ParsedDetails",
                table: "VideoItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParsedDetails",
                table: "VideoItems");

            migrationBuilder.CreateTable(
                name: "VideoSources",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VideoItemId = table.Column<long>(type: "INTEGER", nullable: false),
                    Default = table.Column<bool>(type: "INTEGER", nullable: false),
                    LabelShort = table.Column<string>(type: "TEXT", nullable: false),
                    Resolution = table.Column<int>(type: "INTEGER", nullable: false),
                    Src = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoSources_VideoItems_VideoItemId",
                        column: x => x.VideoItemId,
                        principalTable: "VideoItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoSources_VideoItemId",
                table: "VideoSources",
                column: "VideoItemId");
        }
    }
}
