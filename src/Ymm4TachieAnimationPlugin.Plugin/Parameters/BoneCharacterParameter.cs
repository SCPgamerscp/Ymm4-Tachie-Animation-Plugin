using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Tachie;
using YukkuriMovieMaker.Settings;

namespace Ymm4TachieAnimationPlugin.Plugin.Parameters;

internal sealed class BoneCharacterParameter : TachieCharacterParameterBase
{
    [Display(Name = "\u30EA\u30B0\u30D5\u30A9\u30EB\u30C0\u30FC", Description = "rig.json\u3001\u30C6\u30AF\u30B9\u30C1\u30E3\u3001\u30E2\u30FC\u30B7\u30E7\u30F3\u3092\u683C\u7D0D\u3057\u305F\u30D5\u30A9\u30EB\u30C0\u30FC")]
    [DirectorySelector]
    public string? DirectoryPath
    {
        get => directoryPath;
        set => Set(ref directoryPath, value);
    }
    private string? directoryPath;

    [Display(Name = "PSD\u30D5\u30A1\u30A4\u30EB", Description = "PSD\u30D5\u30A1\u30A4\u30EB\u3092\u76F4\u63A5\u6307\u5B9A\u3059\u308B\u5834\u5408\u306F\u3053\u3061\u3089\u3002\u6307\u5B9A\u3057\u305F\u5834\u5408\u306F\u30EA\u30B0\u30D5\u30A9\u30EB\u30C0\u30FC\u3088\u308A\u512A\u5148\u3055\u308C\u3001\u81EA\u52D5\u3067\u30EA\u30B0\u306B\u5909\u63DB\u3055\u308C\u307E\u3059\u3002")]
    [FileSelector(FileGroupType.TachieParts, CustomFilterName = "PSD\u30D5\u30A1\u30A4\u30EB", CustomFilterValue = "*.psd;*.psb")]
    public string? PsdFilePath
    {
        get => psdFilePath;
        set => Set(ref psdFilePath, value);
    }
    private string? psdFilePath;
}
