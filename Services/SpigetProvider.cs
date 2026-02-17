using PluginDownloader.Models;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PluginDownloader.Services;

public sealed class SpigetProvider : IPluginProvider
{
    private const string ApiBaseUrl = "https://api.spiget.org/v2";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public SpigetProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Name => "Spiget";

    public async Task<ProviderLookupResult?> TryGetLatestAsync(PluginEntry plugin, CancellationToken cancellationToken)
    {
        if (TryExtractResourceId(plugin.Website, out var resourceId))
        {
            var byWebsite = await GetLookupByResourceIdAsync(resourceId, cancellationToken);
            if (byWebsite is not null)
            {
                return byWebsite;
            }
        }

        foreach (var query in PluginQueryBuilder.Build(plugin))
        {
            var resource = await SearchBestResourceAsync(query, plugin.PluginName, cancellationToken);
            if (resource is null)
            {
                continue;
            }

            var lookup = await CreateLookupFromResourceAsync(resource, cancellationToken);
            if (lookup is not null)
            {
                return lookup;
            }
        }

        return null;
    }

    private async Task<SpigetResource?> SearchBestResourceAsync(
        string query,
        string targetPluginName,
        CancellationToken cancellationToken)
    {
        var requestUrl = $"{ApiBaseUrl}/search/resources/{Uri.EscapeDataString(query)}?size=20";
        using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var resources = await JsonSerializer.DeserializeAsync<List<SpigetResource>>(stream, JsonOptions, cancellationToken);
        if (resources is null || resources.Count == 0)
        {
            return null;
        }

        var best = resources
            .Where(static resource => resource.ExistenceStatus == 1)
            .Select(resource => new
            {
                Resource = resource,
                Score = NameMatcher.Score(targetPluginName, resource.Name, resource.Tag)
            })
            .OrderByDescending(static item => item.Score)
            .FirstOrDefault();

        if (best is null || best.Score < 50)
        {
            return null;
        }

        return best.Resource;
    }

    private async Task<ProviderLookupResult?> GetLookupByResourceIdAsync(int resourceId, CancellationToken cancellationToken)
    {
        var requestUrl = $"{ApiBaseUrl}/resources/{resourceId}";
        using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var resource = await JsonSerializer.DeserializeAsync<SpigetResource>(stream, JsonOptions, cancellationToken);
        if (resource is null)
        {
            return null;
        }

        return await CreateLookupFromResourceAsync(resource, cancellationToken);
    }

    private async Task<ProviderLookupResult?> CreateLookupFromResourceAsync(
        SpigetResource resource,
        CancellationToken cancellationToken)
    {
        if (resource.Premium)
        {
            return new ProviderLookupResult(
                Name,
                resource.Name,
                latestVersion: "不明",
                downloadUrl: null,
                suggestedFileName: null,
                note: "有料プラグインはAPI経由で自動ダウンロードできません。");
        }

        var latestVersion = await GetLatestVersionAsync(resource.Id, cancellationToken);
        if (latestVersion is null)
        {
            return null;
        }

        if (resource.External)
        {
            var externalUrl = resource.File?.ExternalUrl;
            if (!string.IsNullOrWhiteSpace(externalUrl) &&
                externalUrl.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
            {
                return new ProviderLookupResult(
                    Name,
                    resource.Name,
                    latestVersion.Name,
                    externalUrl,
                    $"{resource.Name}-{latestVersion.Name}.jar",
                    note: "外部配布URLから取得");
            }

            return new ProviderLookupResult(
                Name,
                resource.Name,
                latestVersion.Name,
                downloadUrl: null,
                suggestedFileName: null,
                note: "外部配布のため自動ダウンロードに対応していません。");
        }

        return new ProviderLookupResult(
            Name,
            resource.Name,
            latestVersion.Name,
            $"{ApiBaseUrl}/resources/{resource.Id}/download",
            $"{resource.Name}-{latestVersion.Name}.jar",
            note: null);
    }

    private async Task<SpigetVersion?> GetLatestVersionAsync(int resourceId, CancellationToken cancellationToken)
    {
        var requestUrl = $"{ApiBaseUrl}/resources/{resourceId}/versions/latest";
        using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<SpigetVersion>(stream, JsonOptions, cancellationToken);
    }

    private static bool TryExtractResourceId(string website, out int resourceId)
    {
        resourceId = 0;
        if (string.IsNullOrWhiteSpace(website) ||
            !Uri.TryCreate(website, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.Host.Contains("spigotmc.org", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = uri.AbsolutePath;
        var match = Regex.Match(path, "resources/[^/]*\\.(\\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out resourceId))
        {
            return true;
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var resourceIndex = Array.FindIndex(segments, segment =>
            segment.Equals("resources", StringComparison.OrdinalIgnoreCase));

        if (resourceIndex >= 0 && resourceIndex + 1 < segments.Length)
        {
            var digits = Regex.Match(segments[resourceIndex + 1], "\\d+");
            if (digits.Success && int.TryParse(digits.Value, out resourceId))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class SpigetResource
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("tag")]
        public string Tag { get; set; } = string.Empty;

        [JsonPropertyName("external")]
        public bool External { get; set; }

        [JsonPropertyName("premium")]
        public bool Premium { get; set; }

        [JsonPropertyName("existenceStatus")]
        public int ExistenceStatus { get; set; }

        [JsonPropertyName("file")]
        public SpigetResourceFile? File { get; set; }
    }

    private sealed class SpigetResourceFile
    {
        [JsonPropertyName("externalUrl")]
        public string? ExternalUrl { get; set; }
    }

    private sealed class SpigetVersion
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}
