using System.Numerics;
using Ymm4TachieAnimationPlugin.Core.Model;
using Ymm4TachieAnimationPlugin.Core.Rigging;

namespace Ymm4TachieAnimationPlugin.Core.Tests;

public sealed class RiggingTests
{
    [Fact]
    public void CreateRadialLimbs_BuildsIndependentChains()
    {
        var root = Guid.NewGuid();
        var bones = BoneArrayGenerator.CreateRadialLimbs(root, 8, 3, 10, 5);
        Assert.Equal(24, bones.Count);
        Assert.Equal(8, bones.Count(x => x.ParentId == root));
        Assert.Equal(24, bones.Select(x => x.Id).Distinct().Count());
    }

    [Fact]
    public void Mirror_ReparentsAndSwapsSideTags()
    {
        var root = Guid.NewGuid();
        var source = BoneArrayGenerator.CreateChain(2, 10, "Arm_L", new Vector2(5, 2), parentId: root, tagPrefix: "Arm_L");
        var mirrored = BoneArrayGenerator.Mirror(source, MirrorAxis.Horizontal, root);
        Assert.Equal(root, mirrored[0].ParentId);
        Assert.Equal(mirrored[0].Id, mirrored[1].ParentId);
        Assert.Equal(-5, mirrored[0].Translation.X);
        Assert.Contains("_R", mirrored[0].RetargetTag);
    }

    [Fact]
    public void SmartSkinner_NormalizesNearestBoneWeights()
    {
        var rig = RigFixtures.TwoBone(out _, out _);
        var mesh = CreateTriangle(Guid.NewGuid(), TextureFiltering.Bilinear, []);
        var bound = SmartSkinner.Bind(mesh, rig, influences: 2);
        Assert.All(bound.Vertices, vertex => Assert.InRange(vertex.Weights.Sum(x => x.Weight), 0.999f, 1.001f));
        Assert.All(bound.Vertices, vertex => Assert.InRange(vertex.Weights.Count, 1, 2));
    }

    internal static MeshPartDefinition CreateTriangle(Guid boneId, TextureFiltering filtering, IReadOnlyList<BoneWeight>? weights = null)
    {
        var actualWeights = weights ?? [new BoneWeight(boneId, 1)];
        MeshVertex Vertex(Vector2 position) => new()
        {
            Position = position,
            TextureCoordinate = position / 10,
            Weights = actualWeights,
        };

        return new MeshPartDefinition
        {
            Id = Guid.NewGuid(),
            Name = "triangle",
            TexturePath = "part.png",
            Filtering = filtering,
            Vertices =
            [
                Vertex(new Vector2(0.25f, 0.25f)),
                Vertex(new Vector2(10.6f, 0.25f)),
                Vertex(new Vector2(0.25f, 10.6f)),
            ],
            TriangleIndices = [0, 1, 2],
        };
    }
}
