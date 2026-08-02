using System.Numerics;
using Ymm4TachieAnimationPlugin.Core.Model;
using Ymm4TachieAnimationPlugin.Core.Runtime;

namespace Ymm4TachieAnimationPlugin.Core.Tests;

public sealed class RuntimePoseControllerTests
{
    [Fact]
    public void ApplyCombinesExpressionLipSyncAndBlink()
    {
        var root = Guid.NewGuid();
        var mouth = Guid.NewGuid();
        var eye = Guid.NewGuid();
        var rig = new RigDefinition
        {
            Name = "runtime",
            Bones =
            [
                new BoneDefinition { Id = root, Name = "root", RetargetTag = "Root" },
                new BoneDefinition { Id = mouth, Name = "mouth", ParentId = root, RetargetTag = "Mouth" },
                new BoneDefinition { Id = eye, Name = "eye", ParentId = root, RetargetTag = "Eye_L" },
            ],
            Expressions =
            [
                new ExpressionDefinition
                {
                    Name = "happy",
                    BoneDeltas = new Dictionary<Guid, BoneTransformDelta>
                    {
                        [mouth] = new() { Translation = new Vector2(3, 4), Rotation = 0.25f },
                    },
                },
            ],
        };

        var result = new RuntimePoseController(rig).Apply(Pose.FromRestPose(rig), TimeSpan.Zero, "happy", 0.5);

        Assert.Equal(new Vector2(3, 4), result[mouth].Translation);
        Assert.Equal(0.25f, result[mouth].Rotation);
        Assert.InRange(result[mouth].Scale.Y, 0.57f, 0.58f);
        Assert.Equal(1f, result[eye].Scale.X);
        Assert.Equal(1f, result[eye].Scale.Y);
    }

    [Fact]
    public void EventsBetweenHandlesLoopBoundary()
    {
        var root = Guid.NewGuid();
        var rig = new RigDefinition
        {
            Name = "events",
            Bones = [new BoneDefinition { Id = root, Name = "root" }],
            Events =
            [
                new RuntimeEventDefinition { Name = "late", Type = RuntimeEventType.SoundEffect, Time = TimeSpan.FromSeconds(0.9) },
                new RuntimeEventDefinition { Name = "early", Type = RuntimeEventType.Particle, Time = TimeSpan.FromSeconds(0.1) },
            ],
        };
        var events = new RuntimePoseController(rig).EventsBetween(
            TimeSpan.FromSeconds(0.8),
            TimeSpan.FromSeconds(1.2),
            TimeSpan.FromSeconds(1));

        Assert.Equal(2, events.Count);
        Assert.Contains(events, x => x.Name == "late");
        Assert.Contains(events, x => x.Name == "early");
    }
}
