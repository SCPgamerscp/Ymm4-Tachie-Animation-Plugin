using System.Numerics;

namespace Ymm4TachieAnimationPlugin.Core.Model;

public enum TextureFiltering
{
    PixelPerfect,
    Bilinear,
    Bicubic,
}

public readonly record struct BoneWeight(Guid BoneId, float Weight);

public sealed record MeshVertex
{
    public Vector2 Position { get; init; }
    public Vector2 TextureCoordinate { get; init; }
    public IReadOnlyList<BoneWeight> Weights { get; init; } = [];
}

public sealed record MeshPartDefinition
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string TexturePath { get; init; }
    public int ZOrder { get; init; }
    public TextureFiltering Filtering { get; init; } = TextureFiltering.Bilinear;
    public IReadOnlyList<MeshVertex> Vertices { get; init; } = [];
    public IReadOnlyList<int> TriangleIndices { get; init; } = [];

    internal void Validate(IEnumerable<Guid> validBoneIds)
    {
        var boneIds = validBoneIds.ToHashSet();
        if (string.IsNullOrWhiteSpace(Name))
            throw new RigValidationException("Mesh part name must not be empty.");
        if (string.IsNullOrWhiteSpace(TexturePath))
            throw new RigValidationException($"Mesh part '{Name}' has no texture path.");
        if (TriangleIndices.Count % 3 != 0 || TriangleIndices.Any(x => x < 0 || x >= Vertices.Count))
            throw new RigValidationException($"Mesh part '{Name}' contains invalid triangle indices.");

        foreach (var vertex in Vertices)
        {
            if (!float.IsFinite(vertex.Position.X) || !float.IsFinite(vertex.Position.Y))
                throw new RigValidationException($"Mesh part '{Name}' contains a non-finite vertex.");
            if (vertex.Weights.Count is < 1 or > 4)
                throw new RigValidationException($"Each vertex in '{Name}' must have one to four weights.");
            if (vertex.Weights.Any(x => !boneIds.Contains(x.BoneId) || x.Weight < 0 || !float.IsFinite(x.Weight)))
                throw new RigValidationException($"Mesh part '{Name}' contains an invalid bone weight.");
            var total = vertex.Weights.Sum(x => x.Weight);
            if (MathF.Abs(total - 1f) > 0.001f)
                throw new RigValidationException($"Vertex weights in '{Name}' must sum to one.");
        }
    }
}
