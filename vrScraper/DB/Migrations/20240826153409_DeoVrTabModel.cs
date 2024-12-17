using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace vrScraper.DB.Migrations
{
    /// <inheritdoc />
    public partial class VrTabModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tabs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    TagWhitelist = table.Column<string>(type: "TEXT", nullable: false),
                    ActressWhitelist = table.Column<string>(type: "TEXT", nullable: false),
                    TagBlacklist = table.Column<string>(type: "TEXT", nullable: false),
                    ActressBlacklist = table.Column<string>(type: "TEXT", nullable: false),
                    VideoWhitelist = table.Column<string>(type: "TEXT", nullable: false),
                    VideoBlacklist = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tabs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tabs");
        }
    }
}
