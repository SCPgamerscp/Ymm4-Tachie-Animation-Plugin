using Ymm4TachieAnimationPlugin.Core.Model;

namespace Ymm4TachieAnimationPlugin.Core.Tests;

public sealed class RigDefinitionTests
{
    [Fact]
    public void Validate_AcceptsValidHierarchy()
    {
        var rig = RigFixtures.TwoBone(out _, out _);
        rig.Validate();
    }

    [Fact]
    public void Validate_RejectsCycle()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var rig = new RigDefinition
        {
            Name = "cycle",
            Bones =
            [
                new BoneDefinition { Id = first, Name = "a", ParentId = second },
                new BoneDefinition { Id = second, Name = "b", ParentId = first },
            ],
        };
        Assert.Throws<RigValidationException>(rig.Validate);
    }
}
