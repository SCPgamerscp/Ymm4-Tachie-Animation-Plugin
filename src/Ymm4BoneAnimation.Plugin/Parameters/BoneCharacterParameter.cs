using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Tachie;

namespace Ymm4BoneAnimation.Plugin.Parameters;

internal sealed class BoneCharacterParameter : TachieCharacterParameterBase
{
    [Display(Name = "リグフォルダー", Description = "rig.json、テクスチャ、モーションを格納したフォルダー")]
    [DirectorySelector]
    public string? DirectoryPath
    {
        get => directoryPath;
        set => Set(ref directoryPath, value);
    }
    private string? directoryPath;
}
