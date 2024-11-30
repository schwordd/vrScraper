namespace deovrScraper.DB.Seed
{
  public static class DbDefaults
  {
    public static void SeedDefaultSettings(DeovrScraperContext db)
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

      db.SaveChanges();
    }
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
        db.Tabs.Add(new Models.DbDeoVrTab()
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
        db.Tabs.Add(new Models.DbDeoVrTab()
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

      if (defaultTabs.Exists(a => a.Name == "Fav") == false)
      {
        db.Tabs.Add(new Models.DbDeoVrTab()
        {
          Type = "DEFAULT",
          Name = "Fav",
          Active = true,
          Order = -70,
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
        var tab = db.Tabs.Where(a => a.Name == "Fav").FirstOrDefault();
        tab!.Order = -70;
      }

      if (defaultTabs.Exists(a => a.Name == "Liked") == false)
      {
        db.Tabs.Add(new Models.DbDeoVrTab()
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
        db.Tabs.Add(new Models.DbDeoVrTab()
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

      if (defaultTabs.Exists(a => a.Name == "Unwatched") == false)
      {
        db.Tabs.Add(new Models.DbDeoVrTab()
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

      db.SaveChanges();
    }
  }
}
