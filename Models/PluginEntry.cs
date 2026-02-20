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
        string detectionStatus,
        PluginTargetKind targetKind = PluginTargetKind.Plugin,
        string? paperMinecraftVersion = null,
        int? paperBuild = null)
    {
        FilePath = filePath;
        _pluginName = pluginName;
        _currentVersion = currentVersion;
        _website = website;
        _detectionStatus = detectionStatus;
        TargetKind = targetKind;
        PaperMinecraftVersion = paperMinecraftVersion;
        PaperBuild = paperBuild;
    }

    public string FilePath { get; }

    public string FileName => Path.GetFileName(FilePath);

    public PluginTargetKind TargetKind { get; }

    public string TargetKindText => TargetKind switch
    {
        PluginTargetKind.Paper => "Paper",
        _ => "Plugin"
    };

    public string? PaperMinecraftVersion { get; }

    public int? PaperBuild { get; }

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
