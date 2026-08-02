using System.Numerics;
using Ymm4TachieAnimationPlugin.Core.Model;

namespace Ymm4TachieAnimationPlugin.Core.Runtime;

public sealed class RigEvaluator
{
    private readonly RigDefinition rig;
    private readonly IReadOnlyDictionary<Guid, BoneDefinition> bones;
    private readonly Guid[] evaluationOrder;
    private readonly IReadOnlyDictionary<Guid, Matrix3x2> inverseBindMatrices;

    public RigEvaluator(RigDefinition rig)
    {
        rig.Validate();
        this.rig = rig;
        bones = rig.Bones.ToDictionary(x => x.Id);
        evaluationOrder = TopologicalOrder(rig.Bones).ToArray();
        var restGlobals = EvaluateGlobals(Pose.FromRestPose(rig));
        inverseBindMatrices = restGlobals.ToDictionary(
            x => x.Key,
            x => Matrix3x2.Invert(x.Value, out var inverse) ? inverse : Matrix3x2.Identity);
    }

    public IReadOnlyDictionary<Guid, Matrix3x2> EvaluateGlobals(Pose pose)
    {
        var result = new Dictionary<Guid, Matrix3x2>(bones.Count);
        foreach (var id in evaluationOrder)
        {
            var local = pose[id].ToMatrix();
            var parentId = bones[id].ParentId;
            result[id] = parentId is { } parent ? local * result[parent] : local;
        }
        return result;
    }

    public IReadOnlyList<Vector2> Deform(MeshPartDefinition mesh, Pose pose)
    {
        var globals = EvaluateGlobals(pose);
        var skinMatrices = globals.ToDictionary(x => x.Key, x => inverseBindMatrices[x.Key] * x.Value);
        var vertices = new Vector2[mesh.Vertices.Count];
        for (var i = 0; i < vertices.Length; i++)
        {
            var vertex = mesh.Vertices[i];
            var deformed = Vector2.Zero;
            foreach (var weight in vertex.Weights)
                deformed += Vector2.Transform(vertex.Position, skinMatrices[weight.BoneId]) * weight.Weight;
            vertices[i] = deformed;
        }
        return vertices;
    }

    public IEnumerable<MeshPartDefinition> PartsInDrawOrder(Pose pose) =>
        rig.Parts.OrderBy(part => part.ZOrder + ResolvePartZOffset(part, pose));

    private static int ResolvePartZOffset(MeshPartDefinition part, Pose pose) =>
        part.Vertices.SelectMany(x => x.Weights).Select(x => pose[x.BoneId].ZOrder).DefaultIfEmpty().Max();

    private static IEnumerable<Guid> TopologicalOrder(IEnumerable<BoneDefinition> definitions)
    {
        var remaining = definitions.ToDictionary(x => x.Id);
        var emitted = new HashSet<Guid>();
        while (remaining.Count > 0)
        {
            var ready = remaining.Values.Where(x => x.ParentId is null || emitted.Contains(x.ParentId.Value)).ToArray();
            if (ready.Length == 0) throw new RigValidationException("Bone hierarchy contains a cycle.");
            foreach (var bone in ready)
            {
                remaining.Remove(bone.Id);
                emitted.Add(bone.Id);
                yield return bone.Id;
            }
        }
    }
}
