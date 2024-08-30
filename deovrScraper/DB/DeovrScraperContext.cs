using deovrScraper.DB.Models;
using Microsoft.EntityFrameworkCore;

namespace deovrScraper.DB
{
  public class DeovrScraperContext(DbContextOptions<DeovrScraperContext> options) : DbContext(options)
  {
    public DbSet<DbVideoItem> VideoItems { get; set; }
    public DbSet<DbStar> Stars { get; set; }
    public DbSet<DbTag> Tags { get; set; }
    public DbSet<DbDeoVrTab> Tabs { get; set; }
  }
}
