using System.Numerics;
using Ymm4BoneAnimation.Core.Model;

namespace Ymm4BoneAnimation.Core.Runtime;

public sealed class CcdIkSolver(RigDefinition rig)
{
    private readonly IReadOnlyDictionary<Guid, BoneDefinition> bones = rig.Bones.ToDictionary(x => x.Id);
    private readonly RigEvaluator evaluator = new(rig);

    public float Solve(Pose pose, Guid endBoneId, Vector2 target, int chainLength = 8, int iterations = 16, float tolerance = 0.5f)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(chainLength, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);
        if (!bones.ContainsKey(endBoneId)) throw new ArgumentException("End bone does not exist.", nameof(endBoneId));

        var chain = BuildChain(endBoneId, chainLength);
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            for (var index = 0; index < chain.Count; index++)
            {
                var jointId = chain[index];
                var globals = evaluator.EvaluateGlobals(pose);
                var joint = globals[jointId].Translation;
                var end = EndPoint(endBoneId, globals);
                var toEnd = end - joint;
                var toTarget = target - joint;
                if (toEnd.LengthSquared() < 1e-8f || toTarget.LengthSquared() < 1e-8f) continue;

                var delta = MathF.Atan2(Cross(toEnd, toTarget), Vector2.Dot(toEnd, toTarget));
                var transform = pose[jointId];
                pose[jointId] = transform with { Rotation = transform.Rotation + delta };
            }

            var distance = Vector2.Distance(EndPoint(endBoneId, evaluator.EvaluateGlobals(pose)), target);
            if (distance <= tolerance) return distance;
        }
        return Vector2.Distance(EndPoint(endBoneId, evaluator.EvaluateGlobals(pose)), target);
    }

    private List<Guid> BuildChain(Guid endBoneId, int chainLength)
    {
        var result = new List<Guid> { endBoneId };
        var current = bones[endBoneId];
        while (result.Count < chainLength && current.ParentId is { } parent)
        {
            result.Add(parent);
            current = bones[parent];
        }
        return result;
    }

    private Vector2 EndPoint(Guid id, IReadOnlyDictionary<Guid, Matrix3x2> globals) =>
        Vector2.Transform(new Vector2(bones[id].Length, 0), globals[id]);

    private static float Cross(Vector2 left, Vector2 right) => left.X * right.Y - left.Y * right.X;
}
