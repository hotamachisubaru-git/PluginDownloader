namespace PluginDownloader.Models;

public sealed class UpdateResultEntry
{
    public UpdateResultEntry(
        string pluginName,
        string currentVersion,
        string latestVersion,
        string provider,
        string status,
        string savedPath,
        string message)
    {
        PluginName = pluginName;
        CurrentVersion = currentVersion;
        LatestVersion = latestVersion;
        Provider = provider;
        Status = status;
        SavedPath = savedPath;
        Message = message;
    }

    public string PluginName { get; }

    public string CurrentVersion { get; }

    public string LatestVersion { get; }

    public string Provider { get; }

    public string Status { get; }

    public string SavedPath { get; }

    public string Message { get; }
}
