using PluginDownloader.Models;
using System.IO;
using System.Text.RegularExpressions;

namespace PluginDownloader.Services;

public static class PaperJarReader
{
    private static readonly Regex PaperJarNameRegex = new(
        "^paper-(?<mc>[0-9]+\\.[0-9]+(?:\\.[0-9]+)?(?:-(?:pre|rc)[0-9]+)?)-(?<build>[0-9]+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static PluginEntry Read(string jarFilePath)
    {
        var stem = Path.GetFileNameWithoutExtension(jarFilePath);
        var match = PaperJarNameRegex.Match(stem);

        if (!match.Success)
        {
            throw new InvalidDataException(
                "Paper公式Jar名 (paper-<mcVersion>-<build>.jar) として認識できません。");
        }

        var minecraftVersion = match.Groups["mc"].Value;
        if (!int.TryParse(match.Groups["build"].Value, out var build))
        {
            throw new InvalidDataException("Paperのビルド番号を読み取れませんでした。");
        }

        return new PluginEntry(
            jarFilePath,
            "Paper",
            $"{minecraftVersion}-{build}",
            "https://papermc.io",
            "解析成功",
            PluginTargetKind.Paper,
            minecraftVersion,
            build);
    }
}
