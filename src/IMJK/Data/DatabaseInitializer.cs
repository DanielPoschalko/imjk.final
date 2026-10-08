using System.Text.Json;
using IMDbClone.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IMDbClone.Data;

public static class DatabaseInitializer
{
    private sealed class LegacyUser { public string Username { get; set; } = ""; public string Password { get; set; } = ""; public string[] FavoriteGenres { get; set; } = []; }
    private sealed class LegacyMovie { public string Title { get; set; } = ""; public int Year { get; set; } public string ImageUrl { get; set; } = ""; public string[] Genres { get; set; } = []; public string[] Cast { get; set; } = []; public string Director { get; set; } = ""; public string Plot { get; set; } = ""; public LegacyReview[] Reviews { get; set; } = []; }
    private sealed class LegacyReview { public string Username { get; set; } = ""; public int Score { get; set; } public string Comment { get; set; } = ""; }

    public static async Task Initialize(IServiceProvider services, string dataDir, string root, IConfiguration configuration)
    {
        var db = services.GetRequiredService<AppDb>();
        var hasher = services.GetRequiredService<IPasswordHasher<AppUser>>();
        await db.Database.EnsureCreatedAsync();
        await UpgradeExistingSchema(db);

        if (!await db.Movies.AnyAsync())
        {
            var path = File.Exists(Path.Combine(dataDir, "movies.json"))
                ? Path.Combine(dataDir, "movies.json")
                : Path.Combine(root, "seed", "movies.json");
            if (File.Exists(path))
            {
                foreach (var old in JsonSerializer.Deserialize<LegacyMovie[]>(await File.ReadAllTextAsync(path)) ?? [])
                {
                    var movie = new Movie { Title = old.Title, Year = old.Year, ImageUrl = old.ImageUrl, Genres = old.Genres, Cast = old.Cast, Director = old.Director, Plot = old.Plot };
                    db.Movies.Add(movie);
                    foreach (var review in old.Reviews)
                        movie.Reviews.Add(new Review { Username = review.Username, Score = review.Score, Comment = review.Comment });
                }
                await db.SaveChangesAsync();
            }
        }

        if (!await db.Users.AnyAsync())
        {
            var path = Path.Combine(dataDir, "users.json");
            if (File.Exists(path))
            {
                foreach (var old in JsonSerializer.Deserialize<LegacyUser[]>(await File.ReadAllTextAsync(path)) ?? [])
                {
                    if (string.IsNullOrWhiteSpace(old.Username) || string.IsNullOrWhiteSpace(old.Password) || old.Username.Equals("admin", StringComparison.OrdinalIgnoreCase) && old.Password == "1234") continue;
                    var user = new AppUser { Username = old.Username, FavoriteGenres = old.FavoriteGenres, IsAdmin = old.Username.Equals("admin", StringComparison.OrdinalIgnoreCase) };
                    user.PasswordHash = hasher.HashPassword(user, old.Password);
                    db.Users.Add(user);
                }
                await db.SaveChangesAsync();
            }
        }

        var adminName = configuration["ADMIN_USERNAME"];
        var adminPassword = configuration["ADMIN_PASSWORD"];
        if (!string.IsNullOrWhiteSpace(adminName) && !string.IsNullOrWhiteSpace(adminPassword) && adminPassword.Length >= 12 && !await db.Users.AnyAsync(x => x.Username == adminName))
        {
            var user = new AppUser { Username = adminName, IsAdmin = true };
            user.PasswordHash = hasher.HashPassword(user, adminPassword);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
    }

    private static async Task UpgradeExistingSchema(AppDb db)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            if (command.Connection!.State != System.Data.ConnectionState.Open) await command.Connection.OpenAsync();
            command.CommandText = "PRAGMA table_info('Movies')";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
        }

        if (!columns.Contains("TmdbId")) await db.Database.ExecuteSqlRawAsync("ALTER TABLE Movies ADD COLUMN TmdbId INTEGER NULL");
        if (!columns.Contains("ReleaseDate")) await db.Database.ExecuteSqlRawAsync("ALTER TABLE Movies ADD COLUMN ReleaseDate TEXT NULL");
        if (!columns.Contains("Source")) await db.Database.ExecuteSqlRawAsync("ALTER TABLE Movies ADD COLUMN Source TEXT NOT NULL DEFAULT 'local'");
        if (!columns.Contains("TmdbRating")) await db.Database.ExecuteSqlRawAsync("ALTER TABLE Movies ADD COLUMN TmdbRating REAL NULL");
        await db.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS IX_Movies_Title");
        await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS IX_Movies_Title_Year ON Movies (Title, Year)");
        await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS IX_Movies_TmdbId ON Movies (TmdbId) WHERE TmdbId IS NOT NULL");
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS Settings (Key TEXT NOT NULL PRIMARY KEY, Value TEXT NOT NULL)");
    }
}
