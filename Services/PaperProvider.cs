using PluginDownloader.Models;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PluginDownloader.Services;

public sealed class PaperProvider : IPluginProvider
{
    private const string ApiBaseUrl = "https://fill.papermc.io/v3";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public PaperProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Name => "PaperMC";

    public async Task<ProviderLookupResult?> TryGetLatestAsync(PluginEntry plugin, CancellationToken cancellationToken)
    {
        if (plugin.TargetKind != PluginTargetKind.Paper)
        {
            return null;
        }

        var minecraftVersion = ResolveMinecraftVersion(plugin);
        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            return new ProviderLookupResult(
                Name,
                "Paper",
                latestVersion: "不明",
                downloadUrl: null,
                suggestedFileName: null,
                note: "PaperのMinecraftバージョンを特定できません。");
        }

        var latestBuild = await GetLatestBuildAsync(minecraftVersion, cancellationToken);
        if (latestBuild is null)
        {
            return new ProviderLookupResult(
                Name,
                $"Paper {minecraftVersion}",
                latestVersion: "不明",
                downloadUrl: null,
                suggestedFileName: null,
                note: "Paper APIで最新ビルド情報が見つかりませんでした。");
        }

        var downloadEntry = SelectDownloadEntry(latestBuild.Downloads);
        if (downloadEntry is null)
        {
            return new ProviderLookupResult(
                Name,
                $"Paper {minecraftVersion}",
                latestVersion: $"{minecraftVersion}-{latestBuild.Id}",
                downloadUrl: null,
                suggestedFileName: null,
                note: "Paper APIのダウンロード情報を取得できませんでした。");
        }

        var file = downloadEntry.Value.Value;
        if (string.IsNullOrWhiteSpace(file.Name) || string.IsNullOrWhiteSpace(file.Url))
        {
            return new ProviderLookupResult(
                Name,
                $"Paper {minecraftVersion}",
                latestVersion: $"{minecraftVersion}-{latestBuild.Id}",
                downloadUrl: null,
                suggestedFileName: null,
                note: "Paper APIのダウンロード情報を取得できませんでした。");
        }

        return new ProviderLookupResult(
            Name,
            $"Paper {minecraftVersion}",
            $"{minecraftVersion}-{latestBuild.Id}",
            file.Url,
            file.Name,
            note: null);
    }

    private async Task<PaperV3Build?> GetLatestBuildAsync(string minecraftVersion, CancellationToken cancellationToken)
    {
        var url = $"{ApiBaseUrl}/projects/paper/versions/{Uri.EscapeDataString(minecraftVersion)}/builds/latest";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<PaperV3Build>(stream, JsonOptions, cancellationToken);
    }

    private static KeyValuePair<string, PaperV3DownloadFile>? SelectDownloadEntry(
        IReadOnlyDictionary<string, PaperV3DownloadFile>? downloads)
    {
        if (downloads is null || downloads.Count == 0)
        {
            return null;
        }

        if (downloads.TryGetValue("server:default", out var serverDefault) &&
            !string.IsNullOrWhiteSpace(serverDefault.Url))
        {
            return new KeyValuePair<string, PaperV3DownloadFile>("server:default", serverDefault);
        }

        var preferred = downloads.FirstOrDefault(static pair =>
            pair.Key.StartsWith("server:", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(pair.Value.Url));
        if (!preferred.Equals(default(KeyValuePair<string, PaperV3DownloadFile>)))
        {
            return preferred;
        }

        var any = downloads.FirstOrDefault(static pair =>
            !string.IsNullOrWhiteSpace(pair.Value.Url));
        if (!any.Equals(default(KeyValuePair<string, PaperV3DownloadFile>)))
        {
            return any;
        }

        return null;
    }

    private static string? ResolveMinecraftVersion(PluginEntry plugin)
    {
        if (!string.IsNullOrWhiteSpace(plugin.PaperMinecraftVersion))
        {
            return plugin.PaperMinecraftVersion;
        }

        var match = Regex.Match(
            plugin.CurrentVersion,
            "^(?<mc>[0-9]+\\.[0-9]+(?:\\.[0-9]+)?(?:-(?:pre|rc)[0-9]+)?)-\\d+$",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["mc"].Value : null;
    }

    private sealed class PaperV3Build
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("downloads")]
        public Dictionary<string, PaperV3DownloadFile>? Downloads { get; set; }
    }

    private sealed class PaperV3DownloadFile
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }
}
