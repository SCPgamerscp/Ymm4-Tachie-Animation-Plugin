using System.Numerics;

namespace Ymm4BoneAnimation.Core.Model;

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
