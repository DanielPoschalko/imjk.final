namespace IMDbClone.Contracts;

public record LoginRequest(string Username, string Password);
public record RegisterRequest(string Username, string Password, string[]? FavoriteGenres);
public record ProfileRequest(string? NewUsername, string[]? FavoriteGenres);
public record MovieRequest(string Title, int Year, string? ImageUrl, string[]? Genres, string[]? Cast, string? Director, string? Description);
public record RatingRequest(int MovieId, int Score, string? ReviewText);
public record LibraryRequest(int MovieId, bool Value);
public record CommentRequest(string Text);
public record ListRequest(string Name, bool IsPublic);
public record ListItemRequest(int MovieId);
public record InviteRequest(string Username);
