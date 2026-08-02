using Ymm4TachieAnimationPlugin.Core.Model;

namespace Ymm4TachieAnimationPlugin.Core.Procedural;

public sealed record ChainWaveSettings
{
    public float AmplitudeRadians { get; init; } = 0.2f;
    public float FrequencyHz { get; init; } = 1f;
    public float PhasePerBone { get; init; } = 0.35f;
    public float Falloff { get; init; } = 0.98f;
}

public static class ChainWaveMotion
{
    public static void Apply(Pose pose, IReadOnlyList<Guid> chain, TimeSpan time, ChainWaveSettings settings)
    {
        ArgumentNullException.ThrowIfNull(pose);
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(settings);
        var basePhase = (float)time.TotalSeconds * settings.FrequencyHz * MathF.Tau;
        for (var index = 0; index < chain.Count; index++)
        {
            var id = chain[index];
            var transform = pose[id];
            var amplitude = settings.AmplitudeRadians * MathF.Pow(settings.Falloff, index);
            pose[id] = transform with
            {
                Rotation = transform.Rotation + MathF.Sin(basePhase - index * settings.PhasePerBone) * amplitude,
            };
        }
    }
}
