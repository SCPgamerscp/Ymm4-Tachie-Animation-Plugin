using Ymm4BoneAnimation.Core.Model;

namespace Ymm4BoneAnimation.Core.Animation;

public sealed record MotionTransition
{
    public required AnimationClip From { get; init; }
    public required AnimationClip To { get; init; }
    public required TimeSpan StartedAt { get; init; }
    public required TimeSpan Duration { get; init; }
    public TimeSpan TargetOffset { get; init; }
}

public static class MotionMixer
{
    public static Pose Sample(
        RigDefinition rig,
        MotionTransition transition,
        TimeSpan position,
        TimeSpan? itemLength = null)
    {
        ArgumentNullException.ThrowIfNull(rig);
        ArgumentNullException.ThrowIfNull(transition);
        if (transition.Duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(transition), "Transition duration cannot be negative.");

        var fromPose = transition.From.Sample(rig, position, itemLength);
        var targetTime = position <= transition.StartedAt
            ? transition.TargetOffset
            : transition.TargetOffset + position - transition.StartedAt;
        var toPose = transition.To.Sample(rig, targetTime, itemLength);
        if (transition.Duration == TimeSpan.Zero) return toPose;

        var linear = (float)((position - transition.StartedAt).TotalSeconds / transition.Duration.TotalSeconds);
        var amount = SmoothStep(Math.Clamp(linear, 0, 1));
        return Pose.Blend(fromPose, toPose, amount, rig.Bones.Select(x => x.Id));
    }

    private static float SmoothStep(float value) => value * value * (3 - 2 * value);
}
