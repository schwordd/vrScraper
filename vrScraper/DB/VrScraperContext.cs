using vrScraper.DB.Models;
using Microsoft.EntityFrameworkCore;

namespace vrScraper.DB
{
  public class VrScraperContext(DbContextOptions<VrScraperContext> options) : DbContext(options)
  {
    public DbSet<DbVideoItem> VideoItems { get; set; }
    public DbSet<DbStar> Stars { get; set; }
    public DbSet<DbTag> Tags { get; set; }
    public DbSet<DbVideoStar> VideoStars { get; set; }
    public DbSet<DbVideoTag> VideoTags { get; set; }
    public DbSet<DbVrTab> Tabs { get; set; }
    public DbSet<DbSetting> Settings { get; set; }
    public DbSet<DbScrapeLog> ScrapeLogs { get; set; }
    public DbSet<DbPlaybackEvent> PlaybackEvents { get; set; }
    public DbSet<DbVideoEngagement> VideoEngagements { get; set; }
    public DbSet<DbDailySnapshot> DailySnapshots { get; set; }

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

      // VideoEngagement 1:1 with VideoItem (shared primary key)
      modelBuilder.Entity<DbVideoEngagement>()
        .HasOne(e => e.Video)
        .WithOne(v => v.Engagement)
        .HasForeignKey<DbVideoEngagement>(e => e.VideoId);

      // Video <-> Star many-to-many via explicit junction entity with IsAutoDetected
      modelBuilder.Entity<DbVideoItem>()
        .HasMany(v => v.Stars)
        .WithMany(s => s.Videos)
        .UsingEntity<DbVideoStar>(
          j => j.HasOne(vs => vs.Star).WithMany().HasForeignKey(vs => vs.StarId),
          j => j.HasOne(vs => vs.Video).WithMany().HasForeignKey(vs => vs.VideoId),
          j => j.HasKey(vs => new { vs.VideoId, vs.StarId }));

      // VideoStars navigation from Video/Star side
      modelBuilder.Entity<DbVideoStar>()
        .HasOne(vs => vs.Video).WithMany(v => v.VideoStars).HasForeignKey(vs => vs.VideoId);
      modelBuilder.Entity<DbVideoStar>()
        .HasOne(vs => vs.Star).WithMany(s => s.VideoStars).HasForeignKey(vs => vs.StarId);

      // Video <-> Tag many-to-many via explicit junction entity with IsAutoDetected
      modelBuilder.Entity<DbVideoItem>()
        .HasMany(v => v.Tags)
        .WithMany(t => t.Videos)
        .UsingEntity<DbVideoTag>(
          j => j.HasOne(vt => vt.Tag).WithMany().HasForeignKey(vt => vt.TagId),
          j => j.HasOne(vt => vt.Video).WithMany().HasForeignKey(vt => vt.VideoId),
          j => j.HasKey(vt => new { vt.VideoId, vt.TagId }));

      // VideoTags navigation from Video/Tag side
      modelBuilder.Entity<DbVideoTag>()
        .HasOne(vt => vt.Video).WithMany(v => v.VideoTags).HasForeignKey(vt => vt.VideoId);
      modelBuilder.Entity<DbVideoTag>()
        .HasOne(vt => vt.Tag).WithMany(t => t.VideoTags).HasForeignKey(vt => vt.TagId);
    }
  }
}
