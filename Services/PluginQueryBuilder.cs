using PluginDownloader.Models;
using System.IO;
using System.Text.RegularExpressions;

namespace PluginDownloader.Services;

public static class PluginQueryBuilder
{
    public static IReadOnlyList<string> Build(PluginEntry plugin)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddCandidate(candidates, plugin.PluginName);

        var fileStem = Path.GetFileNameWithoutExtension(plugin.FileName);
        AddCandidate(candidates, fileStem);

        var withoutVersion = Regex.Replace(
            fileStem,
            "[-_ ]?v?\\d+([._-]\\d+)*.*$",
            string.Empty,
            RegexOptions.IgnoreCase);
        AddCandidate(candidates, withoutVersion);

        return candidates.ToList();
    }

    private static void AddCandidate(HashSet<string> set, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmed = value.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            set.Add(trimmed);
        }
    }
}
