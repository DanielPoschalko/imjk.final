namespace IMDbClone.Services;

public sealed class TmdbOptions
{
    public string ReadAccessToken { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Language { get; set; } = "de-DE";
    public string Region { get; set; } = "DE";
    public int InitialLookbackDays { get; set; } = 30;
    public int MaxPagesPerSync { get; set; } = 100;
    public bool Disabled { get; set; }
    public bool IsConfigured => !Disabled && (!string.IsNullOrWhiteSpace(ReadAccessToken) || !string.IsNullOrWhiteSpace(ApiKey));
}

public record TmdbSyncResult(bool Configured, bool Success, int Imported, int Updated, DateTimeOffset? LastSuccessfulSync, string Message);
