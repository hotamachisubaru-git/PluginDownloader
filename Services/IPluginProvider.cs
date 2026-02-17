using PluginDownloader.Models;

namespace PluginDownloader.Services;

public interface IPluginProvider
{
    string Name { get; }

    Task<ProviderLookupResult?> TryGetLatestAsync(PluginEntry plugin, CancellationToken cancellationToken);
}
