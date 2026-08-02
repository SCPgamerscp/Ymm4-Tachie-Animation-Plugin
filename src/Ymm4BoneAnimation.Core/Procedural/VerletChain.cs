using System.Numerics;

namespace Ymm4BoneAnimation.Core.Procedural;

public sealed class VerletChain
{
    private readonly Vector2[] positions;
    private readonly Vector2[] previous;
    private readonly float[] lengths;

    public VerletChain(IEnumerable<Vector2> points)
    {
        positions = points.ToArray();
        if (positions.Length < 2) throw new ArgumentException("A Verlet chain needs at least two points.", nameof(points));
        previous = positions.ToArray();
        lengths = positions.Zip(positions.Skip(1), Vector2.Distance).ToArray();
    }

    public IReadOnlyList<Vector2> Positions => positions;

    public void Step(Vector2 anchor, float deltaSeconds, Vector2 acceleration, float damping = 0.98f, int constraints = 6)
    {
        if (deltaSeconds <= 0 || !float.IsFinite(deltaSeconds))
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        positions[0] = anchor;
        previous[0] = anchor;

        var deltaSquared = deltaSeconds * deltaSeconds;
        for (var index = 1; index < positions.Length; index++)
        {
            var current = positions[index];
            positions[index] += (positions[index] - previous[index]) * damping + acceleration * deltaSquared;
            previous[index] = current;
        }

        for (var iteration = 0; iteration < constraints; iteration++)
        {
            positions[0] = anchor;
            for (var index = 0; index < lengths.Length; index++)
            {
                var delta = positions[index + 1] - positions[index];
                var distance = delta.Length();
                if (distance < 1e-6f) continue;
                var correction = delta * ((distance - lengths[index]) / distance);
                if (index == 0) positions[index + 1] -= correction;
                else
                {
                    positions[index] += correction * 0.5f;
                    positions[index + 1] -= correction * 0.5f;
                }
            }
        }
    }
}
