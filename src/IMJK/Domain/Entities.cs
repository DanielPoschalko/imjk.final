using System.Text.Json;

namespace IMDbClone.Domain;

public class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string GenresJson { get; set; } = "[]";
    public bool IsAdmin { get; set; }
    public List<UserMovie> Library { get; set; } = [];
    public string[] FavoriteGenres
    {
        get => JsonSerializer.Deserialize<string[]>(GenresJson) ?? [];
        set => GenresJson = JsonSerializer.Serialize(value);
    }
}

public class Movie
{
    public int Id { get; set; }
    public int? TmdbId { get; set; }
    public string Title { get; set; } = "";
    public int Year { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public string ImageUrl { get; set; } = "";
    public string GenresJson { get; set; } = "[]";
    public string CastJson { get; set; } = "[]";
    public string Director { get; set; } = "";
    public string Plot { get; set; } = "";
    public string Status { get; set; } = "approved";
    public string Source { get; set; } = "local";
    public double? TmdbRating { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Review> Reviews { get; set; } = [];
    public string[] Genres
    {
        get => JsonSerializer.Deserialize<string[]>(GenresJson) ?? [];
        set => GenresJson = JsonSerializer.Serialize(value);
    }
    public string[] Cast
    {
        get => JsonSerializer.Deserialize<string[]>(CastJson) ?? [];
        set => CastJson = JsonSerializer.Serialize(value);
    }
    public double AverageRating => Reviews.Count == 0 ? 0 : Math.Round(Reviews.Average(x => (double)x.Score), 1);
    public double DisplayRating => Reviews.Count == 0 ? TmdbRating ?? 0 : AverageRating;
}

public class Review
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;
    public int? UserId { get; set; }
    public string Username { get; set; } = "";
    public int Score { get; set; }
    public string Comment { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ReviewComment> Comments { get; set; } = [];
    public List<ReviewHelpful> HelpfulVotes { get; set; } = [];
}

public class ReviewComment
{
    public int Id { get; set; }
    public int ReviewId { get; set; }
    public string Username { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ReviewHelpful { public int ReviewId { get; set; } public int UserId { get; set; } }
public class UserMovie { public int UserId { get; set; } public int MovieId { get; set; } public bool Watchlist { get; set; } public bool Seen { get; set; } }

public class MovieList
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Owner { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsPublic { get; set; } = true;
    public List<MovieListItem> Items { get; set; } = [];
    public List<MovieListContributor> Contributors { get; set; } = [];
}

public class MovieListContributor { public int ListId { get; set; } public int UserId { get; set; } }
public class MovieListItem { public int ListId { get; set; } public int MovieId { get; set; } }

public class AppSetting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
