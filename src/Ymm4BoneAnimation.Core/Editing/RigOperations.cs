using Ymm4BoneAnimation.Core.Model;

namespace Ymm4BoneAnimation.Core.Editing;

public static class RigOperations
{
    public static RigDefinition AddBones(RigDefinition rig, IEnumerable<BoneDefinition> bones) =>
        rig with { Bones = rig.Bones.Concat(bones).ToArray() };

    public static RigDefinition UpdateBone(RigDefinition rig, Guid id, Func<BoneDefinition, BoneDefinition> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (!rig.Bones.Any(x => x.Id == id)) throw new KeyNotFoundException($"Bone '{id}' was not found.");
        return rig with { Bones = rig.Bones.Select(x => x.Id == id ? update(x) : x).ToArray() };
    }

    public static RigDefinition RemoveBoneSubtree(RigDefinition rig, Guid rootId)
    {
        var remove = new HashSet<Guid> { rootId };
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var bone in rig.Bones.Where(x => x.ParentId is { } parent && remove.Contains(parent)))
                changed |= remove.Add(bone.Id);
        }

        var remainingParts = rig.Parts
            .Where(part => part.Vertices.All(vertex => vertex.Weights.All(weight => !remove.Contains(weight.BoneId))))
            .ToArray();
        return rig with
        {
            Bones = rig.Bones.Where(x => !remove.Contains(x.Id)).ToArray(),
            Parts = remainingParts,
        };
    }

    public static RigDefinition UpsertPart(RigDefinition rig, MeshPartDefinition part)
    {
        var exists = rig.Parts.Any(x => x.Id == part.Id);
        return rig with
        {
            Parts = exists
                ? rig.Parts.Select(x => x.Id == part.Id ? part : x).ToArray()
                : rig.Parts.Append(part).ToArray(),
        };
    }
}
