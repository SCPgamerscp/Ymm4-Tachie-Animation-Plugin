using System.Numerics;
using Ymm4BoneAnimation.Core.Animation;
using Ymm4BoneAnimation.Core.Model;

namespace Ymm4BoneAnimation.Core.Tests;

public sealed class MotionEditingTests
{
    [Fact]
    public void MotionMixer_BlendsClipsAtTransitionMidpoint()
    {
        var rig = RigFixtures.TwoBone(out var root, out _);
        var from = ConstantClip(root, 0);
        var to = ConstantClip(root, 10);
        var transition = new MotionTransition
        {
            From = from,
            To = to,
            StartedAt = TimeSpan.Zero,
            Duration = TimeSpan.FromSeconds(1),
        };
        var pose = MotionMixer.Sample(rig, transition, TimeSpan.FromSeconds(0.5));
        Assert.InRange(pose[root].Translation.X, 4.99f, 5.01f);
    }

    [Fact]
    public void SetKeyframe_RespectsAutoKeyToggleAndSortsKeys()
    {
        var bone = Guid.NewGuid();
        var clip = new AnimationClip { Name = "edit", Duration = TimeSpan.Zero };
        var disabled = AnimationTimelineEditor.SetKeyframe(clip, bone, TimeSpan.Zero, BoneTransform.Identity, autoKeyEnabled: false);
        Assert.Same(clip, disabled);

        var later = AnimationTimelineEditor.SetKeyframe(clip, bone, TimeSpan.FromSeconds(1), BoneTransform.Identity);
        var earlier = AnimationTimelineEditor.SetKeyframe(later, bone, TimeSpan.FromSeconds(0.25), BoneTransform.Identity);
        Assert.Equal(TimeSpan.FromSeconds(0.25), earlier.Tracks[0].Keyframes[0].Time);
        Assert.Equal(TimeSpan.FromSeconds(1), earlier.Duration);
    }

    [Fact]
    public void PastePose_MirrorsToMappedBone()
    {
        var left = Guid.NewGuid();
        var right = Guid.NewGuid();
        var value = BoneTransform.Identity with { Translation = new Vector2(3, 2), Rotation = 0.25f };
        var clip = AnimationTimelineEditor.PastePose(
            new AnimationClip { Name = "mirror", Duration = TimeSpan.Zero },
            TimeSpan.Zero,
            new Dictionary<Guid, BoneTransform> { [left] = value },
            new Dictionary<Guid, Guid> { [left] = right });
        Assert.Equal(right, clip.Tracks.Single().BoneId);
        Assert.Equal(-3, clip.Tracks.Single().Keyframes.Single().Value.Translation.X);
    }

    private static AnimationClip ConstantClip(Guid bone, float x) => new()
    {
        Name = $"x{x}",
        Duration = TimeSpan.FromSeconds(1),
        Playback = PlaybackMode.Hold,
        Tracks =
        [
            new BoneTrack
            {
                BoneId = bone,
                Keyframes = [new TransformKeyframe { Time = TimeSpan.Zero, Value = BoneTransform.Identity with { Translation = new Vector2(x, 0) } }],
            },
        ],
    };
}
