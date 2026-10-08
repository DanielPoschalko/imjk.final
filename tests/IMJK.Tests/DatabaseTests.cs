using IMDbClone.Data;
using IMDbClone.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MovieApp.Tests;

public class DatabaseTests
{
    [Fact]
    public async Task Startup_ImportsLegacyUsersAsHashes_AndSkipsWeakDefaultAdmin()
    {
        var dir = TestSupport.TempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "users.json"), """[{"Username":"alice","Password":"StrongPassword123","FavoriteGenres":["Drama"]},{"Username":"admin","Password":"1234","FavoriteGenres":[]}]""");
            await using var provider = TestSupport.DatabaseServices(dir);
            await using var scope = provider.CreateAsyncScope();
            await DatabaseInitializer.Initialize(scope.ServiceProvider, dir, Directory.GetCurrentDirectory(), new ConfigurationBuilder().Build());
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var user = await db.Users.SingleAsync();
            Assert.Equal("alice", user.Username);
            Assert.NotEqual("StrongPassword123", user.PasswordHash);
            Assert.Equal("Drama", Assert.Single(user.FavoriteGenres));
            Assert.Equal(PasswordVerificationResult.Success,
                scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>().VerifyHashedPassword(user, user.PasswordHash, "StrongPassword123"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Database_PersistsAllUserDataAfterRestart()
    {
        var dir = TestSupport.TempDir();
        try
        {
            var options = new DbContextOptionsBuilder<AppDb>().UseSqlite($"Data Source={Path.Combine(dir, "filmportal.db")}").Options;
            await using (var db = new AppDb(options))
            {
                await db.Database.EnsureCreatedAsync();
                var user = new AppUser { Username = "bob", PasswordHash = "hashed" };
                var movie = new Movie { Title = "Test", Year = 2025, Genres = ["Drama"] };
                db.Users.Add(user); db.Movies.Add(movie); await db.SaveChangesAsync();
                db.UserMovies.Add(new UserMovie { UserId = user.Id, MovieId = movie.Id, Watchlist = true, Seen = true });
                db.Reviews.Add(new Review { MovieId = movie.Id, UserId = user.Id, Username = user.Username, Score = 9, Comment = "Gut" });
                var list = new MovieList { UserId = user.Id, Owner = user.Username, Name = "Favoriten" };
                list.Items.Add(new MovieListItem { MovieId = movie.Id });
                db.Lists.Add(list);
                await db.SaveChangesAsync();
            }
            await using (var db = new AppDb(options))
            {
                var library = await db.UserMovies.SingleAsync();
                Assert.True(library.Watchlist);
                Assert.True(library.Seen);
                Assert.Equal(9, (await db.Reviews.SingleAsync()).Score);
                Assert.Equal("Favoriten", (await db.Lists.Include(x => x.Items).SingleAsync()).Name);
                Assert.Single((await db.Lists.Include(x => x.Items).SingleAsync()).Items);
            }
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Initializer_UpgradesExistingDatabaseWithoutLosingMovies()
    {
        var dir = TestSupport.TempDir();
        try
        {
            var database = Path.Combine(dir, "filmportal.db");
            var options = new DbContextOptionsBuilder<AppDb>().UseSqlite($"Data Source={database}").Options;
            await using (var db = new AppDb(options))
            {
                await db.Database.EnsureCreatedAsync();
                db.Movies.Add(new Movie { Title = "Alt", Year = 2000 });
                await db.SaveChangesAsync();
                await db.Database.ExecuteSqlRawAsync("DROP INDEX IX_Movies_TmdbId");
                await db.Database.ExecuteSqlRawAsync("DROP TABLE Settings");
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Movies DROP COLUMN TmdbId");
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Movies DROP COLUMN ReleaseDate");
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Movies DROP COLUMN Source");
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Movies DROP COLUMN TmdbRating");
            }
            await using var provider = TestSupport.DatabaseServices(dir);
            await using var scope = provider.CreateAsyncScope();
            await DatabaseInitializer.Initialize(scope.ServiceProvider, dir, Directory.GetCurrentDirectory(), new ConfigurationBuilder().Build());
            var upgradedDb = scope.ServiceProvider.GetRequiredService<AppDb>();
            Assert.Equal("Alt", (await upgradedDb.Movies.SingleAsync()).Title);
            Assert.True(await upgradedDb.Database.CanConnectAsync());
            Assert.Empty(await upgradedDb.Settings.ToListAsync());
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Database_AllowsRemakesWithTheSameTitleInDifferentYears()
    {
        var dir = TestSupport.TempDir();
        try
        {
            await using var provider = TestSupport.DatabaseServices(dir);
            await using var scope = provider.CreateAsyncScope();
            await DatabaseInitializer.Initialize(scope.ServiceProvider, dir, Directory.GetCurrentDirectory(), new ConfigurationBuilder().Build());
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            db.Movies.AddRange(
                new Movie { Title = "Der gleiche Titel", Year = 1999 },
                new Movie { Title = "Der gleiche Titel", Year = 2026 });
            await db.SaveChangesAsync();
            Assert.Equal(2, await db.Movies.CountAsync(x => x.Title == "Der gleiche Titel"));
        }
        finally { Directory.Delete(dir, true); }
    }
}
