using System.Numerics;
using Ymm4BoneAnimation.Core.Model;
using Ymm4BoneAnimation.Core.Runtime;

namespace Ymm4BoneAnimation.Core.Tests;

public sealed class RuntimeTests
{
    [Fact]
    public void Evaluator_PropagatesParentTransform()
    {
        var rig = RigFixtures.TwoBone(out var root, out var child);
        var pose = Pose.FromRestPose(rig);
        pose[root] = pose[root] with { Rotation = MathF.PI / 2 };
        var globals = new RigEvaluator(rig).EvaluateGlobals(pose);
        Assert.InRange(globals[child].Translation.X, -0.001f, 0.001f);
        Assert.InRange(globals[child].Translation.Y, 9.999f, 10.001f);
    }

    [Fact]
    public void CcdIkSolver_ReachesTarget()
    {
        var rig = RigFixtures.TwoBone(out _, out var child);
        var pose = Pose.FromRestPose(rig);
        var distance = new CcdIkSolver(rig).Solve(pose, child, new Vector2(10, 10), iterations: 32, tolerance: 0.05f);
        Assert.InRange(distance, 0, 0.1f);
    }
}
