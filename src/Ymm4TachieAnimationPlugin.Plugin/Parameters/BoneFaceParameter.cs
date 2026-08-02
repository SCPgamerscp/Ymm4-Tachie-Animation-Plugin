using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Tachie;

namespace Ymm4TachieAnimationPlugin.Plugin.Parameters;

internal sealed class BoneFaceParameter : TachieFaceParameterBase
{
    public string Expression
    {
        get => expression;
        set => Set(ref expression, value);
    }
    private string expression = "default";

    protected override IEnumerable<IAnimatable> GetAnimatables() => [];
}
