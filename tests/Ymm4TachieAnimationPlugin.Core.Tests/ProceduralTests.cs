using System.Numerics;
using Ymm4TachieAnimationPlugin.Core.Procedural;

namespace Ymm4TachieAnimationPlugin.Core.Tests;

public sealed class ProceduralTests
{
    [Fact]
    public void MultipedGait_OffsetsLegPhases()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var targets = MultipedGait.CalculateTargets(
            [new(first, Vector2.Zero, 0), new(second, Vector2.Zero, 0.5f)],
            TimeSpan.Zero,
            Vector2.UnitX,
            new GaitSettings());
        Assert.NotEqual(targets[first], targets[second]);
    }

    [Fact]
    public void VerletChain_PreservesSegmentLengths()
    {
        var chain = new VerletChain([Vector2.Zero, new Vector2(10, 0), new Vector2(20, 0)]);
        for (var i = 0; i < 20; i++)
            chain.Step(Vector2.Zero, 1f / 60, new Vector2(0, 100));
        Assert.InRange(Vector2.Distance(chain.Positions[0], chain.Positions[1]), 9.9f, 10.1f);
        Assert.InRange(Vector2.Distance(chain.Positions[1], chain.Positions[2]), 9.9f, 10.1f);
    }
}
