namespace deovrScraper.DB.Seed
{
  public static class DbDefaults
  {
    public static void SeedDefaultTabs(DeovrScraperContext db)
    {
      var defaultTabs = db.Tabs.Where(a => a.Type == "DEFAULT").ToList();
      var customTabs = db.Tabs.Where(a => a.Type == "CUSTOM").ToList();

      if (defaultTabs.Exists(a => a.Name == "Latest") == false)
      {
        db.Tabs.Add(new Models.DbDeoVrTab()
        {
          Type = "DEFAULT",
          Name = "Latest",
          Active = true,
          Order  = 0,
          ActressBlacklist = "[]",
          ActressWhitelist = "[]",
          TagBlacklist = "[]",
          TagWhitelist = "[]",
          VideoBlacklist = "[]",
          VideoWhitelist = "[]"
        });
      }

      if (defaultTabs.Exists(a => a.Name == "Rating") == false)
      {
        db.Tabs.Add(new Models.DbDeoVrTab()
        {
          Type = "DEFAULT",
          Name = "Rating",
          Active = true,
          Order = 1,
          ActressBlacklist = "[]",
          ActressWhitelist = "[]",
          TagBlacklist = "[]",
          TagWhitelist = "[]",
          VideoBlacklist = "[]",
          VideoWhitelist = "[]"
        });
      }

      db.SaveChanges();
    }
  }
}
