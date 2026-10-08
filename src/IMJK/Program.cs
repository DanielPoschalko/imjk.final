using IMDbClone.Api;
using IMDbClone.Data;
using IMDbClone.Domain;
using IMDbClone.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IMDbClone;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddUserSecrets<Program>(optional: true);
        var dataDir = builder.Configuration["DATA_DIR"] ?? Path.Combine(builder.Environment.ContentRootPath, "app-data");
        Directory.CreateDirectory(dataDir);

        builder.Services.AddDbContext<AppDb>(options => options.UseSqlite(
            $"Data Source={Path.Combine(dataDir, "filmportal.db")}",
            sqlite => sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.Configure<TmdbOptions>(options =>
        {
            options.ReadAccessToken = builder.Configuration["TMDB_READ_ACCESS_TOKEN"] ?? "";
            options.ApiKey = builder.Configuration["TMDB_API_KEY"] ?? "";
            options.Language = builder.Configuration["TMDB_LANGUAGE"] ?? "de-DE";
            options.Region = builder.Configuration["TMDB_REGION"] ?? "DE";
            options.Disabled = builder.Configuration.GetValue<bool>("TMDB_SYNC_DISABLED");
            if (int.TryParse(builder.Configuration["TMDB_INITIAL_LOOKBACK_DAYS"], out var days)) options.InitialLookbackDays = days;
        });
        builder.Services.AddHttpClient("tmdb", client =>
        {
            client.BaseAddress = new Uri("https://api.themoviedb.org/3/");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        builder.Services.AddSingleton<ITmdbImportService, TmdbImportService>();

        var app = builder.Build();
        await using (var scope = app.Services.CreateAsyncScope())
            await DatabaseInitializer.Initialize(scope.ServiceProvider, dataDir, builder.Environment.ContentRootPath, builder.Configuration);

        var syncResult = await app.Services.GetRequiredService<ITmdbImportService>().SyncOnStartupAsync();
        if (!syncResult.Success) app.Logger.LogWarning("{Message}", syncResult.Message);
        else app.Logger.LogInformation("{Message}", syncResult.Message);

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api") &&
                !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) &&
                context.Request.Headers["X-Requested-With"] != "XMLHttpRequest")
            {
                context.Response.StatusCode = 403;
                return;
            }
            await next();
        });

        app.MapAuthEndpoints();
        app.MapMovieEndpoints();
        app.MapCommunityEndpoints();
        app.MapAdminEndpoints();
        await app.RunAsync();
    }
}
