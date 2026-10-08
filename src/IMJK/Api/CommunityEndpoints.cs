using System.Security.Claims;
using IMDbClone.Contracts;
using IMDbClone.Data;
using IMDbClone.Domain;
using Microsoft.EntityFrameworkCore;

namespace IMDbClone.Api;

public static class CommunityEndpoints
{
    public static IEndpointRouteBuilder MapCommunityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/reviews/{id:int}/comments", async (int id, CommentRequest request, AppDb db, ClaimsPrincipal principal) =>
        {
            var user = await ApiHelpers.CurrentUser(db, principal);
            if (user is null) return Results.Unauthorized();
            if (ApiHelpers.BadText(request.Text, 1000)) return ApiHelpers.Error("Kommentar muss 1 bis 1000 Zeichen haben.");
            if (!await db.Reviews.AnyAsync(x => x.Id == id && x.Movie.Status == "approved")) return Results.NotFound();
            db.Comments.Add(new ReviewComment { ReviewId = id, Username = user.Username, Text = request.Text.Trim() });
            await db.SaveChangesAsync();
            return Results.Ok(new { success = true });
        }).RequireAuthorization();

        app.MapPost("/api/reviews/{id:int}/helpful", async (int id, AppDb db, ClaimsPrincipal principal) =>
        {
            var user = await ApiHelpers.CurrentUser(db, principal);
            if (user is null) return Results.Unauthorized();
            if (!await db.Reviews.AnyAsync(x => x.Id == id && x.Movie.Status == "approved")) return Results.NotFound();
            var vote = await db.HelpfulVotes.FindAsync(id, user.Id);
            if (vote is null) db.HelpfulVotes.Add(new ReviewHelpful { ReviewId = id, UserId = user.Id });
            else db.HelpfulVotes.Remove(vote);
            await db.SaveChangesAsync();
            return Results.Ok(new { success = true });
        }).RequireAuthorization();

        app.MapGet("/api/library", async (AppDb db, ClaimsPrincipal principal) =>
        {
            var id = ApiHelpers.CurrentUserId(principal)!.Value;
            return Results.Ok(await db.UserMovies.Where(x => x.UserId == id)
                .Select(x => new { x.MovieId, x.Watchlist, x.Seen }).ToListAsync());
        }).RequireAuthorization();

        app.MapPut("/api/library/{kind}", async (string kind, LibraryRequest request, AppDb db, ClaimsPrincipal principal) =>
        {
            var userId = ApiHelpers.CurrentUserId(principal)!.Value;
            if (kind is not ("watchlist" or "seen")) return Results.NotFound();
            if (!await db.Movies.AnyAsync(x => x.Id == request.MovieId && x.Status == "approved")) return Results.NotFound();
            var item = await db.UserMovies.FindAsync(userId, request.MovieId);
            if (item is null)
            {
                item = new UserMovie { UserId = userId, MovieId = request.MovieId };
                db.UserMovies.Add(item);
            }
            if (kind == "watchlist") item.Watchlist = request.Value;
            else item.Seen = request.Value;
            if (!item.Watchlist && !item.Seen) db.UserMovies.Remove(item);
            await db.SaveChangesAsync();
            return Results.Ok(new { success = true });
        }).RequireAuthorization();

        app.MapGet("/api/recommendations", async (AppDb db, ClaimsPrincipal principal) =>
        {
            var user = await ApiHelpers.CurrentUser(db, principal);
            if (user is null) return Results.Unauthorized();
            var seen = await db.UserMovies.Where(x => x.UserId == user.Id && x.Seen).Select(x => x.MovieId).ToListAsync();
            var movies = await db.Movies.Where(x => x.Status == "approved").Include(x => x.Reviews).ToListAsync();
            var likedGenres = movies.Where(x => x.Reviews.Any(r => r.UserId == user.Id && r.Score >= 7))
                .SelectMany(x => x.Genres).Concat(user.FavoriteGenres).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var result = movies.Where(x => !seen.Contains(x.Id))
                .Select(x => new { Movie = x, Matches = x.Genres.Where(likedGenres.Contains).ToArray(), Score = x.DisplayRating + x.Genres.Count(likedGenres.Contains) * 2 })
                .OrderByDescending(x => x.Score).Take(8)
                .Select(x => new { movie = ApiHelpers.PublicMovie(x.Movie), reason = x.Matches.Length > 0 ? $"Weil du {string.Join(", ", x.Matches)} magst" : "Beliebt in der Community" });
            return Results.Ok(result);
        }).RequireAuthorization();

        app.MapGet("/api/lists", async (AppDb db, ClaimsPrincipal principal) =>
        {
            var userId = ApiHelpers.CurrentUserId(principal);
            var lists = await db.Lists.Include(x => x.Items).Include(x => x.Contributors)
                .Where(x => x.IsPublic || userId.HasValue && (x.UserId == userId || x.Contributors.Any(c => c.UserId == userId)))
                .OrderByDescending(x => x.Id).ToListAsync();
            return Results.Ok(lists.Select(x => new { x.Id, x.UserId, x.Name, x.Owner, x.IsPublic, ContributorIds = x.Contributors.Select(c => c.UserId), MovieIds = x.Items.Select(i => i.MovieId) }));
        });

        app.MapPost("/api/lists", async (ListRequest request, AppDb db, ClaimsPrincipal principal) =>
        {
            var user = await ApiHelpers.CurrentUser(db, principal);
            if (user is null) return Results.Unauthorized();
            if (ApiHelpers.BadText(request.Name, 80)) return ApiHelpers.Error("Listenname muss 1 bis 80 Zeichen haben.");
            var list = new MovieList { UserId = user.Id, Owner = user.Username, Name = request.Name.Trim(), IsPublic = request.IsPublic };
            db.Lists.Add(list);
            await db.SaveChangesAsync();
            return Results.Ok(new { list.Id });
        }).RequireAuthorization();

        app.MapPost("/api/lists/{id:int}/movies", async (int id, ListItemRequest request, AppDb db, ClaimsPrincipal principal) =>
        {
            var list = await db.Lists.FindAsync(id);
            if (list is null) return Results.NotFound();
            var userId = ApiHelpers.CurrentUserId(principal);
            if (list.UserId != userId && !await db.ListContributors.AnyAsync(x => x.ListId == id && x.UserId == userId)) return Results.Forbid();
            if (!await db.Movies.AnyAsync(x => x.Id == request.MovieId && x.Status == "approved")) return Results.NotFound();
            if (!await db.ListItems.AnyAsync(x => x.ListId == id && x.MovieId == request.MovieId))
            {
                db.ListItems.Add(new MovieListItem { ListId = id, MovieId = request.MovieId });
                await db.SaveChangesAsync();
            }
            return Results.Ok(new { success = true });
        }).RequireAuthorization();

        app.MapPost("/api/lists/{id:int}/invite", async (int id, InviteRequest request, AppDb db, ClaimsPrincipal principal) =>
        {
            var list = await db.Lists.FindAsync(id);
            if (list is null) return Results.NotFound();
            if (list.UserId != ApiHelpers.CurrentUserId(principal)) return Results.Forbid();
            var name = (request.Username ?? "").Trim().ToLower();
            var invited = await db.Users.FirstOrDefaultAsync(user => user.Username.ToLower() == name);
            if (invited is null) return ApiHelpers.Error("Nutzer nicht gefunden.", 404);
            if (invited.Id == list.UserId) return ApiHelpers.Error("Du bist bereits Eigentümer der Liste.");
            if (!await db.ListContributors.AnyAsync(x => x.ListId == id && x.UserId == invited.Id))
            {
                db.ListContributors.Add(new MovieListContributor { ListId = id, UserId = invited.Id });
                await db.SaveChangesAsync();
            }
            return Results.Ok(new { success = true });
        }).RequireAuthorization();

        return app;
    }
}
