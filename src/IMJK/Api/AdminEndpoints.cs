using System.Security.Claims;
using IMDbClone.Data;
using Microsoft.EntityFrameworkCore;

namespace IMDbClone.Api;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/queue", async (AppDb db, ClaimsPrincipal principal) =>
        {
            if ((await ApiHelpers.CurrentUser(db, principal))?.IsAdmin != true) return Results.Forbid();
            return Results.Ok(new
            {
                movies = await db.Movies.Where(x => x.Status == "pending").Select(x => new { x.Id, x.Title, x.Year }).ToListAsync(),
                reviews = await db.Reviews.OrderByDescending(x => x.CreatedAt).Take(100).Select(x => new { x.Id, x.Username, x.Score, x.Comment, x.MovieId }).ToListAsync()
            });
        }).RequireAuthorization();

        app.MapPost("/api/admin/movies/{id:int}/approve", async (int id, AppDb db, ClaimsPrincipal principal) =>
        {
            if ((await ApiHelpers.CurrentUser(db, principal))?.IsAdmin != true) return Results.Forbid();
            var movie = await db.Movies.FindAsync(id);
            if (movie is null) return Results.NotFound();
            movie.Status = "approved";
            await db.SaveChangesAsync();
            return Results.Ok(new { success = true });
        }).RequireAuthorization();

        app.MapDelete("/api/admin/movies/{id:int}", async (int id, AppDb db, ClaimsPrincipal principal) =>
        {
            if ((await ApiHelpers.CurrentUser(db, principal))?.IsAdmin != true) return Results.Forbid();
            var movie = await db.Movies.FindAsync(id);
            if (movie is null) return Results.NotFound();
            movie.Status = "rejected";
            await db.SaveChangesAsync();
            return Results.Ok(new { success = true });
        }).RequireAuthorization();

        app.MapDelete("/api/admin/reviews/{id:int}", async (int id, AppDb db, ClaimsPrincipal principal) =>
        {
            if ((await ApiHelpers.CurrentUser(db, principal))?.IsAdmin != true) return Results.Forbid();
            var review = await db.Reviews.Include(x => x.Comments).Include(x => x.HelpfulVotes).FirstOrDefaultAsync(x => x.Id == id);
            if (review is null) return Results.NotFound();
            db.Comments.RemoveRange(review.Comments);
            db.HelpfulVotes.RemoveRange(review.HelpfulVotes);
            db.Reviews.Remove(review);
            await db.SaveChangesAsync();
            return Results.Ok(new { success = true });
        }).RequireAuthorization();

        return app;
    }
}
