using IMDbClone.Domain;
using Microsoft.EntityFrameworkCore;

namespace IMDbClone.Data;

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ReviewComment> Comments => Set<ReviewComment>();
    public DbSet<ReviewHelpful> HelpfulVotes => Set<ReviewHelpful>();
    public DbSet<UserMovie> UserMovies => Set<UserMovie>();
    public DbSet<MovieList> Lists => Set<MovieList>();
    public DbSet<MovieListItem> ListItems => Set<MovieListItem>();
    public DbSet<MovieListContributor> ListContributors => Set<MovieListContributor>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<AppUser>().HasIndex(x => x.Username).IsUnique();
        builder.Entity<AppUser>().Ignore(x => x.FavoriteGenres);
        builder.Entity<AppUser>().HasMany(x => x.Library).WithOne().HasForeignKey(x => x.UserId);
        builder.Entity<Movie>().HasIndex(x => new { x.Title, x.Year }).IsUnique();
        builder.Entity<Movie>().HasIndex(x => x.TmdbId).IsUnique();
        builder.Entity<Movie>().Ignore(x => x.Genres);
        builder.Entity<Movie>().Ignore(x => x.Cast);
        builder.Entity<Movie>().Ignore(x => x.AverageRating);
        builder.Entity<Movie>().Ignore(x => x.DisplayRating);
        builder.Entity<Review>().HasOne(x => x.Movie).WithMany(x => x.Reviews).HasForeignKey(x => x.MovieId);
        builder.Entity<Review>().HasMany(x => x.Comments).WithOne().HasForeignKey(x => x.ReviewId);
        builder.Entity<Review>().HasMany(x => x.HelpfulVotes).WithOne().HasForeignKey(x => x.ReviewId);
        builder.Entity<UserMovie>().HasKey(x => new { x.UserId, x.MovieId });
        builder.Entity<ReviewHelpful>().HasKey(x => new { x.ReviewId, x.UserId });
        builder.Entity<MovieListItem>().HasKey(x => new { x.ListId, x.MovieId });
        builder.Entity<MovieListContributor>().HasKey(x => new { x.ListId, x.UserId });
        builder.Entity<MovieList>().HasMany(x => x.Contributors).WithOne().HasForeignKey(x => x.ListId);
        builder.Entity<MovieList>().HasMany(x => x.Items).WithOne().HasForeignKey(x => x.ListId);
        builder.Entity<AppSetting>().HasKey(x => x.Key);
    }
}
