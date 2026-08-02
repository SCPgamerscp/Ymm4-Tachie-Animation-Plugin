using System.Numerics;

namespace Ymm4TachieAnimationPlugin.Core.Procedural;

public sealed record LegDefinition(Guid EndBoneId, Vector2 RestTarget, float Phase);

public sealed record GaitSettings
{
    public float StrideLength { get; init; } = 30f;
    public float StepHeight { get; init; } = 12f;
    public float CyclesPerSecond { get; init; } = 1.5f;
    public float StanceRatio { get; init; } = 0.65f;
}

public static class MultipedGait
{
    public static IReadOnlyDictionary<Guid, Vector2> CalculateTargets(
        IReadOnlyList<LegDefinition> legs,
        TimeSpan time,
        Vector2 velocity,
        GaitSettings settings)
    {
        ArgumentNullException.ThrowIfNull(legs);
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.StanceRatio is <= 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(settings), "Stance ratio must be between zero and one.");

        var direction = velocity.LengthSquared() > 1e-6f ? Vector2.Normalize(velocity) : Vector2.UnitX;
        var up = new Vector2(0, -1);
        return legs.ToDictionary(leg => leg.EndBoneId, leg =>
        {
            var phase = PositiveModulo((float)time.TotalSeconds * settings.CyclesPerSecond + leg.Phase, 1);
            if (phase < settings.StanceRatio)
            {
                var stance = phase / settings.StanceRatio;
                return leg.RestTarget + direction * settings.StrideLength * (0.5f - stance);
            }
            var swing = (phase - settings.StanceRatio) / (1 - settings.StanceRatio);
            var horizontal = settings.StrideLength * (swing - 0.5f);
            var lift = MathF.Sin(swing * MathF.PI) * settings.StepHeight;
            return leg.RestTarget + direction * horizontal + up * lift;
        });
    }

    private static float PositiveModulo(float value, float divisor) => (value % divisor + divisor) % divisor;
}
