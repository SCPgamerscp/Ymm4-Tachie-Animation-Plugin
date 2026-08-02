using System.Numerics;
using Ymm4BoneAnimation.Core.Animation;
using Ymm4BoneAnimation.Core.Model;

namespace Ymm4BoneAnimation.Core.Tests;

public sealed class AnimationClipTests
{
    [Theory]
    [InlineData(PlaybackMode.Loop, 1.25, 0.25)]
    [InlineData(PlaybackMode.PingPong, 1.25, 0.75)]
    [InlineData(PlaybackMode.Hold, 1.25, 1.0)]
    public void MapTime_ImplementsPlaybackMode(PlaybackMode mode, double input, double expected)
    {
        var clip = new AnimationClip { Name = "clip", Duration = TimeSpan.FromSeconds(1), Playback = mode };
        Assert.Equal(expected, clip.MapTime(TimeSpan.FromSeconds(input)).TotalSeconds, 6);
    }

    [Fact]
    public void Sample_InterpolatesBoneTransform()
    {
        var rig = RigFixtures.TwoBone(out var root, out _);
        var clip = new AnimationClip
        {
            Name = "move",
            Duration = TimeSpan.FromSeconds(1),
            Playback = PlaybackMode.Hold,
            Tracks =
            [
                new BoneTrack
                {
                    BoneId = root,
                    Keyframes =
                    [
                        new TransformKeyframe { Time = TimeSpan.Zero, Value = BoneTransform.Identity },
                        new TransformKeyframe { Time = TimeSpan.FromSeconds(1), Value = BoneTransform.Identity with { Translation = new Vector2(10, 0) } },
                    ],
                },
            ],
        };
        var pose = clip.Sample(rig, TimeSpan.FromSeconds(0.5));
        Assert.InRange(pose[root].Translation.X, 4.99f, 5.01f);
    }
}
