namespace PluginDownloader.Models;

public sealed class AppSettings
{
    public string OutputDirectory { get; set; } = string.Empty;

    public bool PromptForOutputDirectoryEachRun { get; set; }
}
