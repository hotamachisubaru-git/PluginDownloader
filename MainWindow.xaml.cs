using Microsoft.Win32;
using PluginDownloader.Models;
using PluginDownloader.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace PluginDownloader;

public partial class MainWindow : Window
{
    private const string ApplicationUserAgent = "PluginDownloader/1.0";
    private static readonly JsonSerializerOptions SettingsJsonOptions = new() { WriteIndented = true };
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PluginDownloader",
        "settings.json");

    private readonly ObservableCollection<PluginEntry> _plugins = new();
    private readonly ObservableCollection<UpdateResultEntry> _results = new();
    private readonly HttpClient _httpClient;
    private readonly IReadOnlyList<IPluginProvider> _providers;
    private bool _isUpdating;

    public MainWindow()
    {
        InitializeComponent();

        _httpClient = CreateHttpClient();
        _providers = new List<IPluginProvider>
        {
            new ModrinthProvider(_httpClient),
            new SpigetProvider(_httpClient)
        };

        PluginsDataGrid.ItemsSource = _plugins;
        ResultsDataGrid.ItemsSource = _results;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        UpdateDropHint();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSettings();
        SetStatus("準備完了");
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveSettings();
        _httpClient.Dispose();
    }

    private void BrowseOutputFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectFolder(OutputDirectoryTextBox.Text.Trim());
        if (!string.IsNullOrWhiteSpace(selected))
        {
            OutputDirectoryTextBox.Text = selected;
            SaveSettings();
            SetStatus($"保存先を設定: {selected}");
        }
    }

    private void AddPluginsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "更新対象のプラグインJarを選択",
            Filter = "Jar Files (*.jar)|*.jar",
            Multiselect = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            AddPluginFiles(dialog.FileNames);
        }
    }

    private void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedFolder = SelectFolder(string.Empty);
        if (string.IsNullOrWhiteSpace(selectedFolder))
        {
            return;
        }

        var jarFiles = Directory.EnumerateFiles(selectedFolder, "*.jar", SearchOption.TopDirectoryOnly);
        AddPluginFiles(jarFiles);
    }

    private void RemoveSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = PluginsDataGrid.SelectedItems.Cast<PluginEntry>().ToList();
        if (selected.Count == 0)
        {
            return;
        }

        foreach (var item in selected)
        {
            _plugins.Remove(item);
        }

        UpdateDropHint();
        SetStatus($"{selected.Count}件を一覧から削除しました");
    }

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        _plugins.Clear();
        _results.Clear();
        ProgressBar.Value = 0;
        UpdateDropHint();
        SetStatus("一覧をクリアしました");
    }

    private async void RunUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdating)
        {
            return;
        }

        if (_plugins.Count == 0)
        {
            System.Windows.MessageBox.Show(
                this,
                "左ペインに更新対象のプラグインJarを追加してください。",
                "PluginDownloader",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var outputDirectory = OutputDirectoryTextBox.Text.Trim();
        if (PromptEveryRunCheckBox.IsChecked == true || string.IsNullOrWhiteSpace(outputDirectory))
        {
            var selected = SelectFolder(outputDirectory);
            if (string.IsNullOrWhiteSpace(selected))
            {
                SetStatus("保存先選択がキャンセルされました");
                return;
            }

            outputDirectory = selected;
            OutputDirectoryTextBox.Text = selected;
        }

        try
        {
            Directory.CreateDirectory(outputDirectory);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                $"保存先フォルダを作成できませんでした。\n{ex.Message}",
                "PluginDownloader",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        SaveSettings();
        await ExecuteUpdateAsync(outputDirectory);
    }

    private void PluginsDataGrid_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void PluginsDataGrid_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] droppedItems || droppedItems.Length == 0)
        {
            return;
        }

        var jarFiles = ExpandDroppedItems(droppedItems).ToList();
        AddPluginFiles(jarFiles);
    }

    private async Task ExecuteUpdateAsync(string outputDirectory)
    {
        _isUpdating = true;
        ToggleControls(false);

        _results.Clear();
        ProgressBar.Minimum = 0;
        ProgressBar.Maximum = _plugins.Count;
        ProgressBar.Value = 0;

        var successCount = 0;
        var latestCount = 0;
        var failCount = 0;

        for (var i = 0; i < _plugins.Count; i++)
        {
            var plugin = _plugins[i];
            plugin.DetectionStatus = "更新情報を取得中...";

            UpdateResultEntry result;
            try
            {
                result = await ProcessPluginAsync(plugin, outputDirectory, CancellationToken.None);
            }
            catch (Exception ex)
            {
                result = new UpdateResultEntry(
                    plugin.PluginName,
                    plugin.CurrentVersion,
                    "-",
                    "-",
                    "失敗",
                    string.Empty,
                    ShortMessage(ex.Message));
            }

            _results.Add(result);
            plugin.DetectionStatus = result.Status;

            switch (result.Status)
            {
                case "更新済み":
                    successCount++;
                    break;
                case "最新":
                    latestCount++;
                    break;
                default:
                    failCount++;
                    break;
            }

            ProgressBar.Value = i + 1;
            SetStatus($"{i + 1}/{_plugins.Count}: {plugin.PluginName} - {result.Status}");
        }

        ToggleControls(true);
        _isUpdating = false;

        SetStatus($"完了: 更新済み{successCount}件 / 最新{latestCount}件 / 失敗{failCount}件");
    }

    private async Task<UpdateResultEntry> ProcessPluginAsync(PluginEntry plugin, string outputDirectory, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        foreach (var provider in _providers)
        {
            ProviderLookupResult? lookup;
            try
            {
                lookup = await provider.TryGetLatestAsync(plugin, cancellationToken);
            }
            catch (Exception ex)
            {
                errors.Add($"{provider.Name}: {ShortMessage(ex.Message)}");
                continue;
            }

            if (lookup is null)
            {
                continue;
            }

            if (!lookup.CanDownload)
            {
                if (!string.IsNullOrWhiteSpace(lookup.Note))
                {
                    errors.Add($"{provider.Name}: {lookup.Note}");
                }
                continue;
            }

            if (IsSameVersion(plugin.CurrentVersion, lookup.LatestVersion))
            {
                return new UpdateResultEntry(
                    plugin.PluginName,
                    plugin.CurrentVersion,
                    lookup.LatestVersion,
                    lookup.ProviderName,
                    "最新",
                    string.Empty,
                    "すでに最新バージョンです");
            }

            try
            {
                var savedPath = await DownloadPluginFileAsync(plugin, lookup, outputDirectory, cancellationToken);
                return new UpdateResultEntry(
                    plugin.PluginName,
                    plugin.CurrentVersion,
                    lookup.LatestVersion,
                    lookup.ProviderName,
                    "更新済み",
                    savedPath,
                    lookup.ProjectDisplayName);
            }
            catch (Exception ex)
            {
                errors.Add($"{provider.Name}: {ShortMessage(ex.Message)}");
            }
        }

        var detail = errors.Count == 0
            ? "配布元が見つからないか、自動ダウンロードに対応していません"
            : string.Join(" / ", errors);

        return new UpdateResultEntry(
            plugin.PluginName,
            plugin.CurrentVersion,
            "-",
            "-",
            "失敗",
            string.Empty,
            detail);
    }

    private async Task<string> DownloadPluginFileAsync(
        PluginEntry plugin,
        ProviderLookupResult lookup,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, lookup.DownloadUrl);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (!string.IsNullOrWhiteSpace(contentType) &&
            contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("配布ページへのリダイレクトが返されたため、自動取得できません。");
        }

        var fileName = ResolveFileName(response, plugin, lookup);
        var safeFileName = EnsureJarExtension(SanitizeFileName(fileName));
        var destinationPath = BuildUniqueFilePath(outputDirectory, safeFileName);

        await using var downloadStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = File.Create(destinationPath);
        await downloadStream.CopyToAsync(fileStream, cancellationToken);

        return destinationPath;
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.Brotli |
                                     DecompressionMethods.GZip |
                                     DecompressionMethods.Deflate
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(ApplicationUserAgent);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.TryAddWithoutValidation("Spiget-User-Agent", ApplicationUserAgent);
        return client;
    }

    private void AddPluginFiles(IEnumerable<string> files)
    {
        var addedCount = 0;
        var duplicateCount = 0;
        var skippedCount = 0;

        foreach (var file in files)
        {
            if (!File.Exists(file) || !IsJarFile(file))
            {
                skippedCount++;
                continue;
            }

            if (_plugins.Any(item => string.Equals(item.FilePath, file, StringComparison.OrdinalIgnoreCase)))
            {
                duplicateCount++;
                continue;
            }

            PluginEntry entry;
            try
            {
                entry = PluginJarReader.Read(file);
            }
            catch (Exception ex)
            {
                entry = new PluginEntry(
                    file,
                    Path.GetFileNameWithoutExtension(file),
                    "不明",
                    string.Empty,
                    $"解析失敗: {ShortMessage(ex.Message)}");
            }

            _plugins.Add(entry);
            addedCount++;
        }

        UpdateDropHint();
        SetStatus($"追加: {addedCount}件 (重複{duplicateCount}, 非対応{skippedCount})");
    }

    private void LoadSettings()
    {
        AppSettings settings = new()
        {
            OutputDirectory = GetDefaultOutputDirectory(),
            PromptForOutputDirectoryEachRun = false
        };

        if (File.Exists(SettingsFilePath))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFilePath));
                if (loaded is not null)
                {
                    settings = loaded;
                }
            }
            catch
            {
                // Ignore broken settings and keep defaults.
            }
        }

        if (string.IsNullOrWhiteSpace(settings.OutputDirectory))
        {
            settings.OutputDirectory = GetDefaultOutputDirectory();
        }

        OutputDirectoryTextBox.Text = settings.OutputDirectory;
        PromptEveryRunCheckBox.IsChecked = settings.PromptForOutputDirectoryEachRun;
    }

    private void SaveSettings()
    {
        var settings = new AppSettings
        {
            OutputDirectory = OutputDirectoryTextBox.Text.Trim(),
            PromptForOutputDirectoryEachRun = PromptEveryRunCheckBox.IsChecked == true
        };

        try
        {
            var directoryPath = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var json = JsonSerializer.Serialize(settings, SettingsJsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Settings write errors should not stop the app flow.
        }
    }

    private void ToggleControls(bool isEnabled)
    {
        AddPluginsButton.IsEnabled = isEnabled;
        AddFolderButton.IsEnabled = isEnabled;
        RemoveSelectedButton.IsEnabled = isEnabled;
        ClearAllButton.IsEnabled = isEnabled;
        RunUpdateButton.IsEnabled = isEnabled;
        BrowseOutputFolderButton.IsEnabled = isEnabled;
        OutputDirectoryTextBox.IsEnabled = isEnabled;
        PromptEveryRunCheckBox.IsEnabled = isEnabled;
    }

    private void UpdateDropHint()
    {
        DropHintTextBlock.Visibility = _plugins.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void SetStatus(string message)
    {
        StatusTextBlock.Text = message;
    }

    private static string ResolveFileName(HttpResponseMessage response, PluginEntry plugin, ProviderLookupResult lookup)
    {
        var contentDisposition = response.Content.Headers.ContentDisposition;
        var headerName = contentDisposition?.FileNameStar ?? contentDisposition?.FileName;

        if (!string.IsNullOrWhiteSpace(headerName))
        {
            return headerName.Trim('"');
        }

        if (!string.IsNullOrWhiteSpace(lookup.SuggestedFileName))
        {
            return lookup.SuggestedFileName;
        }

        return $"{plugin.PluginName}-{lookup.LatestVersion}.jar";
    }

    private static string EnsureJarExtension(string fileName)
    {
        return fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : $"{fileName}.jar";
    }

    private static string BuildUniqueFilePath(string outputDirectory, string fileName)
    {
        var basePath = Path.Combine(outputDirectory, fileName);
        if (!File.Exists(basePath))
        {
            return basePath;
        }

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        for (var i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(outputDirectory, $"{fileNameWithoutExtension} ({i}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("同名ファイルが多すぎるため保存先を決定できません。");
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
        return new string(sanitized);
    }

    private static IEnumerable<string> ExpandDroppedItems(IEnumerable<string> droppedItems)
    {
        foreach (var item in droppedItems)
        {
            if (File.Exists(item))
            {
                yield return item;
                continue;
            }

            if (Directory.Exists(item))
            {
                foreach (var file in Directory.EnumerateFiles(item, "*.jar", SearchOption.TopDirectoryOnly))
                {
                    yield return file;
                }
            }
        }
    }

    private static bool IsJarFile(string path)
    {
        return string.Equals(Path.GetExtension(path), ".jar", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDefaultOutputDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloads = Path.Combine(userProfile, "Downloads");
        return Path.Combine(downloads, "PluginUpdates");
    }

    private static string? SelectFolder(string initialPath)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "保存先フォルダを選択",
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
        {
            dialog.InitialDirectory = initialPath;
        }

        return dialog.ShowDialog() == true
            ? dialog.FolderName
            : null;
    }

    private static bool IsSameVersion(string currentVersion, string latestVersion)
    {
        var normalizedCurrent = NormalizeVersion(currentVersion);
        var normalizedLatest = NormalizeVersion(latestVersion);

        if (string.IsNullOrWhiteSpace(normalizedCurrent) || string.IsNullOrWhiteSpace(normalizedLatest))
        {
            return false;
        }

        return string.Equals(normalizedCurrent, normalizedLatest, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeVersion(string version)
    {
        var normalized = version.Trim().Trim('"', '\'');
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase) && normalized.Length > 1)
        {
            normalized = normalized[1..];
        }

        return normalized;
    }

    private static string ShortMessage(string message)
    {
        const int max = 120;
        return message.Length <= max
            ? message
            : $"{message[..max]}...";
    }
}
