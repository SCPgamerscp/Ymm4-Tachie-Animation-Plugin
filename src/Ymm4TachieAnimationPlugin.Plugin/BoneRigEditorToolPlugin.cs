using YukkuriMovieMaker.Plugin;
using Ymm4TachieAnimationPlugin.Editor.ViewModels;
using Ymm4TachieAnimationPlugin.Editor.Views;

namespace Ymm4TachieAnimationPlugin.Plugin;

public sealed class BoneRigEditorToolPlugin : IToolPlugin
{
    public string Name => "2Dボーンリグエディター";
    public Type ViewModelType => typeof(RigEditorViewModel);
    public Type ViewType => typeof(RigEditorWindow);
}
