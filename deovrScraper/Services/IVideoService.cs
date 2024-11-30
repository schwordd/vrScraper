using deovrScraper.DB.Models;
using System;
using static deovrScraper.Services.VideoService;

namespace deovrScraper.Services
{
  public interface IVideoService
  {
    Task Initialize();
    Task<List<DbVideoItem>> GetVideoItems();
    Task<DbVideoItem> GetVideoById(long id);
    Task<List<(DbTag Tag, long Count)>> GetTagInfos();
    Task<List<(DbStar Star, long Count)>> GetActorInfos();

    void SetPlayedVideo(DbVideoItem vid);

    DbVideoItem LikeVideo(DbVideoItem vid);
    DbVideoItem DislikeVideo(DbVideoItem vid);
    DbVideoItem FavVideo(DbVideoItem vid);

    DbVideoItem? CurrentLiveVideo { get; }
    event LiveVideoChanged OnLiveVideoChanged;
  }
}
