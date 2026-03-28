namespace vrScraper.Services
{
  public interface IOllamaService
  {
    Task<bool> IsAvailable();
    Task<bool> IsRunning();
    Task<bool> StartServer();
    Task<(bool Success, string Message)> PullModel(Action<string>? onProgress = null, CancellationToken ct = default);
    Task<bool> WarmupModel(CancellationToken ct = default);
    Task UnloadModel(CancellationToken ct = default);
    Task<string?> NormalizeTitle(string obfuscatedTitle, CancellationToken ct = default);
    Task<List<string?>> NormalizeTitleBatch(List<string> titles, CancellationToken ct = default);
  }
}
