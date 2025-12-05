namespace vrScraper.DB.Seed
{
  public static class DbDefaults
  {
    public static void SeedDefaultSettings(VrScraperContext db)
    {
      var defaultSettings = db.Settings.ToList();

      if (defaultSettings.Exists(a => a.Key == "TagBlacklist") == false)
      {
        db.Settings.Add(new Models.DbSetting()
        {
          Key = "TagBlacklist",
          Type = "System.String",
          Value = "[]"
        });
      }

      // Add automated scraping settings
      if (defaultSettings.Exists(a => a.Key == "ScheduledScrapingEnabled") == false)
      {
        db.Settings.Add(new Models.DbSetting()
        {
          Key = "ScheduledScrapingEnabled",
          Type = "System.Boolean",
          Value = "false"
        });
      }

      if (defaultSettings.Exists(a => a.Key == "ScheduledScrapingTime") == false)
      {
        db.Settings.Add(new Models.DbSetting()
        {
          Key = "ScheduledScrapingTime",
          Type = "System.String",
          Value = "02:00" // 2 AM default
        });
      }

      if (defaultSettings.Exists(a => a.Key == "ScheduledScrapingMaxPages") == false)
      {
        db.Settings.Add(new Models.DbSetting()
        {
          Key = "ScheduledScrapingMaxPages",
          Type = "System.Int32",
          Value = "50" // Max 50 pages to prevent infinite loops
        });
      }

      if (defaultSettings.Exists(a => a.Key == "LastScheduledScrape") == false)
      {
        db.Settings.Add(new Models.DbSetting()
        {
          Key = "LastScheduledScrape",
          Type = "System.String",
          Value = ""
        });
      }

      db.SaveChanges();
    }
    public static void SeedDefaultTabs(VrScraperContext db)
    {
      var defaultTabs = db.Tabs.Where(a => a.Type == "DEFAULT").ToList();
      var customTabs = db.Tabs.Where(a => a.Type == "CUSTOM").ToList();

      if (defaultTabs.Exists(a => a.Name == "Latest") == false)
      {
        db.Tabs.Add(new Models.DbVrTab()
        {
          Type = "DEFAULT",
          Name = "Latest",
          Active = true,
          Order  = -100,
          ActressBlacklist = "[]",
          ActressWhitelist = "[]",
          TagBlacklist = "[]",
          TagWhitelist = "[]",
          VideoBlacklist = "[]",
          VideoWhitelist = "[]"
        });
      }
      else
      {
        var tab = db.Tabs.Where(a => a.Name == "Latest").FirstOrDefault();
        tab!.Order = -100;
      }

      if (defaultTabs.Exists(a => a.Name == "Rating") == false)
      {
        db.Tabs.Add(new Models.DbVrTab()
        {
          Type = "DEFAULT",
          Name = "Rating",
          Active = true,
          Order = -90,
          ActressBlacklist = "[]",
          ActressWhitelist = "[]",
          TagBlacklist = "[]",
          TagWhitelist = "[]",
          VideoBlacklist = "[]",
          VideoWhitelist = "[]"
        });
      }
      else
      {
        var tab = db.Tabs.Where(a => a.Name == "Rating").FirstOrDefault();
        tab!.Order = -90;
      }

      if (defaultTabs.Exists(a => a.Name == "Random") == false)
      {
        db.Tabs.Add(new Models.DbVrTab()
        {
          Type = "DEFAULT",
          Name = "Random",
          Active = true,
          Order = -80,
          ActressBlacklist = "[]",
          ActressWhitelist = "[]",
          TagBlacklist = "[]",
          TagWhitelist = "[]",
          VideoBlacklist = "[]",
          VideoWhitelist = "[]"
        });
      }
      else
      {
        var tab = db.Tabs.Where(a => a.Name == "Random").FirstOrDefault();
        tab!.Order = -80;
      }

      if (defaultTabs.Exists(a => a.Name == "Liked") == false)
      {
        db.Tabs.Add(new Models.DbVrTab()
        {
          Type = "DEFAULT",
          Name = "Liked",
          Active = true,
          Order = -60,
          ActressBlacklist = "[]",
          ActressWhitelist = "[]",
          TagBlacklist = "[]",
          TagWhitelist = "[]",
          VideoBlacklist = "[]",
          VideoWhitelist = "[]"
        });
      }
      else
      {
        var tab = db.Tabs.Where(a => a.Name == "Liked").FirstOrDefault();
        tab!.Order = -60;
      }

      if (defaultTabs.Exists(a => a.Name == "Playtime") == false)
      {
        db.Tabs.Add(new Models.DbVrTab()
        {
          Type = "DEFAULT",
          Name = "Playtime",
          Active = true,
          Order = -50,
          ActressBlacklist = "[]",
          ActressWhitelist = "[]",
          TagBlacklist = "[]",
          TagWhitelist = "[]",
          VideoBlacklist = "[]",
          VideoWhitelist = "[]"
        });
      }
      else
      {
        var tab = db.Tabs.Where(a => a.Name == "Playtime").FirstOrDefault();
        tab!.Order = -50;
      }

      if (defaultTabs.Exists(a => a.Name == "PlayCount") == false)
      {
        db.Tabs.Add(new Models.DbVrTab()
        {
          Type = "DEFAULT",
          Name = "PlayCount",
          Active = true,
          Order = -45,
          ActressBlacklist = "[]",
          ActressWhitelist = "[]",
          TagBlacklist = "[]",
          TagWhitelist = "[]",
          VideoBlacklist = "[]",
          VideoWhitelist = "[]"
        });
      }
      else
      {
        var tab = db.Tabs.Where(a => a.Name == "PlayCount").FirstOrDefault();
        tab!.Order = -45;
      }

      if (defaultTabs.Exists(a => a.Name == "Unwatched") == false)
      {
        db.Tabs.Add(new Models.DbVrTab()
        {
          Type = "DEFAULT",
          Name = "Unwatched",
          Active = true,
          Order = -40,
          ActressBlacklist = "[]",
          ActressWhitelist = "[]",
          TagBlacklist = "[]",
          TagWhitelist = "[]",
          VideoBlacklist = "[]",
          VideoWhitelist = "[]"
        });
      }
      else
      {
        var tab = db.Tabs.Where(a => a.Name == "Unwatched").FirstOrDefault();
        tab!.Order = -40;
      }

      if (defaultTabs.Exists(a => a.Name == "Latest Unwatched") == false)
      {
        db.Tabs.Add(new Models.DbVrTab()
        {
          Type = "DEFAULT",
          Name = "Latest Unwatched",
          Active = true,
          Order = -30,
          ActressBlacklist = "[]",
          ActressWhitelist = "[]",
          TagBlacklist = "[]",
          TagWhitelist = "[]",
          VideoBlacklist = "[]",
          VideoWhitelist = "[]"
        });
      }
      else
      {
        var tab = db.Tabs.Where(a => a.Name == "Latest Unwatched").FirstOrDefault();
        tab!.Order = -30;
      }

      if (defaultTabs.Exists(a => a.Name == "Best Unwatched") == false)
      {
        db.Tabs.Add(new Models.DbVrTab()
        {
          Type = "DEFAULT",
          Name = "Best Unwatched",
          Active = true,
          Order = -20,
          ActressBlacklist = "[]",
          ActressWhitelist = "[]",
          TagBlacklist = "[]",
          TagWhitelist = "[]",
          VideoBlacklist = "[]",
          VideoWhitelist = "[]"
        });
      }
      else
      {
        var tab = db.Tabs.Where(a => a.Name == "Best Unwatched").FirstOrDefault();
        tab!.Order = -20;
      }

      db.SaveChanges();
    }
  }
}
