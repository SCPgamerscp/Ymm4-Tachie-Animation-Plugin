using Ymm4BoneAnimation.Core.Serialization;

namespace Ymm4BoneAnimation.Core.Tests;

public sealed class SerializationTests
{
    [Fact]
    public void Rig_RoundTripsJson()
    {
        var rig = RigFixtures.TwoBone(out _, out _);
        var json = RigSerializer.SerializeRig(rig);
        var restored = RigSerializer.DeserializeRig(json);
        Assert.Equal(rig.Name, restored.Name);
        Assert.Equal(rig.Bones.Count, restored.Bones.Count);
        Assert.Equal(rig.Bones[1].Translation, restored.Bones[1].Translation);
    }
}
