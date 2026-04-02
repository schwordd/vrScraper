using vrScraper.DB.Models;
using System;
using static vrScraper.Services.VideoService;

namespace vrScraper.Services.Interfaces
{
  public interface IVideoService
  {
    Task Initialize();
    Task ReloadVideos();
    Task<List<DbVideoItem>> GetVideoItems();
    Task<DbVideoItem?> GetVideoById(long id);
    Task<List<(DbTag Tag, long Count)>> GetTagInfos();
    Task<List<(DbStar Star, long Count)>> GetActorInfos();
    Task DeleteVideo(long id);
    Task<bool> UpdateVideoLikeStatus(long id, bool liked);
    Task<bool> UpdateVideoErrorCount(long id);
    Task<bool> UpdateVideoRating(long id, double rating);

    void SetPlayedVideo(DbVideoItem vid, [System.Runtime.CompilerServices.CallerMemberName] string? caller = null, string? callerSource = null);
    DbVideoItem? FinishCurrentPlayback();

    DbVideoItem LikeVideo(DbVideoItem vid);
    DbVideoItem DislikeVideo(DbVideoItem vid);
    DbVideoItem FavVideo(DbVideoItem vid);

    DbVideoItem? CurrentLiveVideo { get; }
    event LiveVideoChanged OnLiveVideoChanged;
  }
}
