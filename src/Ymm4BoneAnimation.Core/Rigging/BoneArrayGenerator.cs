using System.Numerics;
using Ymm4BoneAnimation.Core.Model;

namespace Ymm4BoneAnimation.Core.Rigging;

public enum MirrorAxis
{
    Horizontal,
    Vertical,
}

public static class BoneArrayGenerator
{
    public static IReadOnlyList<BoneDefinition> CreateChain(
        int segmentCount,
        float segmentLength,
        string namePrefix,
        Vector2 origin,
        float rotation = 0,
        Guid? parentId = null,
        string? tagPrefix = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(segmentCount, 1);
        if (!float.IsFinite(segmentLength) || segmentLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(segmentLength));
        ArgumentException.ThrowIfNullOrWhiteSpace(namePrefix);

        var result = new List<BoneDefinition>(segmentCount);
        var currentParent = parentId;
        for (var index = 0; index < segmentCount; index++)
        {
            var id = Guid.NewGuid();
            result.Add(new BoneDefinition
            {
                Id = id,
                Name = $"{namePrefix}{index + 1}",
                ParentId = currentParent,
                RetargetTag = tagPrefix is null ? null : $"{tagPrefix}{index + 1}",
                Translation = index == 0 ? origin : new Vector2(segmentLength, 0),
                Rotation = index == 0 ? rotation : 0,
                Length = segmentLength,
            });
            currentParent = id;
        }
        return result;
    }

    public static IReadOnlyList<BoneDefinition> CreateRadialLimbs(
        Guid parentId,
        int limbCount,
        int segmentsPerLimb,
        float segmentLength,
        float radius,
        string namePrefix = "Leg")
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limbCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(segmentsPerLimb, 1);
        if (!float.IsFinite(radius) || radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));

        var result = new List<BoneDefinition>(limbCount * segmentsPerLimb);
        for (var limb = 0; limb < limbCount; limb++)
        {
            var angle = limb * MathF.Tau / limbCount;
            var origin = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            result.AddRange(CreateChain(
                segmentsPerLimb,
                segmentLength,
                $"{namePrefix}{limb + 1}_",
                origin,
                angle,
                parentId,
                $"{namePrefix}{limb + 1}_"));
        }
        return result;
    }

    public static IReadOnlyList<BoneDefinition> Mirror(
        IReadOnlyList<BoneDefinition> source,
        MirrorAxis axis,
        Guid? externalParentId = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var idMap = source.ToDictionary(x => x.Id, _ => Guid.NewGuid());
        return source.Select(bone =>
        {
            var translation = axis == MirrorAxis.Horizontal
                ? new Vector2(-bone.Translation.X, bone.Translation.Y)
                : new Vector2(bone.Translation.X, -bone.Translation.Y);
            var rotation = axis == MirrorAxis.Horizontal ? MathF.PI - bone.Rotation : -bone.Rotation;
            var parent = bone.ParentId is { } sourceParent && idMap.TryGetValue(sourceParent, out var mapped)
                ? mapped
                : externalParentId ?? bone.ParentId;
            return bone with
            {
                Id = idMap[bone.Id],
                Name = SwapSide(bone.Name),
                ParentId = parent,
                RetargetTag = bone.RetargetTag is null ? null : SwapSide(bone.RetargetTag),
                Translation = translation,
                Rotation = MathF.IEEERemainder(rotation, MathF.Tau),
            };
        }).ToArray();
    }

    private static string SwapSide(string value)
    {
        const string marker = "\0SIDE\0";
        return value
            .Replace("Left", marker, StringComparison.OrdinalIgnoreCase)
            .Replace("Right", "Left", StringComparison.OrdinalIgnoreCase)
            .Replace(marker, "Right", StringComparison.Ordinal)
            .Replace("_L", marker, StringComparison.OrdinalIgnoreCase)
            .Replace("_R", "_L", StringComparison.OrdinalIgnoreCase)
            .Replace(marker, "_R", StringComparison.Ordinal);
    }
}
