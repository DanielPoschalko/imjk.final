using System.Security.Claims;
using IMDbClone.Contracts;
using IMDbClone.Data;
using IMDbClone.Domain;
using IMDbClone.Services;
using Microsoft.EntityFrameworkCore;

namespace IMDbClone.Api;

public static class MovieEndpoints
{
    public static IEndpointRouteBuilder MapMovieEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/movies", async (AppDb db, string? genre, string? actor, string? query, int? fromYear, int? toYear, double? minRating, string? sort) =>
        {
            var movies = await db.Movies.Where(m => m.Status == "approved").Include(m => m.Reviews).ToListAsync();
            var filtered = movies.Where(m =>
                (string.IsNullOrWhiteSpace(genre) || m.Genres.Any(g => g.Contains(genre, StringComparison.OrdinalIgnoreCase))) &&
                (string.IsNullOrWhiteSpace(actor) || m.Cast.Any(a => a.Contains(actor, StringComparison.OrdinalIgnoreCase))) &&
                (string.IsNullOrWhiteSpace(query) || new[] { m.Title, m.Director, m.Plot }.Any(s => s.Contains(query, StringComparison.OrdinalIgnoreCase)) || m.Genres.Any(g => g.Contains(query, StringComparison.OrdinalIgnoreCase)) || m.Cast.Any(a => a.Contains(query, StringComparison.OrdinalIgnoreCase))) &&
                (!fromYear.HasValue || m.Year >= fromYear) && (!toYear.HasValue || m.Year <= toYear) &&
                (!minRating.HasValue || m.DisplayRating >= minRating));
            filtered = sort switch
            {
                "new" => filtered.OrderByDescending(m => m.ReleaseDate).ThenByDescending(m => m.CreatedAt),
                "year" => filtered.OrderByDescending(m => m.Year),
                "popular" => filtered.OrderByDescending(m => m.Reviews.Count),
                _ => filtered.OrderByDescending(m => m.DisplayRating)
            };
            return Results.Ok(filtered.Select(m => ApiHelpers.PublicMovie(m)));
        });

        app.MapGet("/api/movies/{id:int}", async (int id, AppDb db) =>
            await ApiHelpers.ApprovedMovie(db, id) is Movie movie
                ? Results.Ok(ApiHelpers.PublicMovie(movie, true))
                : Results.NotFound());

        app.MapGet("/api/tmdb/status", async (ITmdbImportService importer, CancellationToken cancellationToken) =>
            Results.Ok(await importer.GetStatusAsync(cancellationToken)));

        app.MapPost("/api/movies", async (MovieRequest request, AppDb db, ClaimsPrincipal principal) =>
        {
            var user = await ApiHelpers.CurrentUser(db, principal);
            if (user is null) return Results.Unauthorized();
            if (ApiHelpers.BadText(request.Title, 160) || request.Year < 1888 || request.Year > DateTime.UtcNow.Year + 3 || ApiHelpers.BadText(request.Description, 4000))
                return ApiHelpers.Error("Bitte gültigen Titel, Jahr und Beschreibung eingeben.");
            if (await db.Movies.AnyAsync(m => m.Title.ToLower() == request.Title.Trim().ToLower() && m.Year == request.Year))
                return ApiHelpers.Error("Film bereits vorhanden.");
            var movie = new Movie
            {
                Title = request.Title.Trim(), Year = request.Year, ImageUrl = request.ImageUrl?.Trim() ?? "",
                Genres = ApiHelpers.CleanValues(request.Genres), Cast = ApiHelpers.CleanValues(request.Cast),
                Director = request.Director?.Trim() ?? "", Plot = request.Description!.Trim(),
                Status = user.IsAdmin ? "approved" : "pending"
            };
            db.Movies.Add(movie);
            await db.SaveChangesAsync();
            return Results.Ok(new { movie = ApiHelpers.PublicMovie(movie), pending = !user.IsAdmin });
        }).RequireAuthorization();

        app.MapPost("/api/rating", async (RatingRequest request, AppDb db, ClaimsPrincipal principal) =>
        {
            var user = await ApiHelpers.CurrentUser(db, principal);
            if (user is null) return Results.Unauthorized();
            var movie = await ApiHelpers.ApprovedMovie(db, request.MovieId);
            if (movie is null) return Results.NotFound();
            if (request.Score is < 1 or > 10 || (request.ReviewText?.Length ?? 0) > 2000)
                return ApiHelpers.Error("Bewertung oder Text ungültig.");
            var review = movie.Reviews.FirstOrDefault(x => x.UserId == user.Id);
            if (review is null)
            {
                review = new Review { MovieId = movie.Id, UserId = user.Id, Username = user.Username };
                db.Reviews.Add(review);
            }
            review.Score = request.Score;
            review.Comment = request.ReviewText?.Trim() ?? "";
            review.CreatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { success = true });
        }).RequireAuthorization();

        return app;
    }
}
