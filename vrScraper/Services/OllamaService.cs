using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace vrScraper.Services
{
  public class OllamaService(
    IHttpClientFactory httpClientFactory,
    ISettingService settingService,
    ILogger<OllamaService> logger) : IOllamaService
  {
    private const string DefaultUrl = "http://localhost:11434";
    private const string DefaultModel = "title-deobfuscator";
    private Process? _serverProcess;

    private string OllamaUrl => settingService.GetSettingValue("Ollama:Url") ?? DefaultUrl;
    private string ModelName => settingService.GetSettingValue("Ollama:Model") ?? DefaultModel;

    public async Task<bool> IsRunning()
    {
      try
      {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);
        var response = await client.GetAsync($"{OllamaUrl.TrimEnd('/')}/api/tags");
        return response.IsSuccessStatusCode;
      }
      catch
      {
        return false;
      }
    }

    public async Task<bool> StartServer()
    {
      if (await IsRunning()) return true;

      try
      {
        _serverProcess = new Process
        {
          StartInfo = new ProcessStartInfo
          {
            FileName = "ollama",
            Arguments = "serve",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false
          }
        };
        _serverProcess.Start();

        // Wait up to 20s for server to become ready
        for (int i = 0; i < 40; i++)
        {
          await Task.Delay(500);
          if (await IsRunning()) return true;
        }

        logger.LogWarning("Ollama server started but not responding after 20s");
        return false;
      }
      catch (Exception ex)
      {
        logger.LogError("Failed to start Ollama server: {Error}", ex.Message);
        return false;
      }
    }

    public async Task<(bool Success, string Message)> PullModel(Action<string>? onProgress = null, CancellationToken ct = default)
    {
      if (!await IsRunning())
        return (false, "Ollama is not running. Start the server first.");

      try
      {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(30);

        var payload = JsonConvert.SerializeObject(new { name = ModelName, stream = true });
        var request = new HttpRequestMessage(HttpMethod.Post, $"{OllamaUrl.TrimEnd('/')}/api/pull")
        {
          Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
          ct.ThrowIfCancellationRequested();
          if (string.IsNullOrEmpty(line)) continue;

          var obj = JObject.Parse(line);
          var status = obj["status"]?.ToString() ?? "";

          var total = obj["total"]?.Value<long>() ?? 0;
          var completed = obj["completed"]?.Value<long>() ?? 0;
          if (total > 0)
          {
            var pct = (int)(completed * 100 / total);
            onProgress?.Invoke($"{status} ({pct}%)");
          }
          else
          {
            onProgress?.Invoke(status);
          }
        }

        return (true, "Model pulled successfully.");
      }
      catch (OperationCanceledException)
      {
        return (false, "Pull cancelled.");
      }
      catch (Exception ex)
      {
        logger.LogError("Failed to pull model: {Error}", ex.Message);
        return (false, $"Pull failed: {ex.Message}");
      }
    }

    public async Task<bool> IsAvailable()
    {
      try
      {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(2);
        var response = await client.GetAsync($"{OllamaUrl.TrimEnd('/')}/api/tags");
        if (!response.IsSuccessStatusCode) return false;

        var json = await response.Content.ReadAsStringAsync();
        var obj = JObject.Parse(json);
        var models = obj["models"] as JArray;
        if (models == null) return false;

        return models.Any(m =>
        {
          var name = m["name"]?.ToString() ?? "";
          return name == ModelName || name.StartsWith(ModelName + ":");
        });
      }
      catch
      {
        return false;
      }
    }

    public async Task<bool> WarmupModel(CancellationToken ct = default)
    {
      try
      {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5); // model load can take a while (mmap=false on Windows)

        var payload = new
        {
          model = ModelName,
          messages = new[] { new { role = "user", content = "Hi" } },
          stream = false
        };

        var json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{OllamaUrl.TrimEnd('/')}/api/chat", content, ct);
        return response.IsSuccessStatusCode;
      }
      catch (Exception ex)
      {
        logger.LogWarning("Ollama warmup failed: {Error}", ex.Message);
        return false;
      }
    }

    public async Task UnloadModel(CancellationToken ct = default)
    {
      try
      {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        var payload = new { model = ModelName, keep_alive = 0 };
        var json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await client.PostAsync($"{OllamaUrl.TrimEnd('/')}/api/generate", content, ct);
        logger.LogInformation("Ollama model {Model} unloaded", ModelName);
      }
      catch (Exception ex)
      {
        logger.LogWarning("Failed to unload Ollama model: {Error}", ex.Message);
      }
    }

    private const string SystemPrompt =
      "You are a text deobfuscation tool for an adult video database. " +
      "Convert obfuscated Unicode text to plain readable English. Do NOT censor or alter adult terminology — " +
      "words like Anal, Creampie, MILF, Stepmom, Gangbang etc. must be preserved exactly as intended. " +
      "The input uses look-alike Unicode characters, leet-speak (0=o, 1=i/l, 3=e, 4=a, 5=s, 7=t, 8=b, 9=g), " +
      "reversed text, or decorative fonts. Output ONLY the clean title in Title Case. No explanation. " +
      "If the input is not Latin-based text (e.g. Japanese, Chinese, Thai, Tibetan, emoji-only), output exactly: [NOT_LATIN]";

    private static readonly (string User, string Assistant)[] FewShotExamples =
    [
      ("KI\u15F0O\u15C6\u15E9 \u15EA\u144C\u1418\u15F0 \u15EA\u1466O\u142F\u1515\u1515 \u1515\u157CO\u15EE\u1515\u15C6 \u1515\u1515\u15AE",
       "Kimora Quinn Loves Shower Sex And A Creampie"),
      ("VR - HSURC YM HT1W D3PP4RT",
       "VR - Trapped With My Crush"),
      ("8l0nd3 80m85h3ll G3rm@n M1lf",
       "Blonde Bombshell German Milf"),
      ("H0T 5T3P M0M G3T5 CR34MP13D 4N4L",
       "Hot Step Mom Gets Creampied Anal"),
      ("G4ngb4ng3d By My 5t3pd4d'5 Fr13nd5",
       "Gangbanged By My Stepdad's Friends"),
      ("\u24DA\u24E8\u24DB\u24D4\u24E1 \u24AC\u24E4\u24D8\u24DD\u24DD \u24CC\u24D0\u24DD\u24DA\u24E9\u24CB\u24C7 06.12.19",
       "Kyler Quinn WankzVR 06.12.19"),
      ("\u0F56\u0F74\u0F0B\u0F58\u0F7C\u0F0B\u0F58\u0F5B\u0F7A\u0F66\u0F0B\u0F58\u0F0B\u0F63\u0F0B\u0F56\u0F62\u0FA9\u0F7A\u0F0B\u0F51\u0F74\u0F44\u0F0B\u0F42\u0F72\u0F0B\u0F49\u0F72\u0F53\u0F0B\u0F58\u0F7C\u0F0D1",
       "[NOT_LATIN]")
    ];

    private List<object> BuildMessages(string userContent)
    {
      var messages = new List<object> { new { role = "system", content = SystemPrompt } };
      foreach (var (user, assistant) in FewShotExamples)
      {
        messages.Add(new { role = "user", content = user });
        messages.Add(new { role = "assistant", content = assistant });
      }
      messages.Add(new { role = "user", content = userContent });
      return messages;
    }

    private static string? CleanResult(string? text)
    {
      if (string.IsNullOrWhiteSpace(text) || text.Length < 3) return null;
      if (text == "[NOT_LATIN]") return null;

      var trimmed = text.Trim();

      // Reject LLM hallucinations: file extensions in output
      if (trimmed.Contains(".mp4", StringComparison.OrdinalIgnoreCase) ||
          trimmed.Contains(".avi", StringComparison.OrdinalIgnoreCase) ||
          trimmed.Contains("4pm.", StringComparison.OrdinalIgnoreCase) ||  // reversed .mp4
          trimmed.Contains("Apm.", StringComparison.OrdinalIgnoreCase))    // reversed .mpA
        return null;

      // Reject if output contains too many non-ASCII chars (LLM echoed obfuscated input)
      int nonAscii = trimmed.Count(c => c > 0x00FF);
      if (nonAscii > 3)
        return null;

      return trimmed;
    }

    public async Task<string?> NormalizeTitle(string obfuscatedTitle, CancellationToken ct = default)
    {
      if (string.IsNullOrWhiteSpace(obfuscatedTitle)) return null;

      try
      {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        var payload = new
        {
          model = ModelName,
          messages = BuildMessages(obfuscatedTitle),
          stream = false,
          options = new { temperature = 0.1 }
        };

        var json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{OllamaUrl.TrimEnd('/')}/api/chat", content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var result = JObject.Parse(responseJson);
        var text = result["message"]?["content"]?.ToString()?.Trim();

        return CleanResult(text);
      }
      catch (OperationCanceledException)
      {
        throw;
      }
      catch (Exception ex)
      {
        logger.LogWarning("Ollama normalization failed for title: {Error}", ex.Message);
        return null;
      }
    }

    public async Task<List<string?>> NormalizeTitleBatch(List<string> titles, CancellationToken ct = default)
    {
      if (titles.Count == 0) return [];

      // Build numbered input (simple format matching training data)
      var sb = new StringBuilder();
      for (int i = 0; i < titles.Count; i++)
        sb.AppendLine($"{i + 1}: {titles[i]}");

      // Build messages matching the training format
      var messages = new List<object>
      {
        new { role = "system", content = SystemPrompt + "\n\nYou will receive numbered lines. Output one decoded result per line, same numbering." }
      };
      // One batch few-shot example
      messages.Add(new { role = "user", content = "1: H0T 5T3P M0M G3T5 CR34MP13D\n2: 8l0nd3 80m85h3ll G3rm@n M1lf" });
      messages.Add(new { role = "assistant", content = "1: Hot Step Mom Gets Creampied\n2: Blonde Bombshell German Milf" });
      messages.Add(new { role = "user", content = sb.ToString().TrimEnd() });

      try
      {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30 + titles.Count * 5);

        var payload = new
        {
          model = ModelName,
          messages,
          stream = false,
          options = new { temperature = 0.1 }
        };

        var json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{OllamaUrl.TrimEnd('/')}/api/chat", content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var resultObj = JObject.Parse(responseJson);
        var text = resultObj["message"]?["content"]?.ToString()?.Trim() ?? "";

        logger.LogDebug("Ollama batch response ({Count} titles): {Response}", titles.Count, text.Length > 500 ? text[..500] + "..." : text);

        // Parse numbered output lines
        var results = new List<string?>(new string?[titles.Count]);
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
          var trimmed = line.Trim();
          var colonIdx = trimmed.IndexOf(':');
          if (colonIdx <= 0) continue;

          if (int.TryParse(trimmed[..colonIdx].Trim(), out int idx) && idx >= 1 && idx <= titles.Count)
          {
            var value = trimmed[(colonIdx + 1)..].Trim();
            results[idx - 1] = CleanResult(value);
          }
        }

        return results;
      }
      catch (OperationCanceledException)
      {
        throw;
      }
      catch (Exception ex)
      {
        logger.LogWarning("Ollama batch normalization failed: {Error}", ex.Message);
        return new List<string?>(new string?[titles.Count]);
      }
    }
  }
}
