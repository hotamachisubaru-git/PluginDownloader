namespace PluginDownloader.Services;

public sealed class ProviderLookupResult
{
    public ProviderLookupResult(
        string providerName,
        string projectDisplayName,
        string latestVersion,
        string? downloadUrl,
        string? suggestedFileName,
        string? note)
    {
        ProviderName = providerName;
        ProjectDisplayName = projectDisplayName;
        LatestVersion = latestVersion;
        DownloadUrl = downloadUrl;
        SuggestedFileName = suggestedFileName;
        Note = note;
    }

    public string ProviderName { get; }

    public string ProjectDisplayName { get; }

    public string LatestVersion { get; }

    public string? DownloadUrl { get; }

    public string? SuggestedFileName { get; }

    public string? Note { get; }

    public bool CanDownload => !string.IsNullOrWhiteSpace(DownloadUrl);
}
