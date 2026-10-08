using System.Security.Claims;
using IMDbClone.Contracts;
using IMDbClone.Data;
using IMDbClone.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IMDbClone.Api;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", async (AppDb db, ClaimsPrincipal principal) =>
        {
            var user = await ApiHelpers.CurrentUser(db, principal);
            return Results.Ok(new { user = user is null ? null : ApiHelpers.PublicUser(user) });
        });

        app.MapPost("/api/register", async (RegisterRequest request, AppDb db, IPasswordHasher<AppUser> hasher, HttpContext context) =>
        {
            var name = request.Username?.Trim() ?? "";
            if (ApiHelpers.BadText(name, 40) || ApiHelpers.BadText(request.Password, 200) || request.Password.Length < 8)
                return ApiHelpers.Error("Benutzername und Passwort mit mindestens 8 Zeichen eingeben.");
            if (await db.Users.AnyAsync(x => x.Username.ToLower() == name.ToLower()))
                return ApiHelpers.Error("Benutzername existiert bereits.");
            var user = new AppUser { Username = name, FavoriteGenres = ApiHelpers.CleanValues(request.FavoriteGenres) };
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            await ApiHelpers.SignIn(context, user);
            return Results.Ok(new { user = ApiHelpers.PublicUser(user) });
        });

        app.MapPost("/api/login", async (LoginRequest request, AppDb db, IPasswordHasher<AppUser> hasher, HttpContext context) =>
        {
            var name = (request.Username ?? "").Trim().ToLower();
            var user = await db.Users.FirstOrDefaultAsync(x => x.Username.ToLower() == name);
            if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password ?? "") == PasswordVerificationResult.Failed)
                return ApiHelpers.Error("Login fehlgeschlagen.", 401);
            await ApiHelpers.SignIn(context, user);
            return Results.Ok(new { user = ApiHelpers.PublicUser(user) });
        });

        app.MapPost("/api/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync();
            return Results.Ok(new { success = true });
        }).RequireAuthorization();

        app.MapPut("/api/profile", async (ProfileRequest request, AppDb db, ClaimsPrincipal principal, HttpContext context) =>
        {
            var user = await ApiHelpers.CurrentUser(db, principal);
            if (user is null) return Results.Unauthorized();
            var name = request.NewUsername?.Trim();
            if (!string.IsNullOrEmpty(name))
            {
                if (name.Length > 40 || await db.Users.AnyAsync(x => x.Id != user.Id && x.Username.ToLower() == name.ToLower()))
                    return ApiHelpers.Error("Benutzername ungültig oder vergeben.");
                user.Username = name;
                foreach (var review in await db.Reviews.Where(x => x.UserId == user.Id).ToListAsync()) review.Username = name;
                foreach (var list in await db.Lists.Where(x => x.UserId == user.Id).ToListAsync()) list.Owner = name;
            }
            user.FavoriteGenres = ApiHelpers.CleanValues(request.FavoriteGenres);
            await db.SaveChangesAsync();
            await ApiHelpers.SignIn(context, user);
            return Results.Ok(new { user = ApiHelpers.PublicUser(user) });
        }).RequireAuthorization();

        return app;
    }
}
