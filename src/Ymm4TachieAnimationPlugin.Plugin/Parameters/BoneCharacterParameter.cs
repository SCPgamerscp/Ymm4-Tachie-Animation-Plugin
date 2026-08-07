using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Tachie;
using YukkuriMovieMaker.Settings;

namespace Ymm4TachieAnimationPlugin.Plugin.Parameters;

internal sealed class BoneCharacterParameter : TachieCharacterParameterBase
{
    [Display(Name = "リグフォルダー", Description = "rig.json、テクスチャ、モーションを格納したフォルダー")]
    [DirectorySelectorWithRigEditor]
    public string? DirectoryPath
    {
        get => directoryPath;
        set => Set(ref directoryPath, value);
    }
    private string? directoryPath;

    [Display(Name = "PSDファイル", Description = "PSDファイルを直接指定する場合はこちら。指定した場合はリグフォルダーより優先され、自動でリグに変換されます。")]
    [FileSelector(FileGroupType.TachieParts, CustomFilterName = "PSDファイル", CustomFilterValue = "*.psd;*.psb")]
    public string? PsdFilePath
    {
        get => psdFilePath;
        set => Set(ref psdFilePath, value);
    }
    private string? psdFilePath;
}
