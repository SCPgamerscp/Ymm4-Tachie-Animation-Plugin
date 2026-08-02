using System.Numerics;
using Ymm4TachieAnimationPlugin.Core.Model;

namespace Ymm4TachieAnimationPlugin.Core.Tests;

internal static class RigFixtures
{
    public static RigDefinition TwoBone(out Guid rootId, out Guid childId)
    {
        rootId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        childId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        return new RigDefinition
        {
            Name = "test",
            Bones =
            [
                new BoneDefinition { Id = rootId, Name = "root", RetargetTag = "Root", Length = 10 },
                new BoneDefinition { Id = childId, Name = "child", ParentId = rootId, RetargetTag = "Arm_R1", Translation = new Vector2(10, 0), Length = 10 },
            ],
        };
    }
}
