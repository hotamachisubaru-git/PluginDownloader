using System.IO;

namespace PluginDownloader.Models;

public sealed class PluginEntry : BindableBase
{
    private string _pluginName;
    private string _currentVersion;
    private string _website;
    private string _detectionStatus;

    public PluginEntry(
        string filePath,
        string pluginName,
        string currentVersion,
        string website,
        string detectionStatus)
    {
        FilePath = filePath;
        _pluginName = pluginName;
        _currentVersion = currentVersion;
        _website = website;
        _detectionStatus = detectionStatus;
    }

    public string FilePath { get; }

    public string FileName => Path.GetFileName(FilePath);

    public string PluginName
    {
        get => _pluginName;
        set => SetProperty(ref _pluginName, value);
    }

    public string CurrentVersion
    {
        get => _currentVersion;
        set => SetProperty(ref _currentVersion, value);
    }

    public string Website
    {
        get => _website;
        set => SetProperty(ref _website, value);
    }

    public string DetectionStatus
    {
        get => _detectionStatus;
        set => SetProperty(ref _detectionStatus, value);
    }
}
