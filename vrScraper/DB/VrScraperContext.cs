using vrScraper.DB.Models;
using Microsoft.EntityFrameworkCore;

namespace vrScraper.DB
{
  public class VrScraperContext(DbContextOptions<VrScraperContext> options) : DbContext(options)
  {
    public DbSet<DbVideoItem> VideoItems { get; set; }
    public DbSet<DbStar> Stars { get; set; }
    public DbSet<DbTag> Tags { get; set; }
    public DbSet<DbDeoVrTab> Tabs { get; set; }
    public DbSet<DbSetting> Settings { get; set; }
  }
}
