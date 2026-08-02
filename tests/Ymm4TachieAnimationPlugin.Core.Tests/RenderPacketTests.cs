using Ymm4TachieAnimationPlugin.Core.Model;
using Ymm4TachieAnimationPlugin.Core.Rendering;

namespace Ymm4TachieAnimationPlugin.Core.Tests;

public sealed class RenderPacketTests
{
    [Fact]
    public void Build_SnapsPixelPerfectVerticesAndKeepsIndices()
    {
        var baseRig = RigFixtures.TwoBone(out var root, out _);
        var part = RiggingTests.CreateTriangle(root, TextureFiltering.PixelPerfect);
        var rig = baseRig with { Parts = [part] };
        var packet = new MeshRenderPacketBuilder(rig).Build(Pose.FromRestPose(rig)).Single();
        Assert.Equal(TextureFiltering.PixelPerfect, packet.Filtering);
        Assert.Equal([0, 1, 2], packet.TriangleIndices);
        Assert.All(packet.Vertices, vertex =>
        {
            Assert.Equal(MathF.Round(vertex.Position.X), vertex.Position.X);
            Assert.Equal(MathF.Round(vertex.Position.Y), vertex.Position.Y);
        });
    }

    [Fact]
    public void Build_AppliesAnimatedBoneZOrder()
    {
        var baseRig = RigFixtures.TwoBone(out var root, out _);
        var part = RiggingTests.CreateTriangle(root, TextureFiltering.Bicubic);
        var rig = baseRig with { Parts = [part] };
        var pose = Pose.FromRestPose(rig);
        pose[root] = pose[root] with { ZOrder = 7 };
        var packet = new MeshRenderPacketBuilder(rig).Build(pose).Single();
        Assert.Equal(7, packet.ZOrder);
    }
}
