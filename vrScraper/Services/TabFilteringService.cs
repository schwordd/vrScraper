using vrScraper.DB;
using vrScraper.DB.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace vrScraper.Services
{
  public class TabFilteringService(IServiceScopeFactory scopeFactory, IRecommendationService recommendationService, ISettingService settingService) : ITabFilteringService
  {
    public async Task<List<(string Name, List<DbVideoItem> Videos)>> GetFilteredTabVideos(
        List<DbVideoItem> allItems,
        List<string> globalBlackList)
    {
      var tabs = new List<(string Name, List<DbVideoItem> Videos)>();

      // Apply global tag blacklist
      allItems = allItems.Where(item => !item.Tags.Exists(a => globalBlackList.Any(b => b == a.Name))).ToList();

      List<string> SafeDeserializeList(string? json) {
        try { return JsonConvert.DeserializeObject<List<string>>(json ?? "[]") ?? new List<string>(); }
        catch { return new List<string>(); }
      }

      using var scope = scopeFactory.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

      var tabConfigs = await context.Tabs.Where(t => t.Active).OrderBy(t => t.Order).ToListAsync();
      foreach (var t in tabConfigs)
      {
        switch (t.Type)
        {
          case "DEFAULT":
            if (t.Name == "Latest")
            {
              var list = allItems.OrderByDescending(v => v.Id).ToList();
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
              var ratedItems = allItems.Where(a => a.SiteRating.HasValue);
              var averageRating = ratedItems.Any() ? ratedItems.Average(a => a.SiteRating) : 0.5;

              var list = allUnwatched.Where(a => a.Views.HasValue && a.SiteRating.HasValue).OrderByDescending(a =>
                      ((a.Views!.Value / (double)(a.Views.Value + k)) * a.SiteRating!.Value) +
                      ((k / (double)(a.Views.Value + k)) * averageRating)
                  ).ToList();

              tabs.Add((t.Name, list));
            }
            else if (t.Name == "Latest Unwatched")
            {
              var allUnwatched = allItems.Where(x => x.PlayCount == 0);
              var list = allUnwatched.OrderByDescending(v => v.Id).ToList();
              tabs.Add((t.Name, list));
            }
            else if (t.Name == "Best Unwatched")
            {
              var allUnwatched = allItems.Where(x => x.PlayCount == 0);
              var k = 16000;
              var ratedItems2 = allItems.Where(a => a.SiteRating.HasValue);
              var averageRating = ratedItems2.Any() ? ratedItems2.Average(a => a.SiteRating) : 0.5;

              var list = allUnwatched.Where(a => a.Views.HasValue && a.SiteRating.HasValue).OrderByDescending(a =>
                      ((a.Views!.Value / (double)(a.Views.Value + k)) * a.SiteRating!.Value) +
                      ((k / (double)(a.Views.Value + k)) * averageRating)
                  ).ToList();

              tabs.Add((t.Name, list));
            }
            else if (t.Name == "Recommended")
            {
              var recommendedSetting = await settingService.GetSetting("RecommendationsEnabled");
              if (recommendedSetting != null && bool.TryParse(recommendedSetting.Value, out bool recEnabled) && recEnabled)
              {
                var recommended = recommendationService.GetRecommendedVideos(allItems)
                  .Select(s => s.Video).ToList();
                if (recommended.Any())
                {
                  tabs.Add((t.Name, recommended));
                }
              }
            }

            break;

          case "CUSTOM":

            var matchingItems = allItems.AsQueryable();

            var siteFilter = SafeDeserializeList(t.SiteFilter);
            if (siteFilter.Any())
            {
              matchingItems = matchingItems.Where(v => siteFilter.Contains(v.Site));
            }

            var tagsWL = SafeDeserializeList(t.TagWhitelist);
            var tagsBL = SafeDeserializeList(t.TagBlacklist);
            var acctressWL = SafeDeserializeList(t.ActressWhitelist);
            var acctressBL = SafeDeserializeList(t.ActressBlacklist);
            var videoWl = SafeDeserializeList(t.VideoWhitelist);
            var videoBl = SafeDeserializeList(t.VideoBlacklist);

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
            var ratedCustomItems = matchingItems.Where(a => a.SiteRating.HasValue);
            var averageRatingCustom = ratedCustomItems.Any() ? ratedCustomItems.Average(a => a.SiteRating) : 0.5;

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
            var wlSiteFilter = SafeDeserializeList(t.SiteFilter);
            var wlItems = allItems;
            if (wlSiteFilter.Any())
            {
              wlItems = wlItems.Where(v => wlSiteFilter.Contains(v.Site)).ToList();
            }

            var wlVideoIds = SafeDeserializeList(t.VideoWhitelist);
            if (wlVideoIds != null && wlVideoIds.Any())
            {
              var wlIdSet = wlVideoIds.Select(v => Convert.ToInt64(v)).ToHashSet();
              var wlMatching = wlItems.Where(a => wlIdSet.Contains(a.Id)).ToDictionary(a => a.Id);

              var wlOrdered = wlVideoIds
                  .Select(v => Convert.ToInt64(v))
                  .Where(id => wlMatching.ContainsKey(id))
                  .Select(id => wlMatching[id])
                  .ToList();

              tabs.Add((t.Name, wlOrdered));
            }
            break;
        }
      }

      return tabs;
    }
  }
}
