using vrScraper.DB.Models;
using System;
using static vrScraper.Services.VideoService;

namespace vrScraper.Services
{
  public interface IVideoService
  {
    Task Initialize();
    Task<List<DbVideoItem>> GetVideoItems();
    Task<DbVideoItem> GetVideoById(long id);
    Task<List<(DbTag Tag, long Count)>> GetTagInfos();
    Task<List<(DbStar Star, long Count)>> GetActorInfos();
    Task DeleteVideo(long id);

    void SetPlayedVideo(DbVideoItem vid);

    DbVideoItem LikeVideo(DbVideoItem vid);
    DbVideoItem DislikeVideo(DbVideoItem vid);
    DbVideoItem FavVideo(DbVideoItem vid);

    DbVideoItem? CurrentLiveVideo { get; }
    event LiveVideoChanged OnLiveVideoChanged;
  }
}
