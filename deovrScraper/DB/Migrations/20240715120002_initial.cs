using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deovrScraper.DB.Migrations
{
  /// <inheritdoc />
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
  public partial class initial : Migration
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
  {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Stars",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VideoItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Site = table.Column<string>(type: "TEXT", nullable: false),
                    SiteVideoId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    Rating = table.Column<double>(type: "REAL", nullable: true),
                    Views = table.Column<long>(type: "INTEGER", nullable: true),
                    Uploader = table.Column<string>(type: "TEXT", nullable: true),
                    Link = table.Column<string>(type: "TEXT", nullable: true),
                    Thumbnail = table.Column<string>(type: "TEXT", nullable: true),
                    Quality = table.Column<string>(type: "TEXT", nullable: true),
                    IsVr = table.Column<bool>(type: "INTEGER", nullable: false),
                    DataVp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DbStarDbVideoItem",
                columns: table => new
                {
                    StarsId = table.Column<long>(type: "INTEGER", nullable: false),
                    VideosId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DbStarDbVideoItem", x => new { x.StarsId, x.VideosId });
                    table.ForeignKey(
                        name: "FK_DbStarDbVideoItem_Stars_StarsId",
                        column: x => x.StarsId,
                        principalTable: "Stars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DbStarDbVideoItem_VideoItems_VideosId",
                        column: x => x.VideosId,
                        principalTable: "VideoItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DbTagDbVideoItem",
                columns: table => new
                {
                    TagsId = table.Column<long>(type: "INTEGER", nullable: false),
                    VideosId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DbTagDbVideoItem", x => new { x.TagsId, x.VideosId });
                    table.ForeignKey(
                        name: "FK_DbTagDbVideoItem_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DbTagDbVideoItem_VideoItems_VideosId",
                        column: x => x.VideosId,
                        principalTable: "VideoItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoSources",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VideoItemId = table.Column<long>(type: "INTEGER", nullable: false),
                    LabelShort = table.Column<string>(type: "TEXT", nullable: false),
                    Src = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Default = table.Column<bool>(type: "INTEGER", nullable: false),
                    Resolution = table.Column<int>(type: "INTEGER", nullable: false)
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
                name: "IX_DbStarDbVideoItem_VideosId",
                table: "DbStarDbVideoItem",
                column: "VideosId");

            migrationBuilder.CreateIndex(
                name: "IX_DbTagDbVideoItem_VideosId",
                table: "DbTagDbVideoItem",
                column: "VideosId");

            migrationBuilder.CreateIndex(
                name: "IX_Stars_Name",
                table: "Stars",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoItems_Site_SiteVideoId",
                table: "VideoItems",
                columns: new[] { "Site", "SiteVideoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoSources_VideoItemId",
                table: "VideoSources",
                column: "VideoItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DbStarDbVideoItem");

            migrationBuilder.DropTable(
                name: "DbTagDbVideoItem");

            migrationBuilder.DropTable(
                name: "VideoSources");

            migrationBuilder.DropTable(
                name: "Stars");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "VideoItems");
        }
    }
}
