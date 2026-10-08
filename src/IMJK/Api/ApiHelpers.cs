using System.Security.Claims;
using IMDbClone.Data;
using IMDbClone.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace IMDbClone.Api;

public static class ApiHelpers
{
    public static object PublicUser(AppUser user) => new { user.Id, user.Username, user.FavoriteGenres, user.IsAdmin };

    public static object PublicMovie(Movie movie, bool includeReviews = false) => new
    {
        movie.Id,
        movie.TmdbId,
        movie.Title,
        movie.Year,
        movie.ReleaseDate,
        movie.ImageUrl,
        movie.Genres,
        movie.Cast,
        movie.Director,
        movie.Plot,
        movie.Source,
        movie.TmdbRating,
        movie.AverageRating,
        movie.DisplayRating,
        ReviewCount = movie.Reviews.Count,
        movie.CreatedAt,
        Reviews = includeReviews
            ? movie.Reviews.OrderByDescending(r => r.CreatedAt).Select(r => new
            {
                r.Id, r.Username, r.Score, r.Comment, r.CreatedAt,
                HelpfulCount = r.HelpfulVotes.Count,
                Comments = r.Comments.OrderBy(c => c.CreatedAt).Select(c => new { c.Id, c.Username, c.Text, c.CreatedAt })
            }).ToArray()
            : null
    };

    public static int? CurrentUserId(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static async Task<AppUser?> CurrentUser(AppDb db, ClaimsPrincipal principal) =>
        CurrentUserId(principal) is int id ? await db.Users.FindAsync(id) : null;

    public static Task SignIn(HttpContext context, AppUser user) => context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username)
        ], CookieAuthenticationDefaults.AuthenticationScheme)));

    public static string[] CleanValues(string[]? values) => (values ?? [])
        .Select(x => x.Trim()).Where(x => x.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToArray();

    public static bool BadText(string? value, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max;
    public static IResult Error(string message, int status = 400) => Results.Json(new { error = message }, statusCode: status);

    public static Task<Movie?> ApprovedMovie(AppDb db, int id) => db.Movies
        .Include(m => m.Reviews).ThenInclude(r => r.Comments)
        .Include(m => m.Reviews).ThenInclude(r => r.HelpfulVotes)
        .FirstOrDefaultAsync(m => m.Id == id && m.Status == "approved");
}
