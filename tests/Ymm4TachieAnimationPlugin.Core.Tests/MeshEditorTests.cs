using System.Numerics;
using Ymm4TachieAnimationPlugin.Core.Editing;
using Ymm4TachieAnimationPlugin.Core.Model;

namespace Ymm4TachieAnimationPlugin.Core.Tests;

public sealed class MeshEditorTests
{
    [Fact]
    public void SubdivideTriangle_AddsCenterAndThreeTriangles()
    {
        var bone = Guid.NewGuid();
        var part = RiggingTests.CreateTriangle(bone, TextureFiltering.Bilinear);
        var divided = MeshEditor.SubdivideTriangle(part, 0);
        Assert.Equal(4, divided.Vertices.Count);
        Assert.Equal(9, divided.TriangleIndices.Count);
        Assert.InRange(divided.Vertices[3].Weights.Sum(x => x.Weight), 0.999f, 1.001f);
    }

    [Fact]
    public void RemoveVertex_DropsConnectedTrianglesAndReindexesOthers()
    {
        var bone = Guid.NewGuid();
        var part = RiggingTests.CreateTriangle(bone, TextureFiltering.Bilinear);
        part = MeshEditor.AddVertex(part, new Vector2(20, 20), Vector2.One, [new BoneWeight(bone, 1)]);
        part = MeshEditor.AddTriangle(part, 1, 2, 3);
        var updated = MeshEditor.RemoveVertex(part, 0);
        Assert.Equal(3, updated.Vertices.Count);
        Assert.Equal([0, 1, 2], updated.TriangleIndices);
    }

    [Fact]
    public void SnapPoint_UsesGuidesAndAngleStep()
    {
        var settings = new SnapSettings { GridEnabled = false, GuideThreshold = 3, AngleStepDegrees = 15 };
        var snapped = SnapEngine.SnapPoint(new Vector2(9, 21), settings, [new Vector2(10, 20)]);
        Assert.Equal(new Vector2(10, 20), snapped.Value);
        Assert.True(snapped.SnappedX && snapped.SnappedY);
        Assert.InRange(SnapEngine.SnapAngle(14 * MathF.PI / 180, settings), 14.99f * MathF.PI / 180, 15.01f * MathF.PI / 180);
    }
}
