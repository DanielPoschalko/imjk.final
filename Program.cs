using System.Text;
using System.Text.Json;

namespace IMDbClone;

public class User
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public List<string> FavoriteGenres { get; set; } = new();
}

public class Review
{
    public string Username { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Comment { get; set; } = string.Empty;
}

public class Movie
{
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = new();
    public List<string> Cast { get; set; } = new();
    public string Director { get; set; } = string.Empty;
    public string Plot { get; set; } = string.Empty;
    public List<Review> Reviews { get; set; } = new();

    public decimal AverageRating => Reviews.Count == 0
        ? 0m
        : Math.Round(Reviews.Average(r => (decimal)r.Score), 1);
}

public class MovieApp
{
    private readonly string _dataDirectory;
    private readonly string _usersFilePath;
    private readonly string _moviesFilePath;

    public List<User> Users { get; } = new();
    public List<Movie> Movies { get; } = new();

    public MovieApp(string? dataDirectory = null)
    {
        _dataDirectory = string.IsNullOrWhiteSpace(dataDirectory)
            ? Path.Combine(Directory.GetCurrentDirectory(), "app-data")
            : dataDirectory;

        Directory.CreateDirectory(_dataDirectory);

        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var isProjectRoot = Directory.GetFiles(currentDirectory, "*.csproj").Length > 0;
            var legacyDataDirectory = Path.Combine(AppContext.BaseDirectory, "app-data");

            if (isProjectRoot
                && !File.Exists(Path.Combine(_dataDirectory, "users.json"))
                && !File.Exists(Path.Combine(_dataDirectory, "movies.json"))
                && Directory.Exists(legacyDataDirectory))
            {
                foreach (var file in Directory.GetFiles(legacyDataDirectory))
                {
                    var target = Path.Combine(_dataDirectory, Path.GetFileName(file));
                    if (!File.Exists(target))
                    {
                        File.Copy(file, target);
                    }
                }
            }
        }

        _usersFilePath = Path.Combine(_dataDirectory, "users.json");
        _moviesFilePath = Path.Combine(_dataDirectory, "movies.json");

        if (File.Exists(_usersFilePath) || File.Exists(_moviesFilePath))
        {
            LoadFromDisk();
            return;
        }

        SeedUsers();
        SeedMovies();
        SaveToDisk();
    }

