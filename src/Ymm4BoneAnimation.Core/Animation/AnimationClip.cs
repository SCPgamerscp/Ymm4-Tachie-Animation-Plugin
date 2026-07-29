using System.Numerics;
using Ymm4BoneAnimation.Core.Model;

namespace Ymm4BoneAnimation.Core.Animation;

public enum PlaybackMode
{
    Loop,
    PingPong,
    Hold,
    Stretch,
}

public readonly record struct BezierEasing(Vector2 InHandle, Vector2 OutHandle)
{
    public static BezierEasing Linear => new(new(0, 0), new(1, 1));

    public float Evaluate(float time)
    {
        time = Math.Clamp(time, 0, 1);
        var low = 0f;
        var high = 1f;
        var parameter = time;
        for (var i = 0; i < 12; i++)
        {
            parameter = (low + high) * 0.5f;
            var x = Cubic(parameter, 0, OutHandle.X, InHandle.X, 1);
            if (x < time) low = parameter; else high = parameter;
        }
        return Cubic(parameter, 0, OutHandle.Y, InHandle.Y, 1);
    }

    private static float Cubic(float t, float p0, float p1, float p2, float p3)
    {
        var u = 1 - t;
        return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
    }
}

public sealed record TransformKeyframe
{
    public required TimeSpan Time { get; init; }
    public required BoneTransform Value { get; init; }
    public BezierEasing Easing { get; init; } = BezierEasing.Linear;
}

public sealed record BoneTrack
{
    public required Guid BoneId { get; init; }
    public IReadOnlyList<TransformKeyframe> Keyframes { get; init; } = [];

    public BoneTransform Sample(TimeSpan time, BoneTransform fallback)
    {
        if (Keyframes.Count == 0) return fallback;
        if (time <= Keyframes[0].Time) return Keyframes[0].Value;
        if (time >= Keyframes[^1].Time) return Keyframes[^1].Value;

        var right = 1;
        while (right < Keyframes.Count && Keyframes[right].Time < time) right++;
        var from = Keyframes[right - 1];
        var to = Keyframes[right];
        var duration = (to.Time - from.Time).TotalSeconds;
        var linear = duration <= 0 ? 1f : (float)((time - from.Time).TotalSeconds / duration);
        return BoneTransform.Lerp(from.Value, to.Value, from.Easing.Evaluate(linear));
    }
}

public sealed record AnimationClip
{
    public required string Name { get; init; }
    public required TimeSpan Duration { get; init; }
    public PlaybackMode Playback { get; init; } = PlaybackMode.Loop;
    public IReadOnlyList<BoneTrack> Tracks { get; init; } = [];

    public TimeSpan MapTime(TimeSpan position, TimeSpan? itemLength = null)
    {
        if (Duration <= TimeSpan.Zero) return TimeSpan.Zero;
        var seconds = Math.Max(0, position.TotalSeconds);
        var duration = Duration.TotalSeconds;
        return Playback switch
        {
            PlaybackMode.Loop => TimeSpan.FromSeconds(seconds % duration),
            PlaybackMode.PingPong => TimeSpan.FromSeconds(PingPong(seconds, duration)),
            PlaybackMode.Hold => TimeSpan.FromSeconds(Math.Min(seconds, duration)),
            PlaybackMode.Stretch when itemLength is { } length && length > TimeSpan.Zero =>
                TimeSpan.FromSeconds(Math.Min(seconds / length.TotalSeconds, 1) * duration),
            _ => TimeSpan.FromSeconds(Math.Min(seconds, duration)),
        };
    }

    public Pose Sample(RigDefinition rig, TimeSpan position, TimeSpan? itemLength = null)
    {
        var pose = Pose.FromRestPose(rig);
        var time = MapTime(position, itemLength);
        foreach (var track in Tracks)
            pose[track.BoneId] = track.Sample(time, pose[track.BoneId]);
        return pose;
    }

    private static double PingPong(double value, double length)
    {
        var cycle = value % (length * 2);
        return cycle <= length ? cycle : length * 2 - cycle;
    }
}
