using System.Numerics;

namespace Ymm4TachieAnimationPlugin.Core.Model;

public sealed record BoneDefinition
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public Guid? ParentId { get; init; }
    public string? RetargetTag { get; init; }
    public Vector2 Translation { get; init; }
    public float Rotation { get; init; }
    public Vector2 Scale { get; init; } = Vector2.One;
    public float Length { get; init; }
    public int ZOrder { get; init; }
}

public sealed record RigDefinition
{
    public int SchemaVersion { get; init; } = 1;
    public required string Name { get; init; }
    public IReadOnlyList<BoneDefinition> Bones { get; init; } = [];
    public IReadOnlyList<MeshPartDefinition> Parts { get; init; } = [];
    public IReadOnlyList<ExpressionDefinition> Expressions { get; init; } = [];
    public IReadOnlyList<ProceduralChainDefinition> ProceduralChains { get; init; } = [];
    public IReadOnlyList<AttachmentDefinition> Attachments { get; init; } = [];
    public IReadOnlyList<RuntimeEventDefinition> Events { get; init; } = [];

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new RigValidationException("Rig name must not be empty.");
        if (SchemaVersion != 1)
            throw new RigValidationException($"Unsupported schema version: {SchemaVersion}.");

        var ids = new HashSet<Guid>();
        foreach (var bone in Bones)
        {
            if (bone.Id == Guid.Empty || !ids.Add(bone.Id))
                throw new RigValidationException($"Bone id is empty or duplicated: {bone.Id}.");
            if (string.IsNullOrWhiteSpace(bone.Name))
                throw new RigValidationException("Bone name must not be empty.");
            if (bone.Length < 0 || !float.IsFinite(bone.Length))
                throw new RigValidationException($"Bone '{bone.Name}' has an invalid length.");
            if (!IsFinite(bone.Translation) || !IsFinite(bone.Scale) || !float.IsFinite(bone.Rotation))
                throw new RigValidationException($"Bone '{bone.Name}' contains a non-finite transform.");
        }

        var byId = Bones.ToDictionary(x => x.Id);
        foreach (var bone in Bones)
        {
            if (bone.ParentId is { } parentId && !byId.ContainsKey(parentId))
                throw new RigValidationException($"Bone '{bone.Name}' references a missing parent.");
            DetectCycle(bone, byId);
        }

        var partIds = new HashSet<Guid>();
        foreach (var part in Parts)
        {
            if (part.Id == Guid.Empty || !partIds.Add(part.Id))
                throw new RigValidationException($"Part id is empty or duplicated: {part.Id}.");
            part.Validate(byId.Keys);
        }

        if (Expressions.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Expressions.Count)
            throw new RigValidationException("Expression names must be unique.");
        foreach (var expression in Expressions)
        {
            if (string.IsNullOrWhiteSpace(expression.Name) || expression.BoneDeltas.Keys.Any(x => !byId.ContainsKey(x)))
                throw new RigValidationException("An expression has an invalid name or bone reference.");
        }
        foreach (var chain in ProceduralChains)
        {
            if (string.IsNullOrWhiteSpace(chain.Name) || chain.BoneIds.Count < 2 || chain.BoneIds.Any(x => !byId.ContainsKey(x)))
                throw new RigValidationException("A procedural chain has an invalid name or bone reference.");
            if (chain.Damping is < 0 or > 1 || chain.ConstraintIterations < 1)
                throw new RigValidationException($"Procedural chain '{chain.Name}' has invalid physics settings.");
        }
        if (Attachments.Any(x => string.IsNullOrWhiteSpace(x.Name) || !byId.ContainsKey(x.BoneId)))
            throw new RigValidationException("An attachment has an invalid name or bone reference.");
        if (Events.Any(x => string.IsNullOrWhiteSpace(x.Name) || x.Time < TimeSpan.Zero || x.BoneId is { } id && !byId.ContainsKey(id)))
            throw new RigValidationException("A runtime event has invalid data.");
    }

    private static void DetectCycle(BoneDefinition start, IReadOnlyDictionary<Guid, BoneDefinition> bones)
    {
        var visited = new HashSet<Guid>();
        var current = start;
        while (current.ParentId is { } parentId)
        {
            if (!visited.Add(current.Id))
                throw new RigValidationException($"Bone hierarchy contains a cycle at '{start.Name}'.");
            current = bones[parentId];
        }
    }

    private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
}

public sealed class RigValidationException(string message) : Exception(message);
