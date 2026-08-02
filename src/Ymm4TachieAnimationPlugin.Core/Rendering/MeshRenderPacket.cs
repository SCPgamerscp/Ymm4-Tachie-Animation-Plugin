using System.Numerics;
using Ymm4TachieAnimationPlugin.Core.Model;
using Ymm4TachieAnimationPlugin.Core.Runtime;

namespace Ymm4TachieAnimationPlugin.Core.Rendering;

public readonly record struct RenderVertex(Vector2 Position, Vector2 TextureCoordinate);

public readonly record struct RenderBounds(Vector2 Minimum, Vector2 Maximum)
{
    public Vector2 Size => Maximum - Minimum;
}

public sealed record MeshRenderPacket
{
    public required Guid PartId { get; init; }
    public required string TexturePath { get; init; }
    public required IReadOnlyList<RenderVertex> Vertices { get; init; }
    public required IReadOnlyList<int> TriangleIndices { get; init; }
    public required TextureFiltering Filtering { get; init; }
    public required int ZOrder { get; init; }
    public required RenderBounds Bounds { get; init; }
}

public sealed class MeshRenderPacketBuilder
{
    private readonly RigDefinition rig;
    private readonly RigEvaluator evaluator;

    public MeshRenderPacketBuilder(RigDefinition rig)
    {
        rig.Validate();
        this.rig = rig;
        evaluator = new RigEvaluator(rig);
    }

    public IReadOnlyList<MeshRenderPacket> Build(Pose pose)
    {
        ArgumentNullException.ThrowIfNull(pose);
        var packets = new List<MeshRenderPacket>(rig.Parts.Count);
        foreach (var part in evaluator.PartsInDrawOrder(pose))
        {
            var positions = evaluator.Deform(part, pose);
            var vertices = positions.Select((position, index) => new RenderVertex(
                part.Filtering == TextureFiltering.PixelPerfect ? SnapToPixel(position) : position,
                part.Vertices[index].TextureCoordinate)).ToArray();
            packets.Add(new MeshRenderPacket
            {
                PartId = part.Id,
                TexturePath = part.TexturePath,
                Vertices = vertices,
                TriangleIndices = part.TriangleIndices.ToArray(),
                Filtering = part.Filtering,
                ZOrder = part.ZOrder + ResolveZOffset(part, pose),
                Bounds = CalculateBounds(vertices),
            });
        }
        return packets;
    }

    private static RenderBounds CalculateBounds(IReadOnlyList<RenderVertex> vertices)
    {
        if (vertices.Count == 0) return new RenderBounds(Vector2.Zero, Vector2.Zero);
        var minimum = vertices[0].Position;
        var maximum = minimum;
        for (var index = 1; index < vertices.Count; index++)
        {
            minimum = Vector2.Min(minimum, vertices[index].Position);
            maximum = Vector2.Max(maximum, vertices[index].Position);
        }
        return new RenderBounds(minimum, maximum);
    }

    private static int ResolveZOffset(MeshPartDefinition part, Pose pose) =>
        part.Vertices.SelectMany(x => x.Weights).Select(x => pose[x.BoneId].ZOrder).DefaultIfEmpty().Max();

    private static Vector2 SnapToPixel(Vector2 value) => new(MathF.Round(value.X), MathF.Round(value.Y));
}
