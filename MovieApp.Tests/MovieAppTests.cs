using IMDbClone;
using Xunit;
using MovieAppType = IMDbClone.MovieApp;

namespace MovieApp.Tests;

public class MovieAppTests
{
    private static MovieAppType CreateFreshApp()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), $"imdbclone-{Guid.NewGuid():N}");
        return new MovieAppType(dataDirectory);
    }

    [Fact]
    public void RegisterUser_AddsUserToRepository()
    {
        var app = CreateFreshApp();

        var user = app.RegisterUser("alice", "pass123");

        Assert.Equal("alice", user.Username);
        Assert.Contains(app.Users, u => u.Username == "alice");
    }

    [Fact]
    public void RateMovie_UpdatesAverageScore()
    {
        var app = CreateFreshApp();
        var user = app.RegisterUser("bob", "secret");
        var movie = app.Movies.First();

        app.AddRating(movie.Title, user.Username, 8);
        app.AddRating(movie.Title, "admin", 10);

        var updated = app.Movies.First(m => m.Title == movie.Title);
        Assert.True(updated.AverageRating > 8.5m);
    }

    [Fact]
    public void FilterMovies_ByGenre_ReturnsMatchingMovies()
    {
        var app = CreateFreshApp();

        var result = app.FilterMoviesByGenre("Sci-Fi");

        Assert.NotEmpty(result);
        Assert.All(result, movie => Assert.Contains("Sci-Fi", movie.Genres));
    }

    [Fact]
    public void SeedMovies_UseOfficialIMDbPosters()
    {
        var app = CreateFreshApp();

        Assert.NotEmpty(app.Movies);
        Assert.All(app.Movies, movie =>
        {
            Assert.StartsWith("https://m.media-amazon.com/", movie.ImageUrl);
            Assert.DoesNotContain("images.unsplash.com", movie.ImageUrl);
        });
    }

    [Fact]
    public void PersistedState_IsLoadedAfterRestart()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), $"imdbclone-{Guid.NewGuid():N}");

        var firstRun = new MovieAppType(dataDirectory);
        var user = firstRun.RegisterUser("persisted-user", "secret123");
        var movie = firstRun.AddMovie("Persisted Movie", 2025, "https://example.com/poster.jpg", new[] { "Drama" }, new[] { "Actor One" }, "Director One", "Beschreibung");
        firstRun.AddRating(movie.Title, user.Username, 9);

        var secondRun = new MovieAppType(dataDirectory);

        Assert.Contains(secondRun.Users, u => u.Username == "persisted-user");
        Assert.Contains(secondRun.Movies, m => m.Title == "Persisted Movie");
        var persistedMovie = secondRun.Movies.Single(m => m.Title == "Persisted Movie");
        Assert.Contains(persistedMovie.Reviews, r => r.Username == "persisted-user" && r.Score == 9);
    }

    [Fact]
    public void DefaultDataDirectory_UsesCurrentWorkingDirectory()
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"imdbclone-default-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            Directory.SetCurrentDirectory(tempDirectory);

            var app = new MovieAppType();
            app.RegisterUser("cwd-user", "pass123");

            Assert.True(File.Exists(Path.Combine(tempDirectory, "app-data", "users.json")));
            Assert.True(File.Exists(Path.Combine(tempDirectory, "app-data", "movies.json")));

            var reloaded = new MovieAppType();
            Assert.Contains(reloaded.Users, u => u.Username == "cwd-user");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }
    }
}
