using Scalar.AspNetCore;
using vrScraper.DB;
using vrScraper.DB.Seed;
using vrScraper.Normalization;
using vrScraper.Normalization.Interfaces;
using vrScraper.Services;
using vrScraper.Services.Interfaces;
using vrScraper.Scrapers;
using vrScraper.Scrapers.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;
using Serilog.Filters;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace vrScraper
{
  public class Program
  {
    public static string Version { get => "1.0.0"; }

    public static async Task Main(string[] args)
    {
      var logo = @$"
 _    __     _____
| |  / /____/ ___/______________ _____  ___  _____
| | / / ___/\__ \/ ___/ ___/ __ `/ __ \/ _ \/ ___/
| |/ / /   ___/ / /__/ /  / /_/ / /_/ /  __/ /
|___/_/   /____/\___/_/   \__,_/ .___/\___/_/
                              /_/    v {GetVersion()}
";
      Console.WriteLine(logo);

      var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
      if (!string.IsNullOrEmpty(environment))
        Console.WriteLine($"Current Environment: {environment}");

      Log.Logger = SetupDefaultLogger();

      var builder = WebApplication.CreateBuilder(args);

      // Add appsettings.json to the configuration
      builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

      // Add appsettings.{environment}.json to the configuration
      builder.Configuration.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);

      // Add environment variables to the configuration
      builder.Configuration.AddEnvironmentVariables();

      // Add Serilog to the builder
      builder.Host.UseSerilog();

      // Configure Kestrel to listen on a configurable port
      var port = builder.Configuration.GetValue<int>("Port");
      builder.WebHost.ConfigureKestrel(serverOptions =>
      {
        serverOptions.ListenAnyIP(port);
      });

      // Add CORS services to the container.
      builder.Services.AddCors(options =>
      {
        options.AddPolicy("AllowAll", builder =>
        {
          builder.AllowAnyOrigin()
                 .AllowAnyMethod()
                 .AllowAnyHeader()
                 .WithExposedHeaders("Content-Length", "Content-Range", "Accept-Ranges", "Content-Type");
        });
      });

      // Füge HttpClient-Factory hinzu
      builder.Services.AddHttpClient();

      builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
          .AddCookie(options =>
          {
              options.LoginPath = "/login";
              options.LogoutPath = "/api/auth/logout";
              options.ExpireTimeSpan = TimeSpan.FromDays(30);
              options.SlidingExpiration = true;
              options.Cookie.HttpOnly = true;
              options.Cookie.SameSite = SameSiteMode.Lax;
          });
      builder.Services.AddAuthorization(options =>
      {
          options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
              .RequireAuthenticatedUser().Build();
      });

      // Add services to the container.
      builder.Services.AddControllers().AddNewtonsoftJson(options =>
      {
        options.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
        options.SerializerSettings.Formatting = Newtonsoft.Json.Formatting.Indented;
      });


      builder.Services.AddRazorPages(); // Fügt Razor Pages hinzu
      builder.Services.AddServerSideBlazor(); // Fügt Blazor Server hinzu

      builder.Services.AddOpenApi();
      builder.Services.AddSingleton<IVideoService, VideoService>();
      builder.Services.AddSingleton<IEpornerScraper, EpornerScraper>();
      builder.Services.AddSingleton<IVideoScraper>(sp => sp.GetRequiredService<IEpornerScraper>());
      builder.Services.AddSingleton<IXhamsterVrScraper, XhamsterVrScraper>();
      builder.Services.AddSingleton<IVideoScraper>(sp => sp.GetRequiredService<IXhamsterVrScraper>());
      builder.Services.AddSingleton<ISpankBangVrScraper, SpankBangVrScraper>();
      builder.Services.AddSingleton<IVideoScraper>(sp => sp.GetRequiredService<ISpankBangVrScraper>());
      builder.Services.AddSingleton<IScraperRegistry, ScraperRegistry>();
      builder.Services.AddSingleton<ITabService, TabService>();
      builder.Services.AddSingleton<ISettingService, SettingService>();
      builder.Services.AddSingleton<ITabFilteringService, TabFilteringService>();
      builder.Services.AddSingleton<IRecommendationService, RecommendationService>();
      builder.Services.AddSingleton<IScrapeLogService, ScrapeLogService>();
      builder.Services.AddSingleton<ITagNormalizationService, TagNormalizationService>();
      builder.Services.AddSingleton<ITitleNormalizationService, TitleNormalizationService>();
      builder.Services.AddSingleton<IThePornDbService, ThePornDbService>();
      // Add scheduled scraping background service
      builder.Services.AddHostedService<ScheduledScrapingService>();


      // Register the ILogger service with DI
      builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));

      // Add DbContext with SQLite, use connection string from appsettings.json
      var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
      builder.Services.AddDbContext<VrScraperContext>(options =>
          options.UseSqlite(connectionString));

      // Configure global settings for Newtonsoft.Json
      JsonConvert.DefaultSettings = () => new JsonSerializerSettings
      {
        ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
        Formatting = Newtonsoft.Json.Formatting.Indented
      };

      var app = builder.Build();

      // Configure the HTTP request pipeline.
      if (app.Environment.IsDevelopment())
      {
        app.MapOpenApi();
        app.MapScalarApiReference();
      }

      // Apply any pending migrations
      using (var scope = app.Services.CreateScope())
      {
        var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
        var pending = context.Database.GetPendingMigrations().ToList();
        if (pending.Count > 0)
        {
          Console.WriteLine($"Applying {pending.Count} pending migration(s): {string.Join(", ", pending)}");
          Console.WriteLine("This may take a few minutes for large data migrations...");
          // Disable FK checks before migration so bulk INSERT...SELECT is fast
          context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
        }
        context.Database.Migrate();
        // Always ensure FK checks are on after migration
        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
        if (pending.Count > 0)
          Console.WriteLine("Database migrations complete.");

        //seeding
        DbDefaults.SeedDefaultSettings(context);
        DbDefaults.SeedDefaultTabs(context);

        // Fix SiteRating values that were stored unnormalized (raw percentage instead of 0-1)
        int fixedRatings = context.Database.ExecuteSqlRaw(
            "UPDATE VideoItems SET SiteRating = SiteRating / 100.0 WHERE SiteRating > 1.0");
        if (fixedRatings > 0)
          Console.WriteLine($"Fixed {fixedRatings} video(s) with unnormalized SiteRating.");
      }

      // Enable CORS
      app.UseCors("AllowAll");

      app.UseStaticFiles(new StaticFileOptions
      {
        OnPrepareResponse = ctx =>
        {
          ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=604800"; // 7 days
        }
      });

      app.UseRouting();
      app.UseAuthentication();
      app.UseAuthorization();


      // Map Blazor Hub (AllowAnonymous so login page works; Blazor handles auth via AuthorizeRouteView)
      app.MapBlazorHub().AllowAnonymous();
      app.MapFallbackToPage("/_Host").AllowAnonymous();

      app.MapControllers();


      // Retrieve the service from the DI container and call the method
      var settingService = app.Services.GetRequiredService<ISettingService>();
      await settingService.Initialize();

      var videoService = app.Services.GetRequiredService<IVideoService>();
      await videoService.Initialize();

      var epornerScraper = app.Services.GetRequiredService<IEpornerScraper>();
      epornerScraper.Initialize();

      var xhamsterScraper = app.Services.GetRequiredService<IXhamsterVrScraper>();
      xhamsterScraper.Initialize();

      var spankBangScraper = app.Services.GetRequiredService<ISpankBangVrScraper>();
      spankBangScraper.Initialize();

      var tabService = app.Services.GetRequiredService<ITabService>();
      await tabService.Initialize();

      // Graceful shutdown: finish current playback tracking
      var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
      lifetime.ApplicationStopping.Register(() =>
      {
        videoService.FinishCurrentPlayback();
      });

      // Open the browser unless --headless flag is set
      var url = $"http://127.0.0.1:{builder.Configuration.GetValue<int>("Port")}";
      if (!args.Contains("--headless"))
        _ = Task.Run(() => OpenBrowser(url));
      else
        Console.WriteLine($"Headless mode — listening on {url}");

      app.Run();
    }

    private static Serilog.Core.Logger SetupDefaultLogger()
    {
      var template = "[{Timestamp:yyyy.MM.dd HH:mm:ss.fff}] {Level:u3} {SourceContext}: {Message:lj}{NewLine}{Exception}";

      var config = new ConfigurationBuilder()
      .AddJsonFile("appsettings.json", optional: false)
      .Build();

      var loggerConfig = new LoggerConfiguration()
        .ReadFrom.Configuration(config)
        .Enrich.FromLogContext()
        .Filter.ByExcluding(Matching.FromSource("Microsoft.AspNetCore.Hosting.Diagnostics"))
        .Filter.ByExcluding(Matching.FromSource("Microsoft.AspNetCore.Server.Kestrel"))
        .Filter.ByExcluding(Matching.FromSource("System.Net.Http.HttpClient.ProxyKitClient"))
        .Filter.ByExcluding(Matching.FromSource("Microsoft.AspNetCore.Routing"))
        .Filter.ByExcluding(Matching.FromSource("Microsoft.AspNetCore.Mvc"))
        .WriteTo.Console(outputTemplate: template, theme: AnsiConsoleTheme.Literate);

      var logPath = config.GetSection("appSettings").GetValue<string>("logPath");

      if (logPath != null)
      {
        loggerConfig.WriteTo.File(logPath, outputTemplate: template, rollOnFileSizeLimit: true);
      }

      return loggerConfig.CreateLogger();
    }

    private static void OpenBrowser(string url)
    {
      try
      {
        // Check if OS is Windows
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
          Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
        }
        // Check if OS is macOS
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
          Process.Start("open", url);
        }
        // Assume Linux
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
          Process.Start("xdg-open", url);
        }
      }
      catch (Exception ex)
      {
        Log.Error("Failed to open browser: {Error}", ex.Message);
      }
    }

    public static string GetVersion()
    {
      var assembly = Assembly.GetExecutingAssembly();
      var resourceName = "vrScraper.VERSION";

      if (assembly == null) throw new Exception("Could not determine ExecutingAssembly");

      using Stream? stream = assembly.GetManifestResourceStream(resourceName);

      if (stream == null) throw new Exception($"Could not load ManifestResourceStream for {resourceName}");

      using StreamReader reader = new(stream);
      return reader.ReadToEnd();
    }
  }
}
