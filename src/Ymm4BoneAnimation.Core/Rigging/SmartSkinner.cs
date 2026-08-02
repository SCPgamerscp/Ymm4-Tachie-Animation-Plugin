using System.Numerics;
using Ymm4BoneAnimation.Core.Model;
using Ymm4BoneAnimation.Core.Runtime;

namespace Ymm4BoneAnimation.Core.Rigging;

public static class SmartSkinner
{
    public static MeshPartDefinition Bind(MeshPartDefinition mesh, RigDefinition rig, int influences = 4, float falloff = 2f)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(rig);
        influences = Math.Clamp(influences, 1, 4);
        if (!float.IsFinite(falloff) || falloff <= 0) throw new ArgumentOutOfRangeException(nameof(falloff));
        if (rig.Bones.Count == 0) throw new RigValidationException("Cannot skin a mesh to an empty rig.");

        var globals = new RigEvaluator(rig).EvaluateGlobals(Pose.FromRestPose(rig));
        var segments = rig.Bones.Select(bone =>
        {
            var start = globals[bone.Id].Translation;
            var end = Vector2.Transform(new Vector2(bone.Length, 0), globals[bone.Id]);
            return (bone.Id, Start: start, End: end);
        }).ToArray();

        var vertices = mesh.Vertices.Select(vertex => vertex with
        {
            Weights = CalculateWeights(vertex.Position, segments, influences, falloff),
        }).ToArray();
        return mesh with { Vertices = vertices };
    }

    public static IReadOnlyList<MeshPartDefinition> RepeatBind(
        MeshPartDefinition template,
        IReadOnlyList<Guid> targetBoneIds,
        string namePrefix = "Part")
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(targetBoneIds);
        return targetBoneIds.Select((boneId, index) => template with
        {
            Id = Guid.NewGuid(),
            Name = $"{namePrefix}{index + 1}",
            ZOrder = template.ZOrder + index,
            Vertices = template.Vertices.Select(vertex => vertex with
            {
                Weights = [new BoneWeight(boneId, 1)],
            }).ToArray(),
        }).ToArray();
    }

    private static IReadOnlyList<BoneWeight> CalculateWeights(
        Vector2 point,
        IReadOnlyList<(Guid Id, Vector2 Start, Vector2 End)> segments,
        int influences,
        float falloff)
    {
        var nearest = segments
            .Select(segment => (segment.Id, Distance: DistanceToSegment(point, segment.Start, segment.End)))
            .OrderBy(x => x.Distance)
            .Take(influences)
            .ToArray();
        if (nearest[0].Distance < 1e-5f) return [new BoneWeight(nearest[0].Id, 1)];

        var raw = nearest.Select(x => (x.Id, Weight: 1f / MathF.Pow(MathF.Max(x.Distance, 1e-5f), falloff))).ToArray();
        var total = raw.Sum(x => x.Weight);
        return raw.Select(x => new BoneWeight(x.Id, x.Weight / total)).ToArray();
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared < 1e-8f) return Vector2.Distance(point, start);
        var amount = Math.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0, 1);
        return Vector2.Distance(point, start + segment * amount);
    }
}
