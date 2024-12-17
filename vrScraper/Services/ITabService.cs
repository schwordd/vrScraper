using vrScraper.DB.Models;

namespace vrScraper.Services
{
  public interface ITabService
  {
    Task Initialize();
    Task<List<DbVrTab>> GetAllTabs();
    Task AddTab(DbVrTab newTab);
    Task DeleteTab(long id);
    Task UpdateTab(DbVrTab tab);

  }
}
