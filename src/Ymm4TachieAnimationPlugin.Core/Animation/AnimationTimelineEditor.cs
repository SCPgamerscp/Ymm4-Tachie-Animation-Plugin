using Ymm4TachieAnimationPlugin.Core.Model;
using Ymm4TachieAnimationPlugin.Core.Rigging;

namespace Ymm4TachieAnimationPlugin.Core.Animation;

public static class AnimationTimelineEditor
{
    private static readonly TimeSpan TimeTolerance = TimeSpan.FromTicks(1);

    public static AnimationClip SetKeyframe(
        AnimationClip clip,
        Guid boneId,
        TimeSpan time,
        BoneTransform value,
        BezierEasing? easing = null,
        bool autoKeyEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (!autoKeyEnabled) return clip;
        if (time < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(time));

        var tracks = clip.Tracks.ToList();
        var trackIndex = tracks.FindIndex(x => x.BoneId == boneId);
        var existing = trackIndex >= 0 ? tracks[trackIndex] : new BoneTrack { BoneId = boneId };
        var keys = existing.Keyframes.Where(x => (x.Time - time).Duration() > TimeTolerance).ToList();
        keys.Add(new TransformKeyframe { Time = time, Value = value, Easing = easing ?? BezierEasing.Linear });
        var updated = existing with { Keyframes = keys.OrderBy(x => x.Time).ToArray() };
        if (trackIndex >= 0) tracks[trackIndex] = updated; else tracks.Add(updated);
        return clip with { Duration = MaxDuration(clip.Duration, time), Tracks = tracks };
    }

    public static AnimationClip DeleteKeyframe(AnimationClip clip, Guid boneId, TimeSpan time)
    {
        var tracks = clip.Tracks.Select(track => track.BoneId != boneId
            ? track
            : track with { Keyframes = track.Keyframes.Where(x => (x.Time - time).Duration() > TimeTolerance).ToArray() })
            .Where(track => track.Keyframes.Count > 0)
            .ToArray();
        return clip with { Tracks = tracks };
    }

    public static AnimationClip MoveKeyframe(AnimationClip clip, Guid boneId, TimeSpan from, TimeSpan to)
    {
        if (to < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(to));
        var track = clip.Tracks.FirstOrDefault(x => x.BoneId == boneId);
        var key = track?.Keyframes.FirstOrDefault(x => (x.Time - from).Duration() <= TimeTolerance)
            ?? throw new InvalidOperationException("The keyframe to move was not found.");
        return SetKeyframe(DeleteKeyframe(clip, boneId, from), boneId, to, key.Value, key.Easing);
    }

    public static AnimationClip PastePose(
        AnimationClip clip,
        TimeSpan time,
        IReadOnlyDictionary<Guid, BoneTransform> copiedPose,
        IReadOnlyDictionary<Guid, Guid>? mirrorMap = null,
        MirrorAxis mirrorAxis = MirrorAxis.Horizontal)
    {
        ArgumentNullException.ThrowIfNull(copiedPose);
        var result = clip;
        foreach (var (sourceId, sourceTransform) in copiedPose)
        {
            var targetId = mirrorMap is not null && mirrorMap.TryGetValue(sourceId, out var mapped) ? mapped : sourceId;
            var value = mirrorMap is null ? sourceTransform : Mirror(sourceTransform, mirrorAxis);
            result = SetKeyframe(result, targetId, time, value);
        }
        return result;
    }

    private static BoneTransform Mirror(BoneTransform value, MirrorAxis axis) => axis switch
    {
        MirrorAxis.Horizontal => value with
        {
            Translation = new(-value.Translation.X, value.Translation.Y),
            Rotation = MathF.IEEERemainder(MathF.PI - value.Rotation, MathF.Tau),
        },
        MirrorAxis.Vertical => value with
        {
            Translation = new(value.Translation.X, -value.Translation.Y),
            Rotation = -value.Rotation,
        },
        _ => value,
    };

    private static TimeSpan MaxDuration(TimeSpan left, TimeSpan right) => left >= right ? left : right;
}
