using System.Net;
using IMDbClone.Data;
using IMDbClone.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MovieApp.Tests;

public class TmdbImportTests
{
    private const string Details = """{"id":123,"title":"Neuer Film","release_date":"2026-10-08","overview":"Deutsche Beschreibung","poster_path":"/poster.jpg","vote_average":7.8,"genres":[{"name":"Drama"}],"credits":{"cast":[{"name":"Darsteller Eins","order":0}],"crew":[{"name":"Regie Person","job":"Director"}]}}""";

    [Fact]
    public async Task StartupSync_ImportsDetailsCreditsPosterAndTimestamp()
    {
        var dir = TestSupport.TempDir();
        try
        {
            var database = Path.Combine(dir, "filmportal.db");
            await CreateDatabase(database);
            var handler = SuccessfulHandler();
            var now = new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero);
            var result = await TestSupport.TmdbService(database, handler, now).SyncOnStartupAsync();
            Assert.True(result.Success);
            Assert.Equal(1, result.Imported);
            await using var db = NewDb(database);
            var movie = await db.Movies.SingleAsync();
            Assert.Equal(123, movie.TmdbId);
            Assert.Equal("Neuer Film", movie.Title);
            Assert.Equal(new DateOnly(2026, 10, 8), movie.ReleaseDate);
            Assert.Equal("https://image.tmdb.org/t/p/w780/poster.jpg", movie.ImageUrl);
            Assert.Equal("Regie Person", movie.Director);
            Assert.Equal("Darsteller Eins", Assert.Single(movie.Cast));
            Assert.Equal("Drama", Assert.Single(movie.Genres));
            Assert.Equal(7.8, movie.TmdbRating);
            Assert.NotNull(await db.Settings.FindAsync(TmdbImportService.LastSyncSettingKey));
            Assert.All(handler.Requests, uri => Assert.DoesNotContain("test-token", uri.ToString()));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task SecondStartup_UsesLastSyncAndDoesNotDuplicateMovies()
    {
        var dir = TestSupport.TempDir();
        try
        {
            var database = Path.Combine(dir, "filmportal.db");
            await CreateDatabase(database);
            var firstNow = new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);
            await TestSupport.TmdbService(database, SuccessfulHandler(), firstNow).SyncOnStartupAsync();
            var secondHandler = SuccessfulHandler();
            var result = await TestSupport.TmdbService(database, secondHandler, firstNow.AddDays(1)).SyncOnStartupAsync();
            Assert.Equal(0, result.Imported);
            Assert.Equal(1, result.Updated);
            Assert.Contains("release_date.gte=2026-10-07", secondHandler.Requests[0].Query);
            await using var db = NewDb(database);
            Assert.Equal(1, await db.Movies.CountAsync());
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task FailedSync_DoesNotAdvanceLastSuccessfulTimestamp()
    {
        var dir = TestSupport.TempDir();
        try
        {
            var database = Path.Combine(dir, "filmportal.db");
            await CreateDatabase(database);
            var handler = new StubHttpHandler(_ => StubHttpHandler.Json("{}", HttpStatusCode.ServiceUnavailable));
            var result = await TestSupport.TmdbService(database, handler, DateTimeOffset.UtcNow).SyncOnStartupAsync();
            Assert.False(result.Success);
            await using var db = NewDb(database);
            Assert.Null(await db.Settings.FindAsync(TmdbImportService.LastSyncSettingKey));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task MissingCredentials_SkipsNetworkAndLeavesApplicationUsable()
    {
        var dir = TestSupport.TempDir();
        try
        {
            var database = Path.Combine(dir, "filmportal.db");
            await CreateDatabase(database);
            var handler = SuccessfulHandler();
            var result = await TestSupport.TmdbService(database, handler, DateTimeOffset.UtcNow, new TmdbOptions()).SyncOnStartupAsync();
            Assert.False(result.Configured);
            Assert.True(result.Success);
            Assert.Empty(handler.Requests);
        }
        finally { Directory.Delete(dir, true); }
    }

    private static StubHttpHandler SuccessfulHandler() => new(request =>
        request.RequestUri!.AbsolutePath.EndsWith("/discover/movie")
            ? StubHttpHandler.Json("""{"page":1,"results":[{"id":123}],"total_pages":1}""")
            : StubHttpHandler.Json(Details));

    private static AppDb NewDb(string database) => new(new DbContextOptionsBuilder<AppDb>().UseSqlite($"Data Source={database}").Options);
    private static async Task CreateDatabase(string database)
    {
        await using var db = NewDb(database);
        await db.Database.EnsureCreatedAsync();
    }
}
