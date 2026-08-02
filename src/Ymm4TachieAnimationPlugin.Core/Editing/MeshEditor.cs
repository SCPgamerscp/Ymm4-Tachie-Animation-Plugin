using System.Numerics;
using Ymm4TachieAnimationPlugin.Core.Model;

namespace Ymm4TachieAnimationPlugin.Core.Editing;

public static class MeshEditor
{
    public static MeshPartDefinition MoveVertex(MeshPartDefinition part, int vertexIndex, Vector2 position)
    {
        ValidateVertexIndex(part, vertexIndex);
        var vertices = part.Vertices.ToArray();
        vertices[vertexIndex] = vertices[vertexIndex] with { Position = position };
        return part with { Vertices = vertices };
    }

    public static MeshPartDefinition AddVertex(
        MeshPartDefinition part,
        Vector2 position,
        Vector2 textureCoordinate,
        IEnumerable<BoneWeight> weights)
    {
        var normalized = NormalizeWeights(weights);
        return part with
        {
            Vertices = part.Vertices.Append(new MeshVertex
            {
                Position = position,
                TextureCoordinate = textureCoordinate,
                Weights = normalized,
            }).ToArray(),
        };
    }

    public static MeshPartDefinition RemoveVertex(MeshPartDefinition part, int vertexIndex)
    {
        ValidateVertexIndex(part, vertexIndex);
        var vertices = part.Vertices.Where((_, index) => index != vertexIndex).ToArray();
        var triangles = new List<int>();
        for (var index = 0; index < part.TriangleIndices.Count; index += 3)
        {
            var triangle = part.TriangleIndices.Skip(index).Take(3).ToArray();
            if (triangle.Contains(vertexIndex)) continue;
            triangles.AddRange(triangle.Select(value => value > vertexIndex ? value - 1 : value));
        }
        return part with { Vertices = vertices, TriangleIndices = triangles };
    }

    public static MeshPartDefinition AddTriangle(MeshPartDefinition part, int first, int second, int third)
    {
        ValidateVertexIndex(part, first);
        ValidateVertexIndex(part, second);
        ValidateVertexIndex(part, third);
        if (first == second || second == third || first == third)
            throw new ArgumentException("A triangle must contain three different vertices.");
        return part with { TriangleIndices = part.TriangleIndices.Concat([first, second, third]).ToArray() };
    }

    public static MeshPartDefinition SubdivideTriangle(MeshPartDefinition part, int triangleIndex)
    {
        if (triangleIndex < 0 || triangleIndex >= part.TriangleIndices.Count / 3)
            throw new ArgumentOutOfRangeException(nameof(triangleIndex));
        var offset = triangleIndex * 3;
        var first = part.TriangleIndices[offset];
        var second = part.TriangleIndices[offset + 1];
        var third = part.TriangleIndices[offset + 2];
        var source = new[] { part.Vertices[first], part.Vertices[second], part.Vertices[third] };
        var center = new MeshVertex
        {
            Position = source.Aggregate(Vector2.Zero, (sum, vertex) => sum + vertex.Position) / 3,
            TextureCoordinate = source.Aggregate(Vector2.Zero, (sum, vertex) => sum + vertex.TextureCoordinate) / 3,
            Weights = BlendWeights(source),
        };
        var centerIndex = part.Vertices.Count;
        var indices = part.TriangleIndices.ToList();
        indices.RemoveRange(offset, 3);
        indices.InsertRange(offset,
        [
            first, second, centerIndex,
            second, third, centerIndex,
            third, first, centerIndex,
        ]);
        return part with { Vertices = part.Vertices.Append(center).ToArray(), TriangleIndices = indices };
    }

    private static IReadOnlyList<BoneWeight> BlendWeights(IEnumerable<MeshVertex> vertices)
    {
        var raw = vertices
            .SelectMany(vertex => vertex.Weights)
            .GroupBy(weight => weight.BoneId)
            .Select(group => new BoneWeight(group.Key, group.Sum(weight => weight.Weight)))
            .OrderByDescending(weight => weight.Weight)
            .Take(4);
        return NormalizeWeights(raw);
    }

    private static IReadOnlyList<BoneWeight> NormalizeWeights(IEnumerable<BoneWeight> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        var merged = weights
            .Where(weight => weight.Weight > 0 && float.IsFinite(weight.Weight))
            .GroupBy(weight => weight.BoneId)
            .Select(group => new BoneWeight(group.Key, group.Sum(weight => weight.Weight)))
            .OrderByDescending(weight => weight.Weight)
            .Take(4)
            .ToArray();
        if (merged.Length == 0) throw new ArgumentException("At least one positive bone weight is required.", nameof(weights));
        var total = merged.Sum(weight => weight.Weight);
        return merged.Select(weight => weight with { Weight = weight.Weight / total }).ToArray();
    }

    private static void ValidateVertexIndex(MeshPartDefinition part, int index)
    {
        if (index < 0 || index >= part.Vertices.Count) throw new ArgumentOutOfRangeException(nameof(index));
    }
}
