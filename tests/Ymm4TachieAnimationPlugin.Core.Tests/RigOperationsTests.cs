using Ymm4TachieAnimationPlugin.Core.Editing;
using Ymm4TachieAnimationPlugin.Core.Model;

namespace Ymm4TachieAnimationPlugin.Core.Tests;

public sealed class RigOperationsTests
{
    [Fact]
    public void RemoveBoneSubtree_RemovesDependentParts()
    {
        var rig = RigFixtures.TwoBone(out var root, out var child);
        var rootPart = RiggingTests.CreateTriangle(root, TextureFiltering.Bilinear);
        var childPart = RiggingTests.CreateTriangle(child, TextureFiltering.Bilinear);
        rig = rig with { Parts = [rootPart, childPart] };

        var updated = RigOperations.RemoveBoneSubtree(rig, child);
        updated.Validate();
        Assert.Single(updated.Bones);
        Assert.Single(updated.Parts);
        Assert.Equal(rootPart.Id, updated.Parts[0].Id);
    }
}
