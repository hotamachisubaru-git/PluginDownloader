using PluginDownloader.Models;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PluginDownloader.Services;

public sealed class ModrinthProvider : IPluginProvider
{
    private const string ApiBaseUrl = "https://api.modrinth.com/v2";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public ModrinthProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Name => "Modrinth";

    public async Task<ProviderLookupResult?> TryGetLatestAsync(PluginEntry plugin, CancellationToken cancellationToken)
    {
        if (plugin.TargetKind != PluginTargetKind.Plugin)
        {
            return null;
        }

        if (TryExtractProjectIdOrSlug(plugin.Website, out var websiteProject))
        {
            var websiteLookup = await GetLatestVersionForProjectAsync(
                websiteProject,
                websiteProject,
                cancellationToken);

            if (websiteLookup is not null)
            {
                return websiteLookup;
            }
        }

        var queryCandidates = PluginQueryBuilder.Build(plugin);
        var checkedProjectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var query in queryCandidates)
        {
            var project = await SearchBestProjectAsync(query, plugin.PluginName, cancellationToken);
            if (project is null || !checkedProjectIds.Add(project.ProjectId))
            {
                continue;
            }

            var lookup = await GetLatestVersionForProjectAsync(
                project.ProjectId,
                project.Title,
                cancellationToken);

            if (lookup is not null)
            {
                return lookup;
            }
        }

        return null;
    }

    private async Task<ModrinthSearchHit?> SearchBestProjectAsync(
        string query,
        string targetPluginName,
        CancellationToken cancellationToken)
    {
        var facets = Uri.EscapeDataString(
            "[[\"categories:paper\",\"categories:spigot\",\"categories:bukkit\",\"categories:purpur\",\"categories:folia\"]]");
        var requestUrl =
            $"{ApiBaseUrl}/search?query={Uri.EscapeDataString(query)}&limit=20&index=relevance&facets={facets}";

        using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<ModrinthSearchResponse>(stream, JsonOptions, cancellationToken);
        if (payload?.Hits is null || payload.Hits.Count == 0)
        {
            return null;
        }

        var bestMatch = payload.Hits
            .Select(hit => new
            {
                Hit = hit,
                Score = NameMatcher.Score(targetPluginName, hit.Title, hit.Slug) +
                        GetCategoryBonus(hit.Categories)
            })
            .OrderByDescending(static item => item.Score)
            .FirstOrDefault();

        if (bestMatch is null || bestMatch.Score < 50)
        {
            return null;
        }

        return bestMatch.Hit;
    }

    private async Task<ProviderLookupResult?> GetLatestVersionForProjectAsync(
        string projectIdOrSlug,
        string displayName,
        CancellationToken cancellationToken)
    {
        var loaders = Uri.EscapeDataString("[\"paper\",\"spigot\",\"bukkit\",\"purpur\",\"folia\"]");
        var requestUrl = $"{ApiBaseUrl}/project/{Uri.EscapeDataString(projectIdOrSlug)}/version?loaders={loaders}";

        using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var versions = await JsonSerializer.DeserializeAsync<List<ModrinthVersion>>(stream, JsonOptions, cancellationToken);
        if (versions is null || versions.Count == 0)
        {
            return null;
        }

        var latestVersion = versions
            .OrderByDescending(static version => version.DatePublished)
            .FirstOrDefault(static version => version.Files is { Count: > 0 });
        if (latestVersion is null)
        {
            return null;
        }

        var file = latestVersion.Files.FirstOrDefault(static candidate => candidate.Primary) ??
                   latestVersion.Files.FirstOrDefault(static candidate =>
                       !string.IsNullOrWhiteSpace(candidate.FileName) &&
                       candidate.FileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)) ??
                   latestVersion.Files.FirstOrDefault();

        if (file is null || string.IsNullOrWhiteSpace(file.Url))
        {
            return null;
        }

        return new ProviderLookupResult(
            Name,
            displayName,
            latestVersion.VersionNumber,
            file.Url,
            file.FileName,
            note: null);
    }

    private static bool TryExtractProjectIdOrSlug(string website, out string projectIdOrSlug)
    {
        projectIdOrSlug = string.Empty;
        if (string.IsNullOrWhiteSpace(website) ||
            !Uri.TryCreate(website, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.Host.Contains("modrinth.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        if (segments.Length >= 2 &&
            (segments[0].Equals("plugin", StringComparison.OrdinalIgnoreCase) ||
             segments[0].Equals("mod", StringComparison.OrdinalIgnoreCase) ||
             segments[0].Equals("project", StringComparison.OrdinalIgnoreCase)))
        {
            projectIdOrSlug = segments[1];
            return !string.IsNullOrWhiteSpace(projectIdOrSlug);
        }

        projectIdOrSlug = segments[^1];
        return !string.IsNullOrWhiteSpace(projectIdOrSlug);
    }

    private static int GetCategoryBonus(IReadOnlyList<string>? categories)
    {
        if (categories is null || categories.Count == 0)
        {
            return 0;
        }

        var pluginCategories = new[]
        {
            "paper",
            "spigot",
            "bukkit",
            "purpur",
            "folia"
        };

        return pluginCategories.Count(category =>
            categories.Contains(category, StringComparer.OrdinalIgnoreCase)) * 8;
    }

    private sealed class ModrinthSearchResponse
    {
        [JsonPropertyName("hits")]
        public List<ModrinthSearchHit> Hits { get; set; } = [];
    }

    private sealed class ModrinthSearchHit
    {
        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; } = string.Empty;

        [JsonPropertyName("slug")]
        public string Slug { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("categories")]
        public List<string>? Categories { get; set; }
    }

    private sealed class ModrinthVersion
    {
        [JsonPropertyName("version_number")]
        public string VersionNumber { get; set; } = string.Empty;

        [JsonPropertyName("date_published")]
        public DateTimeOffset DatePublished { get; set; }

        [JsonPropertyName("files")]
        public List<ModrinthVersionFile> Files { get; set; } = [];
    }

    private sealed class ModrinthVersionFile
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("filename")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("primary")]
        public bool Primary { get; set; }
    }
}