    public void SaveToDisk()
    {
        Directory.CreateDirectory(_dataDirectory);

        var usersJson = JsonSerializer.Serialize(Users, new JsonSerializerOptions { WriteIndented = true });
        var moviesJson = JsonSerializer.Serialize(Movies, new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(_usersFilePath, usersJson);
        File.WriteAllText(_moviesFilePath, moviesJson);
    }

    private void LoadFromDisk()
    {
        Users.Clear();
        Movies.Clear();

        if (File.Exists(_usersFilePath))
        {
            var usersJson = File.ReadAllText(_usersFilePath);
            if (!string.IsNullOrWhiteSpace(usersJson))
            {
                var loadedUsers = JsonSerializer.Deserialize<List<User>>(usersJson);
                if (loadedUsers is not null)
                {
                    Users.AddRange(loadedUsers);
                }
            }
        }

        if (File.Exists(_moviesFilePath))
        {
            var moviesJson = File.ReadAllText(_moviesFilePath);
            if (!string.IsNullOrWhiteSpace(moviesJson))
            {
                var loadedMovies = JsonSerializer.Deserialize<List<Movie>>(moviesJson);
                if (loadedMovies is not null)
                {
                    Movies.AddRange(loadedMovies);
                }
            }
        }

        if (Users.Count == 0 && Movies.Count == 0)
        {
            SeedUsers();
            SeedMovies();
            SaveToDisk();
        }
    }

    public User RegisterUser(string username, string password, IEnumerable<string>? favoriteGenres = null)
    {
        var cleanedUsername = username.Trim();
        if (string.IsNullOrWhiteSpace(cleanedUsername))
            throw new ArgumentException("Benutzername darf nicht leer sein.");

        if (Users.Any(u => string.Equals(u.Username, cleanedUsername, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Benutzername existiert bereits.");

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Passwort darf nicht leer sein.");

        var user = new User
        {
            Username = cleanedUsername,
            Password = password,
            FavoriteGenres = favoriteGenres?
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Select(g => g.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>()
        };

        Users.Add(user);
        SaveToDisk();
        return user;
    }

    public User? Login(string username, string password)
    {
        return Users.FirstOrDefault(u =>
            string.Equals(u.Username, username.Trim(), StringComparison.OrdinalIgnoreCase)
            && u.Password == password);
    }

    public Movie AddMovie(string title, int year, string imageUrl, IEnumerable<string> genres, IEnumerable<string> cast, string director, string description)
    {
        var cleanedTitle = title.Trim();
        if (string.IsNullOrWhiteSpace(cleanedTitle))
            throw new ArgumentException("Filmname darf nicht leer sein.");

        if (Movies.Any(m => string.Equals(m.Title, cleanedTitle, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Dieser Film ist bereits vorhanden.");

        var movie = new Movie
        {
            Title = cleanedTitle,
            Year = year,
            ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? "https://m.media-amazon.com/images/G/01/imdb/images/social/imdb_logo.png" : imageUrl,
            Genres = genres.Where(g => !string.IsNullOrWhiteSpace(g)).Select(g => g.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Cast = cast.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Director = director.Trim(),
            Plot = description.Trim(),
            Reviews = new List<Review>()
        };

        Movies.Add(movie);
        SaveToDisk();
        return movie;
    }

    public Movie? GetMovieByTitle(string title)
    {
        return Movies.FirstOrDefault(m => string.Equals(m.Title, title.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public User UpdateUserProfile(string currentUsername, string? newUsername, IEnumerable<string>? favoriteGenres)
    {
        var user = Users.FirstOrDefault(u => string.Equals(u.Username, currentUsername, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Benutzer nicht gefunden.");

        var cleanedNewUsername = newUsername?.Trim();
        if (!string.IsNullOrWhiteSpace(cleanedNewUsername) && !string.Equals(user.Username, cleanedNewUsername, StringComparison.OrdinalIgnoreCase))
        {
            if (Users.Any(u => string.Equals(u.Username, cleanedNewUsername, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Benutzername bereits vergeben.");

            user.Username = cleanedNewUsername;
        }

        user.FavoriteGenres = favoriteGenres?
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Select(g => g.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        SaveToDisk();
        return user;
    }

    public void AddRating(string movieTitle, string username, int score)
    {
        var movie = Movies.FirstOrDefault(m =>
            string.Equals(m.Title, movieTitle, StringComparison.OrdinalIgnoreCase));

        if (movie is null)
            throw new InvalidOperationException("Film nicht gefunden.");

        if (score < 1 || score > 10)
            throw new ArgumentOutOfRangeException(nameof(score), "Bewertung muss zwischen 1 und 10 liegen.");

        movie.Reviews.Add(new Review
        {
            Username = username,
            Score = score,
            Comment = "Bewertung via App"
        });

        SaveToDisk();
    }

    public List<Movie> FilterMoviesByGenre(string genre)
    {
        if (string.IsNullOrWhiteSpace(genre))
            return Movies.OrderByDescending(m => m.AverageRating).ToList();

        return Movies
            .Where(m => m.Genres.Any(g => g.Contains(genre, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(m => m.AverageRating)
            .ToList();
    }

    public List<Movie> FilterMoviesByActor(string actor)
    {
        if (string.IsNullOrWhiteSpace(actor))
            return Movies.OrderByDescending(m => m.AverageRating).ToList();

        return Movies
            .Where(m => m.Cast.Any(c => c.Contains(actor, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(m => m.AverageRating)
            .ToList();
    }

    public List<Movie> SearchMovies(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Movies.OrderByDescending(m => m.AverageRating).ToList();

        return Movies
            .Where(m =>
                m.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || m.Genres.Any(g => g.Contains(query, StringComparison.OrdinalIgnoreCase))
                || m.Cast.Any(c => c.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(m => m.AverageRating)
            .ToList();
    }

    private static readonly Dictionary<string, string> OfficialImdbPosterUrls = new(StringComparer.OrdinalIgnoreCase)
    {
        ["The Shawshank Redemption"] = "https://m.media-amazon.com/images/M/MV5BMDAyY2FhYjctNDc5OS00MDNlLThiMGUtY2UxYWVkNGY2ZjljXkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["The Godfather"] = "https://m.media-amazon.com/images/M/MV5BMjg5ZGNmZWMtY2ZmNy00OWFiLThkMjAtMjRlNDYzZDJkNThlXkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["The Dark Knight"] = "https://m.media-amazon.com/images/M/MV5BMDQ5MWU2YWUtNTQ4OC00Njk5LWI0NzctMjM4OGZiNmZmNGViXkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["The Godfather Part II"] = "https://m.media-amazon.com/images/M/MV5BMDIxMzBlZDktZjMxNy00ZGI4LTgxNDEtYWRlNzRjMjJmOGQ1XkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["12 Angry Men"] = "https://m.media-amazon.com/images/M/MV5BNGE0OTU1MmMtNjUyZi00ZGM0LWFjMGUtZTJjYmY4ZjJhMzA5XkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["Schindler's List"] = "https://m.media-amazon.com/images/M/MV5BMjAzNTg4MzUtNzBjNC00OGE1LWE3YmQtYWNiY2UwYWJlY2YxXkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["The Lord of the Rings: The Return of the King"] = "https://m.media-amazon.com/images/M/MV5BOWRmOTdkNjctY2YxMC00Y2E4LWFkMjktMzVmNjAxN2RmODJhXkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["Pulp Fiction"] = "https://m.media-amazon.com/images/M/MV5BZTRjYmY1NmYtMmU0Ny00OWQ4LWI5ZDUtMDE0MzU1YWZjYTFhXkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["The Good, the Bad and the Ugly"] = "https://m.media-amazon.com/images/M/MV5BNjgyOWViNjYtZDFjOC00NDg1LWFmZDYtNmVmYjdiYWE5YTljXkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["Forrest Gump"] = "https://m.media-amazon.com/images/M/MV5BYmM4MDY3NWUtMmI1YS00NTZjLTgwYWQtYzA1MGNkMTIxYjA4XkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["Fight Club"] = "https://m.media-amazon.com/images/M/MV5BMTc0MDE1OTYwN15BMl5BanBnXkFtZTYwNjY3NzY3XkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["Inception"] = "https://m.media-amazon.com/images/M/MV5BMjQwNGE0MjctMDA3Yi00MGEyLTkyNTAtMjcxMTYxOWE0ZWRkXkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["The Matrix"] = "https://m.media-amazon.com/images/M/MV5BYjM1YzE0MzMtMDM4Ni00M2M4LWEyYzItNGFjMDkxZTcyNTEyXkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["Goodfellas"] = "https://m.media-amazon.com/images/M/MV5BZTUzZjc3Y2ItNDlhOC00MmY4LWIxMWQtYTlhZjk4ZGI5YWQ3XkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["Spirited Away"] = "https://m.media-amazon.com/images/M/MV5BNWJlMmQ0ZjktNTFiYy00YzQ5LTg5NGQtY2NjNjRjMTRhMDJjXkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["Interstellar"] = "https://m.media-amazon.com/images/M/MV5BNmIwM2E3YjMtYWUxYi00M2ZkLWFmMzktZWFiYTU4MGM2YTk2XkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["Parasite"] = "https://m.media-amazon.com/images/M/MV5BYjk1Y2U4MjQtY2ZiNS00OWQyLWI3MmYtZWUwNmRjYWRiNWNhXkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["The Green Mile"] = "https://m.media-amazon.com/images/M/MV5BNTEyY2E4OGItZGE4Yy00OTI1LThmMWMtNDIyMmY3YzU0MGY0XkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["The Lord of the Rings: The Fellowship of the Ring"] = "https://m.media-amazon.com/images/M/MV5BZGFmNDg3ZmMtMmMyMC00NjA5LWFmNGMtYmY2YjFhMDc5YjI1XkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["The Empire Strikes Back"] = "https://m.media-amazon.com/images/M/MV5BYWVhZWQyN2EtNzZhNC00ZDIxLWIxNzEtMDliNmIyYzc5ZTE5XkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["The Silence of the Lambs"] = "https://m.media-amazon.com/images/M/MV5BMTM5N2JiN2YtNmVmZS00YTA4LWI0NDgtMzFiMjRmZDU2YmUzXkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["Saving Private Ryan"] = "https://m.media-amazon.com/images/M/MV5BODhhNmFhMWMtYmUxOC00NzNmLWE5NzUtY2Q0ZDVhMjg5MWEwXkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["The Departed"] = "https://m.media-amazon.com/images/M/MV5BNzcxMWIwYjktMzY4Mi00NjEwLTllYzYtZWMxNzFjMWU0ZmYyXkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["The Prestige"] = "https://m.media-amazon.com/images/M/MV5BMGQ1YjdkOWMtOTdiNS00YjYwLWEzMWQtNDdmN2FlZTQ2NGJhXkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["Gladiator"] = "https://m.media-amazon.com/images/M/MV5BNWNmYTU2ZmItMTBjYS00OGJmLTliMDgtN2VkNmU4OGQ2NjZjXkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["Se7en"] = "https://m.media-amazon.com/images/M/MV5BMDZjNjZlNjQtZjY5Ny00ODdhLTlhYTAtOTc3ZmMyOTcyMzhhXkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["The Usual Suspects"] = "https://m.media-amazon.com/images/M/MV5BMDRiZWZjMjQtMWE2Zi00NDgzLWI2MWItMDA1OTUyOTc4YmYyXkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["Whiplash"] = "https://m.media-amazon.com/images/M/MV5BNTZiZDhjNjMtYzhlMy00MmQwLWIzNzEtZGNjMmQwYTE0YjY3XkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg",
        ["The Lion King"] = "https://m.media-amazon.com/images/M/MV5BZjk3NjI3M2MtYWQwZi00MDRjLTk1NDMtMDMwYmU2NTQ3Nzc4XkEyXkFqcGc@._V1_FMjpg_UX1000_.jpg"
    };

    private static string GetOfficialImdbPosterUrl(string title)
    {
        if (OfficialImdbPosterUrls.TryGetValue(title, out var posterUrl))
            return posterUrl;

        return "https://m.media-amazon.com/images/G/01/imdb/images/social/imdb_logo.png";
    }

    private void SeedUsers()
    {
        Users.Add(new User { Username = "admin", Password = "1234", FavoriteGenres = new List<string> { "Sci-Fi", "Drama" } });
    }

    private void SeedMovies()
    {
        Movies.Clear();

        var seedMovies = new[]
        {
            new { Title = "The Shawshank Redemption", Year = 1994, ImageUrl = "https://images.unsplash.com/photo-1517604931442-7e0c8ed2963c?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Drama", "Crime" }, Cast = new[] { "Tim Robbins", "Morgan Freeman", "Bob Gunton" }, Director = "Frank Darabont", Plot = "Ein Banker wird in einem Gefängnis zu einem Symbol für Hoffnung und Widerstandsfähigkeit.", Reviews = new[] { new { Username = "admin", Score = 10, Comment = "Klassiker" }, new { Username = "guest", Score = 9, Comment = "Fantastisch" } } },
            new { Title = "The Godfather", Year = 1972, ImageUrl = "https://images.unsplash.com/photo-1513106580091-1d82408b8cd6?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Crime", "Drama" }, Cast = new[] { "Marlon Brando", "Al Pacino", "James Caan" }, Director = "Francis Ford Coppola", Plot = "Die Corleone-Familie gerät in einen Machtkampf zwischen Loyalität, Verrat und Familienbande.", Reviews = new[] { new { Username = "admin", Score = 10, Comment = "Meisterwerk" }, new { Username = "guest", Score = 9, Comment = "Genial" } } },
            new { Title = "The Dark Knight", Year = 2008, ImageUrl = "https://images.unsplash.com/photo-1536440136628-849c177e76a1?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Action", "Crime", "Drama" }, Cast = new[] { "Christian Bale", "Heath Ledger", "Aaron Eckhart" }, Director = "Christopher Nolan", Plot = "Batman muss die Stadt vor einem unberechenbaren Schurken und der eigenen Moral schützen.", Reviews = new[] { new { Username = "admin", Score = 10, Comment = "Perfekt" }, new { Username = "guest", Score = 9, Comment = "Toller Plot" } } },
            new { Title = "The Godfather Part II", Year = 1974, ImageUrl = "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Crime", "Drama" }, Cast = new[] { "Al Pacino", "Robert De Niro", "Diane Keaton" }, Director = "Francis Ford Coppola", Plot = "Die Geschichte der Familie Corleone erstreckt sich über mehrere Generationen und dunkle Entscheidungen.", Reviews = new[] { new { Username = "admin", Score = 9, Comment = "Brillant" }, new { Username = "guest", Score = 9, Comment = "Stark" } } },
            new { Title = "12 Angry Men", Year = 1957, ImageUrl = "https://images.unsplash.com/photo-1524985069026-dd778a71c7b4?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Drama", "Crime" }, Cast = new[] { "Henry Fonda", "Lee J. Cobb", "Martin Balsam" }, Director = "Sidney Lumet", Plot = "Zwölf Geschworene müssen über das Schicksal eines jungen Mannes entscheiden und stellen dabei ihre Vorurteile in Frage.", Reviews = new[] { new { Username = "admin", Score = 9, Comment = "Zeitlos" }, new { Username = "guest", Score = 8, Comment = "Großartig" } } },
            new { Title = "Schindler's List", Year = 1993, ImageUrl = "https://images.unsplash.com/photo-1504384308090-c894fdcc538d?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Drama", "History", "War" }, Cast = new[] { "Liam Neeson", "Ben Kingsley", "Ralph Fiennes" }, Director = "Steven Spielberg", Plot = "Ein Unternehmer rettet während des Holocausts Tausende Menschen und zeigt unglaubliche Menschlichkeit.", Reviews = new[] { new { Username = "admin", Score = 10, Comment = "Beeindruckend" }, new { Username = "guest", Score = 9, Comment = "Wuchtig" } } },
            new { Title = "The Lord of the Rings: The Return of the King", Year = 2003, ImageUrl = "https://images.unsplash.com/photo-1513106580091-1d82408b8cd6?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Fantasy", "Adventure", "Action" }, Cast = new[] { "Elijah Wood", "Viggo Mortensen", "Ian McKellen" }, Director = "Peter Jackson", Plot = "Die Gefährten stehen vor dem finalen Kampf, um Mittelerde vor dem Schatten des Rings zu retten.", Reviews = new[] { new { Username = "admin", Score = 10, Comment = "Episch" }, new { Username = "guest", Score = 9, Comment = "Fantastisch" } } },
            new { Title = "Pulp Fiction", Year = 1994, ImageUrl = "https://images.unsplash.com/photo-1516280440614-37939bbacd81?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Crime", "Drama" }, Cast = new[] { "John Travolta", "Samuel L. Jackson", "Uma Thurman" }, Director = "Quentin Tarantino", Plot = "Verschiedene Charaktere überkreuzen sich in einer chaotischen, aber brillanten Story über Gewalt und Schicksal.", Reviews = new[] { new { Username = "admin", Score = 9, Comment = "Genial" }, new { Username = "guest", Score = 9, Comment = "Kult" } } },
            new { Title = "The Good, the Bad and the Ugly", Year = 1966, ImageUrl = "https://images.unsplash.com/photo-1500530855697-b586d89ba3ee?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Western", "Adventure" }, Cast = new[] { "Clint Eastwood", "Lee Van Cleef", "Eli Wallach" }, Director = "Sergio Leone", Plot = "Drei Revolverhelden jagen nach einem geheimen Goldschatz durch den wilden Westen.", Reviews = new[] { new { Username = "admin", Score = 9, Comment = "Legende" }, new { Username = "guest", Score = 8, Comment = "Klassisch" } } },
            new { Title = "Forrest Gump", Year = 1994, ImageUrl = "https://images.unsplash.com/photo-1497032628192-86f99bcd76bc?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Drama", "Romance" }, Cast = new[] { "Tom Hanks", "Robin Wright", "Gary Sinise" }, Director = "Robert Zemeckis", Plot = "Ein einfacher Mann erlebt das ganze 20. Jahrhundert und bleibt dabei immer er selbst.", Reviews = new[] { new { Username = "admin", Score = 10, Comment = "Herzerwärmend" }, new { Username = "guest", Score = 9, Comment = "Großartig" } } },
            new { Title = "Fight Club", Year = 1999, ImageUrl = "https://images.unsplash.com/photo-1513106580091-1d82408b8cd6?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Drama", "Thriller" }, Cast = new[] { "Brad Pitt", "Edward Norton", "Helena Bonham Carter" }, Director = "David Fincher", Plot = "Ein gelangweilter Büroangestellter gründet eine gefährliche Underground-Bewegung mit weitreichenden Folgen.", Reviews = new[] { new { Username = "admin", Score = 9, Comment = "Düster" }, new { Username = "guest", Score = 9, Comment = "Kultfilm" } } },
            new { Title = "Inception", Year = 2010, ImageUrl = "https://images.unsplash.com/photo-1516574187841-cb9cc2ca948b?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Sci-Fi", "Action", "Thriller" }, Cast = new[] { "Leonardo DiCaprio", "Joseph Gordon-Levitt", "Elliot Page" }, Director = "Christopher Nolan", Plot = "Ein Dieb stiehlt Informationen aus Träumen und muss einen Weg zur Rückkehr finden.", Reviews = new[] { new { Username = "admin", Score = 10, Comment = "Genial" }, new { Username = "guest", Score = 9, Comment = "Visionär" } } },
            new { Title = "The Matrix", Year = 1999, ImageUrl = "https://images.unsplash.com/photo-1478720568477-152d9b164e26?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Sci-Fi", "Action" }, Cast = new[] { "Keanu Reeves", "Laurence Fishburne", "Carrie-Anne Moss" }, Director = "Lana Wachowski", Plot = "Ein Hacker entdeckt die Wahrheit über die Welt und das System, das sie kontrolliert.", Reviews = new[] { new { Username = "admin", Score = 9, Comment = "Klassiker" }, new { Username = "guest", Score = 9, Comment = "Brilliant" } } },
            new { Title = "Goodfellas", Year = 1990, ImageUrl = "https://images.unsplash.com/photo-1497032628192-86f99bcd76bc?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Crime", "Drama" }, Cast = new[] { "Ray Liotta", "Robert De Niro", "Joe Pesci" }, Director = "Martin Scorsese", Plot = "Ein junger Mann steigt in das organisierte Verbrechen ein und wird von Macht und Gewalt verführt.", Reviews = new[] { new { Username = "admin", Score = 9, Comment = "Rasant" }, new { Username = "guest", Score = 8, Comment = "Exzellent" } } },
            new { Title = "Spirited Away", Year = 2001, ImageUrl = "https://images.unsplash.com/photo-1516117172878-fd2c41f4a759?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Animation", "Fantasy" }, Cast = new[] { "Rumi Hiiragi", "Miyu Irino", "Mari Natsuki" }, Director = "Hayao Miyazaki", Plot = "Ein Mädchen verirrt sich in einer magischen Welt und muss ihren Mut beweisen, um nach Hause zurückzukehren.", Reviews = new[] { new { Username = "admin", Score = 10, Comment = "Magisch" }, new { Username = "guest", Score = 9, Comment = "Traumhaft" } } },
            new { Title = "Interstellar", Year = 2014, ImageUrl = "https://images.unsplash.com/photo-1419242902214-272b3f66ee7a?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Sci-Fi", "Adventure", "Drama" }, Cast = new[] { "Matthew McConaughey", "Anne Hathaway", "Jessica Chastain" }, Director = "Christopher Nolan", Plot = "Ein Astronaut reist durch ein Wurmloch, um die Zukunft der Menschheit zu retten.", Reviews = new[] { new { Username = "admin", Score = 9, Comment = "Visionär" }, new { Username = "guest", Score = 9, Comment = "Großartig" } } },
            new { Title = "Parasite", Year = 2019, ImageUrl = "https://images.unsplash.com/photo-1524985069026-dd778a71c7b4?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Thriller", "Drama", "Comedy" }, Cast = new[] { "Song Kang-ho", "Lee Sun-kyun", "Cho Yeo-jeong" }, Director = "Bong Joon-ho", Plot = "Eine Familie versucht sich in die Lebensweise einer wohlhabenden Familie einzuschleichen und gerät dadurch in einen gefährlichen Strudel.", Reviews = new[] { new { Username = "admin", Score = 10, Comment = "Meisterhaft" }, new { Username = "guest", Score = 9, Comment = "Brillant" } } },
            new { Title = "The Green Mile", Year = 1999, ImageUrl = "https://images.unsplash.com/photo-1504384308090-c894fdcc538d?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Crime", "Drama", "Fantasy" }, Cast = new[] { "Tom Hanks", "Michael Clarke Duncan", "David Morse" }, Director = "Frank Darabont", Plot = "Ein Wärter erkennt an einem verurteilten Mörder eine unheimliche Gabe und erlebt dabei die Wahrheit über Gerechtigkeit.", Reviews = new[] { new { Username = "admin", Score = 9, Comment = "Bewegend" }, new { Username = "guest", Score = 8, Comment = "Sehr gut" } } },
            new { Title = "The Lord of the Rings: The Fellowship of the Ring", Year = 2001, ImageUrl = "https://images.unsplash.com/photo-1518623489648-a173ef7824f3?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Fantasy", "Adventure" }, Cast = new[] { "Elijah Wood", "Ian McKellen", "Viggo Mortensen" }, Director = "Peter Jackson", Plot = "Ein Hobbit und seine Freunde müssen einen Ring an einen sicheren Ort bringen und dabei die ganze Welt verändern.", Reviews = new[] { new { Username = "admin", Score = 9, Comment = "Episch" }, new { Username = "guest", Score = 9, Comment = "Wunderbar" } } },
            new { Title = "The Empire Strikes Back", Year = 1980, ImageUrl = "https://images.unsplash.com/photo-1464306076886-da185f6a9d8c?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Sci-Fi", "Adventure" }, Cast = new[] { "Mark Hamill", "Harrison Ford", "Carrie Fisher" }, Director = "Irvin Kershner", Plot = "Luke Skywalker trainiert unter Yoda, während Vader die Galaxis und den Jedi-Orden bedroht.", Reviews = new[] { new { Username = "admin", Score = 9, Comment = "Kult" }, new { Username = "guest", Score = 9, Comment = "Klassiker" } } },
            new { Title = "The Silence of the Lambs", Year = 1991, ImageUrl = "https://images.unsplash.com/photo-1513106580091-1d82408b8cd6?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Crime", "Thriller", "Drama" }, Cast = new[] { "Jodie Foster", "Anthony Hopkins", "Scott Glenn" }, Director = "Jonathan Demme", Plot = "Eine FBI-Agentin sucht Hilfe bei einem brillanten Serienmörder, um einen weiteren Täter zu finden.", Reviews = new[] { new { Username = "admin", Score = 9, Comment = "Spannend" }, new { Username = "guest", Score = 8, Comment = "Tief" } } },
            new { Title = "Saving Private Ryan", Year = 1998, ImageUrl = "https://images.unsplash.com/photo-1516280440614-37939bbacd81?auto=format&fit=crop&w=900&q=80", Genres = new[] { "War", "Drama", "History" }, Cast = new[] { "Tom Hanks", "Matt Damon", "Vin Diesel" }, Director = "Steven Spielberg", Plot = "Eine kleine Einheit wird entsandt, um den letzten noch lebenden Bruder eines getöteten Soldaten zu retten.", Reviews = new[] { new { Username = "admin", Score = 9, Comment = "Gewaltig" }, new { Username = "guest", Score = 8, Comment = "Ergreifend" } } },
            new { Title = "The Departed", Year = 2006, ImageUrl = "https://images.unsplash.com/photo-1524985069026-dd778a71c7b4?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Crime", "Drama", "Thriller" }, Cast = new[] { "Leonardo DiCaprio", "Matt Damon", "Jack Nicholson" }, Director = "Martin Scorsese", Plot = "Ein Undercover-Ermittler und ein Mafia-Informant geraten in einen tödlichen Wettrennen.", Reviews = new[] { new { Username = "admin", Score = 9, Comment = "Perfekt" }, new { Username = "guest", Score = 8, Comment = "Toll" } } },
            new { Title = "The Prestige", Year = 2006, ImageUrl = "https://images.unsplash.com/photo-1500530855697-b586d89ba3ee?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Drama", "Mystery", "Thriller" }, Cast = new[] { "Christian Bale", "Hugh Jackman", "Scarlett Johansson" }, Director = "Christopher Nolan", Plot = "Zwei Magier kämpfen in einer Welt voller Intrigen um den Titel des größten Zauberers der Welt.", Reviews = new[] { new { Username = "admin", Score = 9, Comment = "Intelligent" }, new { Username = "guest", Score = 8, Comment = "Faszinierend" } } },
            new { Title = "Gladiator", Year = 2000, ImageUrl = "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Action", "Drama", "History" }, Cast = new[] { "Russell Crowe", "Joaquin Phoenix", "Connie Nielsen" }, Director = "Ridley Scott", Plot = "Ein römischer General wird zum Sklaven und kämpft um Gerechtigkeit und Rache.", Reviews = new[] { new { Username = "admin", Score = 9, Comment = "Heroisch" }, new { Username = "guest", Score = 8, Comment = "Eindrucksvoll" } } },
            new { Title = "Se7en", Year = 1995, ImageUrl = "https://images.unsplash.com/photo-1478720568477-152d9b164e26?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Crime", "Drama", "Mystery" }, Cast = new[] { "Brad Pitt", "Morgan Freeman", "Gwyneth Paltrow" }, Director = "David Fincher", Plot = "Zwei Detectives jagen einen Serienmörder, der die sieben Todsünden als Material benutzt.", Reviews = new[] { new { Username = "admin", Score = 9, Comment = "Düster" }, new { Username = "guest", Score = 8, Comment = "Mitreißend" } } },
            new { Title = "The Usual Suspects", Year = 1995, ImageUrl = "https://images.unsplash.com/photo-1497032628192-86f99bcd76bc?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Crime", "Mystery", "Thriller" }, Cast = new[] { "Kevin Spacey", "Gabriel Byrne", "Chazz Palminteri" }, Director = "Bryan Singer", Plot = "Ein Mafia-Verbrecher erzählt das rätselhafte und spektakuläre Schicksal eines mysteriösen Mannes.", Reviews = new[] { new { Username = "admin", Score = 9, Comment = "Smart" }, new { Username = "guest", Score = 8, Comment = "Unvorhersehbar" } } },
            new { Title = "Whiplash", Year = 2014, ImageUrl = "https://images.unsplash.com/photo-1516280440614-37939bbacd81?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Drama", "Music" }, Cast = new[] { "Miles Teller", "J.K. Simmons", "Melissa Benoist" }, Director = "Damien Chazelle", Plot = "Ein ambitionierter Schlagzeuger muss bei einem extrem fordernden Musiklehrer alles geben, um Erfolg zu haben.", Reviews = new[] { new { Username = "admin", Score = 9, Comment = "Kraftvoll" }, new { Username = "guest", Score = 8, Comment = "Sehr gut" } } },
            new { Title = "The Lion King", Year = 1994, ImageUrl = "https://images.unsplash.com/photo-1516117172878-fd2c41f4a759?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Animation", "Adventure", "Drama" }, Cast = new[] { "Matthew Broderick", "Jeremy Irons", "James Earl Jones" }, Director = "Roger Allers", Plot = "Ein junger Löwe steht vor der Entscheidung, sein Erbe anzunehmen und die Zukunft zu bewahren.", Reviews = new[] { new { Username = "admin", Score = 9, Comment = "Herzerwärmend" }, new { Username = "guest", Score = 8, Comment = "Wunderbar" } } },
            new { Title = "Toy Story 3", Year = 2010, ImageUrl = "https://images.unsplash.com/photo-1513106580091-1d82408b8cd6?auto=format&fit=crop&w=900&q=80", Genres = new[] { "Animation", "Adventure", "Comedy" }, Cast = new[] { "Tom Hanks", "Tim Allen", "Joan Cusack" }, Director = "Lee Unkrich", Plot = "Woody und Buzz müssen sich mit dem Schicksal einer Kinderzimmer-Welt auseinandersetzen und bleiben dabei zusammen.", Reviews = new[] { new { Username = "admin", Score = 8, Comment = "Süß" }, new { Username = "guest", Score = 8, Comment = "Familienfilm" } } }
        };

        foreach (var movieSeed in seedMovies)
        {
            Movies.Add(new Movie
            {
                Title = movieSeed.Title,
                Year = movieSeed.Year,
                ImageUrl = GetOfficialImdbPosterUrl(movieSeed.Title),
                Genres = movieSeed.Genres.ToList(),
                Cast = movieSeed.Cast.ToList(),
                Director = movieSeed.Director,
                Plot = movieSeed.Plot,
                Reviews = movieSeed.Reviews.Select(r => new Review
                {
                    Username = r.Username,
                    Score = r.Score,
                    Comment = r.Comment
                }).ToList()
            });
        }
    }
}

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string[] FavoriteGenres { get; set; } = Array.Empty<string>();
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RatingRequest
{
    public string MovieTitle { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int Score { get; set; }
    public string ReviewText { get; set; } = string.Empty;
}

public class MovieCreateRequest
{
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string[] Genres { get; set; } = Array.Empty<string>();
    public string[] Cast { get; set; } = Array.Empty<string>();
    public string Director { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class ProfileUpdateRequest
{
    public string Username { get; set; } = string.Empty;
    public string? NewUsername { get; set; }
    public string[] FavoriteGenres { get; set; } = Array.Empty<string>();
}

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSingleton<MovieApp>();

        var app = builder.Build();

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapGet("/api/movies", (MovieApp movieApp, string? genre, string? actor, string? query) =>
        {
            var movies = new List<Movie>();

            if (!string.IsNullOrWhiteSpace(genre))
            {
                movies = movieApp.FilterMoviesByGenre(genre);
            }
            else if (!string.IsNullOrWhiteSpace(actor))
            {
                movies = movieApp.FilterMoviesByActor(actor);
            }
            else if (!string.IsNullOrWhiteSpace(query))
            {
                movies = movieApp.SearchMovies(query);
            }
            else
            {
                movies = movieApp.Movies.OrderByDescending(m => m.AverageRating).ToList();
            }

            return Results.Json(movies.Select(m => new
            {
                title = m.Title,
                year = m.Year,
                imageUrl = m.ImageUrl,
                genres = m.Genres,
                cast = m.Cast,
                director = m.Director,
                plot = m.Plot,
                averageRating = m.AverageRating,
                reviewCount = m.Reviews.Count,
                reviews = m.Reviews
            }));
        });

        app.MapGet("/api/movies/by-title", (MovieApp movieApp, string title) =>
        {
            var movie = movieApp.GetMovieByTitle(title);
            if (movie is null) return Results.NotFound(new { error = "Film nicht gefunden." });

            return Results.Json(new
            {
                title = movie.Title,
                year = movie.Year,
                imageUrl = movie.ImageUrl,
                genres = movie.Genres,
                cast = movie.Cast,
                director = movie.Director,
                plot = movie.Plot,
                averageRating = movie.AverageRating,
                reviewCount = movie.Reviews.Count,
                reviews = movie.Reviews.Select(r => new { username = r.Username, score = r.Score, comment = r.Comment })
            });
        });

        app.MapPost("/api/movies", async (HttpContext context, MovieApp movieApp) =>
        {
            try
            {
                var request = await context.Request.ReadFromJsonAsync<MovieCreateRequest>();
                if (request is null) return Results.BadRequest(new { error = "Fehlerhafte Anfrage." });

                var movie = movieApp.AddMovie(request.Title, request.Year, request.ImageUrl, request.Genres, request.Cast, request.Director, request.Description);
                return Results.Ok(new { success = true, movie = new { title = movie.Title, imageUrl = movie.ImageUrl } });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/register", async (HttpContext context, MovieApp movieApp) =>
        {
            try
            {
                var request = await context.Request.ReadFromJsonAsync<RegisterRequest>();
                if (request is null) return Results.BadRequest(new { error = "Fehlerhafte Anfrage." });

                var user = movieApp.RegisterUser(request.Username, request.Password, request.FavoriteGenres);
                return Results.Ok(new { success = true, user = new { username = user.Username, favoriteGenres = user.FavoriteGenres } });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/login", async (HttpContext context, MovieApp movieApp) =>
        {
            try
            {
                var request = await context.Request.ReadFromJsonAsync<LoginRequest>();
                if (request is null) return Results.BadRequest(new { error = "Fehlerhafte Anfrage." });

                var user = movieApp.Login(request.Username, request.Password);
                if (user is null)
                    return Results.BadRequest(new { error = "Login fehlgeschlagen." });

                return Results.Ok(new { success = true, user = new { username = user.Username, favoriteGenres = user.FavoriteGenres } });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/rating", async (HttpContext context, MovieApp movieApp) =>
        {
            try
            {
                var request = await context.Request.ReadFromJsonAsync<RatingRequest>();
                if (request is null) return Results.BadRequest(new { error = "Fehlerhafte Anfrage." });

                movieApp.AddRating(request.MovieTitle, request.Username, request.Score);
                var movie = movieApp.Movies.First(m => m.Title.Equals(request.MovieTitle, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(request.ReviewText))
                {
                    var review = movie.Reviews.LastOrDefault(r => r.Username == request.Username);
                    if (review is not null)
                    {
                        review.Comment = request.ReviewText;
                    }
                }

                movieApp.SaveToDisk();
                return Results.Ok(new { success = true, averageRating = movie.AverageRating, reviewCount = movie.Reviews.Count });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPut("/api/profile", async (HttpContext context, MovieApp movieApp) =>
        {
            try
            {
                var request = await context.Request.ReadFromJsonAsync<ProfileUpdateRequest>();
                if (request is null) return Results.BadRequest(new { error = "Fehlerhafte Anfrage." });

                var updated = movieApp.UpdateUserProfile(request.Username, request.NewUsername, request.FavoriteGenres);
                return Results.Ok(new { success = true, user = new { username = updated.Username, favoriteGenres = updated.FavoriteGenres } });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.Run();
    }
}

