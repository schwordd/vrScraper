FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
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
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "vrScraper.dll"]
