using System.Net;
using IMDbClone.Data;
using IMDbClone.Domain;
using IMDbClone.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MovieApp.Tests;

internal static class TestSupport
{
    public static string TempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "filmportal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static ServiceProvider DatabaseServices(string path) => new ServiceCollection()
        .AddDbContext<AppDb>(options => options.UseSqlite($"Data Source={Path.Combine(path, "filmportal.db")}"))
        .AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>()
        .BuildServiceProvider();

    public static TmdbImportService TmdbService(
        string databasePath,
        HttpMessageHandler handler,
        DateTimeOffset now,
        TmdbOptions? options = null)
    {
        var services = new ServiceCollection()
            .AddDbContext<AppDb>(builder => builder.UseSqlite($"Data Source={databasePath}"))
            .BuildServiceProvider();
        return new TmdbImportService(
            new FakeHttpClientFactory(handler),
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options ?? new TmdbOptions { ReadAccessToken = "test-token" }),
            new FixedTimeProvider(now),
            NullLogger<TmdbImportService>.Instance);
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.themoviedb.org/3/") };
}

internal sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        return Task.FromResult(response(request));
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
}
