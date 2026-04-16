FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5001

# tzdata is required so TimeZoneInfo.FindSystemTimeZoneById(...) can resolve
# IANA zone IDs (e.g. "Europe/Berlin") that the frontend auto-detects from the browser.
# Scheduled scraping depends on this regardless of the TZ env var or mounted /etc/localtime.
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["vrScraper/vrScraper.csproj", "vrScraper/"]
RUN dotnet restore "vrScraper/vrScraper.csproj"
COPY . .
WORKDIR "/src/vrScraper"
RUN dotnet build "vrScraper.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "vrScraper.csproj" -c Release -o /app/publish \
    /p:RuntimeIdentifier=linux-x64 \
    /p:PublishSingleFile=false \
    /p:SelfContained=false \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
ENV TZ=Europe/Berlin
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "vrScraper.dll"]
