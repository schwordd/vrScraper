using vrScraper.DB;
using vrScraper.DB.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace vrScraper.Services
{
  public class TabFilteringService(IServiceScopeFactory scopeFactory) : ITabFilteringService
  {
    public async Task<List<(string Name, List<DbVideoItem> Videos)>> GetFilteredTabVideos(
        List<DbVideoItem> allItems,
        List<string> globalBlackList)
    {
      var tabs = new List<(string Name, List<DbVideoItem> Videos)>();

      // Apply global tag blacklist
      allItems = allItems.Where(item => !item.Tags.Exists(a => globalBlackList.Any(b => b == a.Name))).ToList();

      using var scope = scopeFactory.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

      var tabConfigs = await context.Tabs.Where(t => t.Active).OrderBy(t => t.Order).ToListAsync();
      tabConfigs.ForEach(t =>
      {
        switch (t.Type)
        {
          case "DEFAULT":
            if (t.Name == "Latest")
            {
              var list = allItems.OrderByDescending(v => long.Parse(v.SiteVideoId)).ToList();
              tabs.Add((t.Name, list));
            }
            else if (t.Name == "Rating")
            {
              var list = allItems.OrderByDescending(v => v.SiteRating).ThenByDescending(v => v.Views).ToList();
              tabs.Add((t.Name, list));
            }
            else if (t.Name == "Random")
            {
              var list = allItems.OrderBy(a => Guid.NewGuid()).ToList();
              tabs.Add((t.Name, list));
            }
            else if (t.Name == "Liked")
            {
              var list = allItems.Where(x => x.Liked == true).OrderBy(a => Guid.NewGuid()).ToList();
              tabs.Add((t.Name, list));
            }
            else if (t.Name == "Playtime")
            {
              var list = allItems.OrderByDescending(a => a.PlayDurationEst).Where(a => a.PlayDurationEst > TimeSpan.FromSeconds(20)).ToList();
              tabs.Add((t.Name, list));
            }
            else if (t.Name == "PlayCount")
            {
              var list = allItems.OrderByDescending(a => a.PlayCount).Where(a => a.PlayCount > 0).ToList();
              tabs.Add((t.Name, list));
            }
            else if (t.Name == "Unwatched")
            {
              var allUnwatched = allItems.Where(x => x.PlayCount == 0);
              var k = 16000;
              var averageRating = allItems.Where(a => a.SiteRating.HasValue).Average(a => a.SiteRating);

              var list = allUnwatched.Where(a => a.Views.HasValue && a.SiteRating.HasValue).OrderByDescending(a =>
                      ((a.Views!.Value / (double)(a.Views.Value + k)) * a.SiteRating!.Value) +
                      ((k / (double)(a.Views.Value + k)) * averageRating)
                  ).ToList();

              tabs.Add((t.Name, list));
            }
            else if (t.Name == "Latest Unwatched")
            {
              var allUnwatched = allItems.Where(x => x.PlayCount == 0);
              var list = allUnwatched.OrderByDescending(v => long.Parse(v.SiteVideoId)).ToList();
              tabs.Add((t.Name, list));
            }
            else if (t.Name == "Best Unwatched")
            {
              var allUnwatched = allItems.Where(x => x.PlayCount == 0);
              var k = 16000;
              var averageRating = allItems.Where(a => a.SiteRating.HasValue).Average(a => a.SiteRating);

              var list = allUnwatched.Where(a => a.Views.HasValue && a.SiteRating.HasValue).OrderByDescending(a =>
                      ((a.Views!.Value / (double)(a.Views.Value + k)) * a.SiteRating!.Value) +
                      ((k / (double)(a.Views.Value + k)) * averageRating)
                  ).ToList();

              tabs.Add((t.Name, list));
            }

            break;

          case "CUSTOM":

            var matchingItems = allItems.AsQueryable();

            var tagsWL = JsonConvert.DeserializeObject<List<string>>(t.TagWhitelist) ?? new List<string>();
            var tagsBL = JsonConvert.DeserializeObject<List<string>>(t.TagBlacklist) ?? new List<string>();
            var acctressWL = JsonConvert.DeserializeObject<List<string>>(t.ActressWhitelist) ?? new List<string>();
            var acctressBL = JsonConvert.DeserializeObject<List<string>>(t.ActressBlacklist) ?? new List<string>();
            var videoWl = JsonConvert.DeserializeObject<List<string>>(t.VideoWhitelist) ?? new List<string>();
            var videoBl = JsonConvert.DeserializeObject<List<string>>(t.VideoBlacklist) ?? new List<string>();

            foreach (var item in tagsWL)
              matchingItems = matchingItems.Where(a => a.Tags.Any(t => t.Name == item));

            foreach (var item in tagsBL)
              matchingItems = matchingItems.Where(a => a.Tags.Any(t => t.Name == item) == false);

            foreach (var item in acctressWL)
              matchingItems = matchingItems.Where(a => a.Stars.Any(t => t.Name == item));

            foreach (var item in acctressBL)
              matchingItems = matchingItems.Where(a => a.Stars.Any(t => t.Name == item) == false);

            if (videoWl != null && videoWl.Any())
            {
              var videoIds = videoWl.Select(v => Convert.ToInt64(v)).ToHashSet();
              matchingItems = matchingItems.Where(a => videoIds.Contains(a.Id));
            }

            if (videoBl != null && videoBl.Any())
            {
              var videoExclIds = videoBl.Select(v => Convert.ToInt64(v)).ToHashSet();
              matchingItems = matchingItems.Where(a => !videoExclIds.Contains(a.Id));
            }

            var kCustom = 16000;
            var averageRatingCustom = matchingItems.Where(a => a.SiteRating.HasValue).Average(a => a.SiteRating);

            var customList = matchingItems
                .Where(a => a.Views.HasValue && a.SiteRating.HasValue)
                .OrderByDescending(a =>
                    ((a.Views!.Value / (double)(a.Views.Value + kCustom)) * a.SiteRating!.Value) +
                    ((kCustom / (double)(a.Views.Value + kCustom)) * averageRatingCustom)
                )
                .ToList();

            tabs.Add((t.Name, customList));

            break;

          case "WATCHLIST":
            var wlVideoIds = JsonConvert.DeserializeObject<List<string>>(t.VideoWhitelist);
            if (wlVideoIds != null && wlVideoIds.Any())
            {
              var wlIdSet = wlVideoIds.Select(v => Convert.ToInt64(v)).ToHashSet();
              var wlMatching = allItems.Where(a => wlIdSet.Contains(a.Id)).ToDictionary(a => a.Id);

              var wlOrdered = wlVideoIds
                  .Select(v => Convert.ToInt64(v))
                  .Where(id => wlMatching.ContainsKey(id))
                  .Select(id => wlMatching[id])
                  .ToList();

              tabs.Add((t.Name, wlOrdered));
            }
            break;
        }
      });

      return tabs;
    }
  }
}
