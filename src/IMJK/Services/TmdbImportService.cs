using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using IMDbClone.Data;
using IMDbClone.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IMDbClone.Services;

public interface ITmdbImportService
{
    Task<TmdbSyncResult> SyncOnStartupAsync(CancellationToken cancellationToken = default);
    Task<TmdbSyncResult> GetStatusAsync(CancellationToken cancellationToken = default);
}

public sealed class TmdbImportService(
    IHttpClientFactory clientFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<TmdbOptions> options,
    TimeProvider timeProvider,
    ILogger<TmdbImportService> logger) : ITmdbImportService
{
    public const string LastSyncSettingKey = "tmdb.last-successful-sync";
    private readonly TmdbOptions _options = options.Value;

    public async Task<TmdbSyncResult> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var lastSync = await ReadLastSync(db, cancellationToken);
        return new(_options.IsConfigured, true, 0, 0, lastSync,
            _options.IsConfigured ? "TMDB-Import ist eingerichtet." : "TMDB-Zugangsdaten fehlen.");
    }

    public async Task<TmdbSyncResult> SyncOnStartupAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var lastSync = await ReadLastSync(db, cancellationToken);
        if (!_options.IsConfigured)
            return new(false, true, 0, 0, lastSync, "TMDB-Import übersprungen: Zugangsdaten fehlen.");

        var now = timeProvider.GetUtcNow();
        var from = lastSync is null
            ? DateOnly.FromDateTime(now.UtcDateTime.AddDays(-Math.Clamp(_options.InitialLookbackDays, 1, 365)))
            : DateOnly.FromDateTime(lastSync.Value.UtcDateTime);
        var to = DateOnly.FromDateTime(now.UtcDateTime);
        var imported = 0;
        var updated = 0;

        try
        {
            var client = CreateClient();
            var summaries = await DiscoverMovies(client, from, to, cancellationToken);
            foreach (var summary in summaries.DistinctBy(x => x.Id))
            {
                var details = await GetDetails(client, summary.Id, cancellationToken);
                var existing = await db.Movies.FirstOrDefaultAsync(x => x.TmdbId == details.Id, cancellationToken)
                    ?? await db.Movies.FirstOrDefaultAsync(x => x.Title == details.Title && x.Year == details.ReleaseYear, cancellationToken);
                if (existing is null)
                {
                    existing = new Movie { TmdbId = details.Id, CreatedAt = now.UtcDateTime, Status = "approved", Source = "tmdb" };
                    db.Movies.Add(existing);
                    imported++;
                }
                else
                {
                    existing.TmdbId ??= details.Id;
                    updated++;
                }

                ApplyDetails(existing, details);
            }

            await db.SaveChangesAsync(cancellationToken);
            await WriteLastSync(db, now, cancellationToken);
            logger.LogInformation("TMDB import completed: {Imported} imported, {Updated} updated for {From} through {To}.", imported, updated, from, to);
            return new(true, true, imported, updated, now, $"{imported} neue und {updated} bekannte Filme verarbeitet.");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            logger.LogWarning(exception, "TMDB import failed. The previous successful sync timestamp remains unchanged.");
            return new(true, false, imported, updated, lastSync, "TMDB war nicht erreichbar; der Import wird beim nächsten Start wiederholt.");
        }
    }

    private HttpClient CreateClient()
    {
        var client = clientFactory.CreateClient("tmdb");
        if (!string.IsNullOrWhiteSpace(_options.ReadAccessToken))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ReadAccessToken.Trim());
        return client;
    }

    private async Task<List<TmdbMovieSummary>> DiscoverMovies(HttpClient client, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var movies = new List<TmdbMovieSummary>();
        var page = 1;
        var totalPages = 1;
        do
        {
            var path = $"discover/movie?include_adult=false&include_video=false&language={Uri.EscapeDataString(_options.Language)}" +
                $"&region={Uri.EscapeDataString(_options.Region)}&with_release_type=2%7C3" +
                $"&release_date.gte={from:yyyy-MM-dd}&release_date.lte={to:yyyy-MM-dd}&sort_by=release_date.asc&page={page}" + ApiKeyQuery();
            var response = await client.GetFromJsonAsync<TmdbDiscoverResponse>(path, cancellationToken)
                ?? throw new InvalidOperationException("TMDB returned an empty discovery response.");
            movies.AddRange(response.Results);
            totalPages = Math.Min(response.TotalPages, Math.Clamp(_options.MaxPagesPerSync, 1, 500));
            page++;
        } while (page <= totalPages);
        return movies;
    }

    private async Task<TmdbMovieDetails> GetDetails(HttpClient client, int id, CancellationToken cancellationToken)
    {
        var path = $"movie/{id}?language={Uri.EscapeDataString(_options.Language)}&append_to_response=credits" + ApiKeyQuery();
        var response = await client.GetFromJsonAsync<TmdbMovieDetailsResponse>(path, cancellationToken)
            ?? throw new InvalidOperationException($"TMDB returned no details for movie {id}.");
        var releaseDate = DateOnly.TryParseExact(response.ReleaseDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed : (DateOnly?)null;
        var director = response.Credits.Crew.FirstOrDefault(x => x.Job == "Director")?.Name ?? "";
        return new(response.Id, response.Title, releaseDate, response.Overview,
            response.PosterPath is null ? "" : $"https://image.tmdb.org/t/p/w780{response.PosterPath}",
            response.Genres.Select(x => x.Name).Where(x => x.Length > 0).Take(12).ToArray(),
            response.Credits.Cast.OrderBy(x => x.Order).Select(x => x.Name).Where(x => x.Length > 0).Take(12).ToArray(),
            director, Math.Round(response.VoteAverage, 1));
    }

    private string ApiKeyQuery() => string.IsNullOrWhiteSpace(_options.ApiKey)
        || !string.IsNullOrWhiteSpace(_options.ReadAccessToken)
        ? ""
        : $"&api_key={Uri.EscapeDataString(_options.ApiKey.Trim())}";

    private static void ApplyDetails(Movie movie, TmdbMovieDetails details)
    {
        movie.Title = details.Title;
        movie.Year = details.ReleaseYear;
        movie.ReleaseDate = details.ReleaseDate;
        movie.Plot = details.Overview;
        movie.ImageUrl = details.PosterUrl;
        movie.Genres = details.Genres;
        movie.Cast = details.Cast;
        movie.Director = details.Director;
        movie.TmdbRating = details.Rating;
        movie.Status = "approved";
        if (movie.Source == "local" && movie.Reviews.Count == 0) movie.Source = "tmdb";
    }

    private static async Task<DateTimeOffset?> ReadLastSync(AppDb db, CancellationToken cancellationToken)
    {
        var value = await db.Settings.Where(x => x.Key == LastSyncSettingKey).Select(x => x.Value).FirstOrDefaultAsync(cancellationToken);
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) ? parsed : null;
    }

    private static async Task WriteLastSync(AppDb db, DateTimeOffset value, CancellationToken cancellationToken)
    {
        var setting = await db.Settings.FindAsync([LastSyncSettingKey], cancellationToken);
        if (setting is null) db.Settings.Add(new AppSetting { Key = LastSyncSettingKey, Value = value.ToString("O") });
        else setting.Value = value.ToString("O");
        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed class TmdbDiscoverResponse
    {
        [JsonPropertyName("results")] public List<TmdbMovieSummary> Results { get; set; } = [];
        [JsonPropertyName("total_pages")] public int TotalPages { get; set; }
    }
    private sealed class TmdbMovieSummary { [JsonPropertyName("id")] public int Id { get; set; } }
    private sealed class TmdbMovieDetailsResponse
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("title")] public string Title { get; set; } = "";
        [JsonPropertyName("release_date")] public string ReleaseDate { get; set; } = "";
        [JsonPropertyName("overview")] public string Overview { get; set; } = "";
        [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
        [JsonPropertyName("vote_average")] public double VoteAverage { get; set; }
        [JsonPropertyName("genres")] public List<TmdbGenre> Genres { get; set; } = [];
        [JsonPropertyName("credits")] public TmdbCredits Credits { get; set; } = new();
    }
    private sealed class TmdbGenre { [JsonPropertyName("name")] public string Name { get; set; } = ""; }
    private sealed class TmdbCredits
    {
        [JsonPropertyName("cast")] public List<TmdbCast> Cast { get; set; } = [];
        [JsonPropertyName("crew")] public List<TmdbCrew> Crew { get; set; } = [];
    }
    private sealed class TmdbCast { [JsonPropertyName("name")] public string Name { get; set; } = ""; [JsonPropertyName("order")] public int Order { get; set; } }
    private sealed class TmdbCrew { [JsonPropertyName("name")] public string Name { get; set; } = ""; [JsonPropertyName("job")] public string Job { get; set; } = ""; }
    private sealed record TmdbMovieDetails(int Id, string Title, DateOnly? ReleaseDate, string Overview, string PosterUrl, string[] Genres, string[] Cast, string Director, double Rating)
    {
        public int ReleaseYear => ReleaseDate?.Year ?? DateTime.UtcNow.Year;
    }
}
