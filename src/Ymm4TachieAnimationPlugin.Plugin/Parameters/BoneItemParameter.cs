using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Tachie;

namespace Ymm4TachieAnimationPlugin.Plugin.Parameters;

internal sealed class BoneItemParameter : TachieItemParameterBase
{
    // MotionName is intentionally serializable now; a visual preset editor is planned for the editor phase.
    public string MotionName
    {
        get => motionName;
        set => Set(ref motionName, value);
    }
    private string motionName = "idle";

    public double PlaybackSpeed
    {
        get => playbackSpeed;
        set => Set(ref playbackSpeed, Math.Clamp(value, 0.01, 100));
    }
    private double playbackSpeed = 1;

    protected override IEnumerable<IAnimatable> GetAnimatables() => [];
}
