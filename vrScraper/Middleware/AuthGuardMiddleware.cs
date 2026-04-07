using vrScraper.Services.Interfaces;

namespace vrScraper.Middleware
{
  public class AuthGuardMiddleware
  {
    private readonly RequestDelegate _next;
    private static readonly HashSet<string> _allowedPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
      "/login",
      "/api/auth",
      "/heresphere",
      "/deovr",
      "/api/videoproxy",
      "/api/events",
      "/_blazor",
      "/_framework",
      "/css",
      "/js",
      "/lib",
      "/fonts",
      "/favicon",
      "/banner.png"
    };

    public AuthGuardMiddleware(RequestDelegate next)
    {
      _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
      var path = context.Request.Path.Value ?? "/";

      // Always allow static/auth paths
      if (_allowedPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
          || path.Contains('.'))
      {
        await _next(context);
        return;
      }

      // Check if auth is configured
      var settingService = context.RequestServices.GetRequiredService<ISettingService>();
      var setting = await settingService.GetSetting("AuthPasswordHash");
      var hasPassword = !string.IsNullOrEmpty(setting?.Value);

      // If no password set, allow everything
      if (!hasPassword)
      {
        await _next(context);
        return;
      }

      // Password is set — require authentication
      if (context.User.Identity?.IsAuthenticated != true)
      {
        context.Response.Redirect("/login");
        return;
      }

      await _next(context);
    }
  }
}
