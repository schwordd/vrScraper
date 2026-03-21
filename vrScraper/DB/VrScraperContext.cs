using vrScraper.DB.Models;
using Microsoft.EntityFrameworkCore;

namespace vrScraper.DB
{
  public class VrScraperContext(DbContextOptions<VrScraperContext> options) : DbContext(options)
  {
    public DbSet<DbVideoItem> VideoItems { get; set; }
    public DbSet<DbStar> Stars { get; set; }
    public DbSet<DbTag> Tags { get; set; }
    public DbSet<DbVrTab> Tabs { get; set; }
    public DbSet<DbSetting> Settings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      base.OnModelCreating(modelBuilder);

      modelBuilder.Entity<DbVideoItem>(entity =>
      {
        entity.HasIndex(e => e.ParsedDetails);
        entity.HasIndex(e => e.Liked);
        entity.HasIndex(e => e.ErrorCount);
      });

      modelBuilder.Entity<DbVrTab>()
        .HasIndex(e => new { e.Type, e.Active });
    }
  }
}
