using System.Numerics;
using Ymm4TachieAnimationPlugin.Core.Procedural;

namespace Ymm4TachieAnimationPlugin.Core.Model;

public sealed record BoneTransformDelta
{
    public Vector2 Translation { get; init; }
    public float Rotation { get; init; }
    public Vector2 ScaleMultiplier { get; init; } = Vector2.One;
    public int ZOrderOffset { get; init; }
}

public sealed record ExpressionDefinition
{
    public required string Name { get; init; }
    public IReadOnlyDictionary<Guid, BoneTransformDelta> BoneDeltas { get; init; }
        = new Dictionary<Guid, BoneTransformDelta>();
}

public sealed record ProceduralChainDefinition
{
    public required string Name { get; init; }
    public IReadOnlyList<Guid> BoneIds { get; init; } = [];
    public bool EnableWave { get; init; }
    public ChainWaveSettings Wave { get; init; } = new();
    public bool EnablePhysics { get; init; }
    public Vector2 Acceleration { get; init; } = new(0, 600);
    public float Damping { get; init; } = 0.98f;
    public int ConstraintIterations { get; init; } = 6;
}

public enum RuntimeEventType
{
    SoundEffect,
    Particle,
    TrailStart,
    TrailStop,
}

public sealed record RuntimeEventDefinition
{
    public required string Name { get; init; }
    public RuntimeEventType Type { get; init; }
    public TimeSpan Time { get; init; }
    public string? ResourcePath { get; init; }
    public Guid? BoneId { get; init; }
}

public sealed record AttachmentDefinition
{
    public required string Name { get; init; }
    public required Guid BoneId { get; init; }
    public string? TargetRigPath { get; init; }
    public Vector2 Translation { get; init; }
    public float Rotation { get; init; }
    public Vector2 Scale { get; init; } = Vector2.One;
}
