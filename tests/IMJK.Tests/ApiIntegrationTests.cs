using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IMDbClone;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MovieApp.Tests;

public class ApiIntegrationTests
{
    [Fact]
    public async Task CompleteUserAndAdminFlow_PersistsAcrossApplicationRestart()
    {
        var dataDir = TestSupport.TempDir();
        try
        {
            int movieId;
            int reviewId;
            int listId;
            await using (var factory = new FilmPortalFactory(dataDir))
            {
                using var anonymous = factory.CreateClient(ClientOptions());
                var html = await anonymous.GetStringAsync("/");
                Assert.Contains("id=\"loginBtn\"", html);
                Assert.Contains("data-view=\"watchlist\"", html);
                Assert.Contains("id=\"createListBtn\"", html);
                Assert.Contains("id=\"tmdbSyncStatus\"", html);
                Assert.Contains("Dieses Produkt verwendet die TMDB-API", html);
                Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/library")).StatusCode);

                using var user = factory.CreateClient(ClientOptions());
                await AssertOk(Post(user, "/api/register", new { username = "tester", password = "StrongPassword123", favoriteGenres = new[] { "Drama" } }));
                var movies = await GetArray(user, "/api/movies?genre=Drama&sort=year");
                Assert.NotEmpty(movies);
                movieId = movies[0].GetProperty("id").GetInt32();
                await AssertOk(Put(user, "/api/library/watchlist", new { movieId, value = true }));
                await AssertOk(Put(user, "/api/library/seen", new { movieId, value = true }));
                await AssertOk(Post(user, "/api/rating", new { movieId, score = 9, reviewText = "Ausführlicher Test" }));
                var detail = await GetObject(user, $"/api/movies/{movieId}");
                reviewId = detail.GetProperty("reviews")[0].GetProperty("id").GetInt32();
                await AssertOk(Post(user, $"/api/reviews/{reviewId}/comments", new { text = "Stimme zu" }));
                await AssertOk(Post(user, $"/api/reviews/{reviewId}/helpful", new { }));
                var listResponse = await ReadJson(await Post(user, "/api/lists", new { name = "Favoriten", isPublic = true }));
                listId = listResponse.GetProperty("id").GetInt32();
                await AssertOk(Post(user, $"/api/lists/{listId}/movies", new { movieId }));
                Assert.NotEmpty(await GetArray(user, "/api/recommendations"));
                await AssertOk(Put(user, "/api/profile", new { newUsername = "tester-neu", favoriteGenres = new[] { "Drama", "Thriller" } }));

                using var collaborator = factory.CreateClient(ClientOptions());
                await AssertOk(Post(collaborator, "/api/register", new { username = "partner", password = "StrongPassword456", favoriteGenres = Array.Empty<string>() }));
                await AssertOk(Post(user, $"/api/lists/{listId}/invite", new { username = "partner" }));
                await AssertOk(Post(collaborator, $"/api/lists/{listId}/movies", new { movieId = movies[1].GetProperty("id").GetInt32() }));

                using var admin = factory.CreateClient(ClientOptions());
                await AssertOk(Post(admin, "/api/login", new { username = "siteadmin", password = "TestAdminPassword123" }));
                var proposed = await ReadJson(await Post(user, "/api/movies", new { title = "Integration Test Film", year = 2026, imageUrl = "", genres = new[] { "Drama" }, cast = Array.Empty<string>(), director = "Test", description = "Testfilm" }));
                Assert.True(proposed.GetProperty("pending").GetBoolean());
                var queue = await GetObject(admin, "/api/admin/queue");
                var pendingId = queue.GetProperty("movies")[0].GetProperty("id").GetInt32();
                await AssertOk(Post(admin, $"/api/admin/movies/{pendingId}/approve", new { }));
                Assert.Contains((await GetArray(user, "/api/movies")).Select(x => x.GetProperty("title").GetString()), x => x == "Integration Test Film");
                await AssertOk(Post(user, "/api/logout", new { }));
                Assert.Equal(HttpStatusCode.Unauthorized, (await user.GetAsync("/api/library")).StatusCode);
            }

            await using (var restartedFactory = new FilmPortalFactory(dataDir))
            {
                using var user = restartedFactory.CreateClient(ClientOptions());
                await AssertOk(Post(user, "/api/login", new { username = "tester-neu", password = "StrongPassword123" }));
                var library = await GetArray(user, "/api/library");
                Assert.Contains(library, x => x.GetProperty("movieId").GetInt32() == movieId && x.GetProperty("watchlist").GetBoolean() && x.GetProperty("seen").GetBoolean());
                var detail = await GetObject(user, $"/api/movies/{movieId}");
                Assert.Contains(detail.GetProperty("reviews").EnumerateArray(), x => x.GetProperty("id").GetInt32() == reviewId && x.GetProperty("comment").GetString() == "Ausführlicher Test");
                Assert.Contains(await GetArray(user, "/api/lists"), x => x.GetProperty("id").GetInt32() == listId);
            }
        }
        finally { Directory.Delete(dataDir, true); }
    }

    private static WebApplicationFactoryClientOptions ClientOptions() => new() { HandleCookies = true, AllowAutoRedirect = false };
    private static HttpRequestMessage Request(HttpMethod method, string path, object body)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        return request;
    }
    private static Task<HttpResponseMessage> Post(HttpClient client, string path, object body) => client.SendAsync(Request(HttpMethod.Post, path, body));
    private static Task<HttpResponseMessage> Put(HttpClient client, string path, object body) => client.SendAsync(Request(HttpMethod.Put, path, body));
    private static async Task AssertOk(Task<HttpResponseMessage> responseTask) => Assert.True((await responseTask).IsSuccessStatusCode);
    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync())).RootElement.Clone();
    }
    private static async Task<JsonElement> GetObject(HttpClient client, string path) => await ReadJson(await client.GetAsync(path));
    private static async Task<JsonElement[]> GetArray(HttpClient client, string path) => (await GetObject(client, path)).EnumerateArray().Select(x => x.Clone()).ToArray();
}

internal sealed class FilmPortalFactory(string dataDir) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("DATA_DIR", dataDir);
        builder.UseSetting("ADMIN_USERNAME", "siteadmin");
        builder.UseSetting("ADMIN_PASSWORD", "TestAdminPassword123");
        builder.UseSetting("TMDB_READ_ACCESS_TOKEN", "");
        builder.UseSetting("TMDB_SYNC_DISABLED", "true");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DATA_DIR"] = dataDir,
            ["ADMIN_USERNAME"] = "siteadmin",
            ["ADMIN_PASSWORD"] = "TestAdminPassword123",
            ["TMDB_READ_ACCESS_TOKEN"] = ""
        }));
    }
}
