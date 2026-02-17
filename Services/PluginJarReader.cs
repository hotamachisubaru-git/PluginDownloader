using PluginDownloader.Models;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace PluginDownloader.Services;

public static class PluginJarReader
{
    public static PluginEntry Read(string jarFilePath)
    {
        using var archive = ZipFile.OpenRead(jarFilePath);
        var pluginYmlEntry = archive.Entries.FirstOrDefault(static entry =>
            string.Equals(entry.FullName, "plugin.yml", StringComparison.OrdinalIgnoreCase) ||
            entry.FullName.EndsWith("/plugin.yml", StringComparison.OrdinalIgnoreCase));

        if (pluginYmlEntry is null)
        {
            throw new InvalidDataException("plugin.yml が見つかりません。Bukkit系プラグインJarではない可能性があります。");
        }

        using var stream = pluginYmlEntry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var yamlText = reader.ReadToEnd();
        var metadata = ParseFlatYaml(yamlText);

        var pluginName = GetOrDefault(metadata, "name", Path.GetFileNameWithoutExtension(jarFilePath));
        var version = GetOrDefault(metadata, "version", "不明");
        var website = GetOrDefault(metadata, "website", string.Empty);

        return new PluginEntry(jarFilePath, pluginName, version, website, "解析成功");
    }

    private static Dictionary<string, string> ParseFlatYaml(string yamlText)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var reader = new StringReader(yamlText);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmedStart = line.TrimStart();
            if (trimmedStart.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith(' ') || line.StartsWith('\t'))
            {
                // Nested YAML is ignored because we only need top-level keys.
                continue;
            }

            var colonIndex = line.IndexOf(':');
            if (colonIndex <= 0)
            {
                continue;
            }

            var key = line[..colonIndex].Trim();
            var value = line[(colonIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            result[key] = NormalizeYamlValue(value);
        }

        return result;
    }

    private static string NormalizeYamlValue(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        var value = rawValue.Trim();
        if (value.Length >= 2 &&
            ((value.StartsWith('"') && value.EndsWith('"')) ||
             (value.StartsWith('\'') && value.EndsWith('\''))))
        {
            value = value[1..^1];
        }

        return value;
    }

    private static string GetOrDefault(Dictionary<string, string> metadata, string key, string fallback)
    {
        return metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;
    }
}
