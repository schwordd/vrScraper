using vrScraper.DB.Models;

namespace vrScraper.Services
{
  public interface ITabService
  {
    Task Initialize();
    Task<List<DbDeoVrTab>> GetAllTabs();
    Task AddTab(DbDeoVrTab newTab);
    Task DeleteTab(long id);
    Task UpdateTab(DbDeoVrTab tab);

  }
}
