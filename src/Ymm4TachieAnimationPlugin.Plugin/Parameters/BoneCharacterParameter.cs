using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Tachie;

namespace Ymm4TachieAnimationPlugin.Plugin.Parameters;

internal sealed class BoneCharacterParameter : TachieCharacterParameterBase
{
    [Display(Name = "リグフォルダー (またはPSD)", Description = "rig.json、テクスチャが入ったフォルダー、またはPSDファイルのパス")]
    [DirectorySelector]
    public string? DirectoryPath
    {
        get => directoryPath;
        set => Set(ref directoryPath, value);
    }
    private string? directoryPath;
}
