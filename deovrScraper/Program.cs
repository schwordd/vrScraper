using Blzr.BootstrapSelect;
using deovrScraper.DB;
using deovrScraper.DB.Seed;
using deovrScraper.Services;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;
using Serilog.Filters;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace deovrScraper
{
  public class Program
  {
    public static string Version { get => "1.0.0"; }

    public static void Main(string[] args)
    {
      var logo = @$"

    ____           _    __     _____                                
   / __ \___  ____| |  / /____/ ___/______________ _____  ___  _____
  / / / / _ \/ __ \ | / / ___/\__ \/ ___/ ___/ __ `/ __ \/ _ \/ ___/
 / /_/ /  __/ /_/ / |/ / /   ___/ / /__/ /  / /_/ / /_/ /  __/ /    
/_____/\___/\____/|___/_/   /____/\___/_/   \__,_/ .___/\___/_/     
                                                /_/    v{GetVersion()}

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
                 .AllowAnyHeader();
        });
      });

      // Add services to the container.
      builder.Services.AddControllers().AddNewtonsoftJson(options =>
      {
        options.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
        options.SerializerSettings.Formatting = Newtonsoft.Json.Formatting.Indented;
      });


      builder.Services.AddRazorPages(); // Fügt Razor Pages hinzu
      builder.Services.AddServerSideBlazor(); // Fügt Blazor Server hinzu

      builder.Services.AddEndpointsApiExplorer();
      builder.Services.AddSwaggerGen();
      builder.Services.AddSingleton<IVideoService, VideoService>();
      builder.Services.AddSingleton<IEpornerScraper, EpornerScraper>();
      builder.Services.AddSingleton<ITabService, TabService>();
      builder.Services.AddBootstrapSelect();


      // Register the ILogger service with DI
      builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));

      // Add DbContext with SQLite, use connection string from appsettings.json
      var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
      builder.Services.AddDbContext<DeovrScraperContext>(options =>
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
        app.UseSwagger();
        app.UseSwaggerUI();
      }

      // Apply any pending migrations
      using (var scope = app.Services.CreateScope())
      {
        var context = scope.ServiceProvider.GetRequiredService<DeovrScraperContext>();
        context.Database.Migrate();

        //seeding
        DbDefaults.SeedDefaultTabs(context);
      }

      app.UseRouting();

      // Enable CORS
      app.UseCors("AllowAll");
      app.UseAuthorization();

      app.UseStaticFiles();


      // Map Blazor Hub
      app.MapBlazorHub(); // Blazor Hub
      app.MapFallbackToPage("/_Host"); // Fallback auf Blazor-Seite

      app.MapControllers();


      // Retrieve the service from the DI container and call the method
      var videoService = app.Services.GetRequiredService<IVideoService>();
      videoService.Initialize();

      var epornerScraper = app.Services.GetRequiredService<IEpornerScraper>();
      epornerScraper.Initialize();

      var tabService = app.Services.GetRequiredService<ITabService>();
      tabService.Initialize();

      // Open the browser asynchronously
      var url = $"http://{builder.Configuration.GetValue<string>("Ip")}:{builder.Configuration.GetValue<int>("Port")}";
      Task.Run(() => OpenBrowser(url));

      app.Run();
    }

    private static Serilog.ILogger SetupDefaultLogger()
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
      var resourceName = "deovrScraper.VERSION";

      if (assembly == null) throw new Exception("Could not determine ExecutingAssembly");

      using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
      {
        if (stream == null) throw new Exception($"Could not load ManifestResourceStream for {resourceName}");

        using (StreamReader reader = new StreamReader(stream))
        {
          return reader.ReadToEnd();
        }
      }
    }
  }
}
