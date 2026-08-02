using System.Numerics;
using Ymm4TachieAnimationPlugin.Core.Model;
using Ymm4TachieAnimationPlugin.Core.Procedural;

namespace Ymm4TachieAnimationPlugin.Core.Runtime;

/// <summary>Applies expressions, lip sync, blinking, chain waves and persistent Verlet physics to a sampled pose.</summary>
public sealed class RuntimePoseController
{
    private sealed record PhysicsState(VerletChain Chain, TimeSpan LastTime);

    private readonly RigDefinition rig;
    private readonly RigEvaluator evaluator;
    private readonly Dictionary<string, PhysicsState> physics = new(StringComparer.Ordinal);

    public RuntimePoseController(RigDefinition rig)
    {
        rig.Validate();
        this.rig = rig;
        evaluator = new RigEvaluator(rig);
    }

    public Pose Apply(Pose source, TimeSpan time, string? expression, double lipSync, bool automaticBlink = true)
    {
        ArgumentNullException.ThrowIfNull(source);
        var pose = source.Clone();
        ApplyExpression(pose, expression);
        ApplyLipSync(pose, lipSync);
        if (automaticBlink) ApplyBlink(pose, time);
        foreach (var definition in rig.ProceduralChains)
        {
            if (definition.EnableWave)
                ChainWaveMotion.Apply(pose, definition.BoneIds, time, definition.Wave);
            if (definition.EnablePhysics)
                ApplyPhysics(pose, definition, time);
        }
        return pose;
    }

    public IReadOnlyList<RuntimeEventDefinition> EventsBetween(TimeSpan previous, TimeSpan current, TimeSpan duration)
    {
        if (current < TimeSpan.Zero || previous < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(current));
        if (duration <= TimeSpan.Zero || rig.Events.Count == 0) return [];
        var from = Mod(previous, duration);
        var to = Mod(current, duration);
        return rig.Events
            .Where(x => current - previous >= duration || (from <= to ? x.Time > from && x.Time <= to : x.Time > from || x.Time <= to))
            .OrderBy(x => x.Time)
            .ToArray();
    }

    public void ResetPhysics() => physics.Clear();

    private void ApplyExpression(Pose pose, string? name)
    {
        var definition = rig.Expressions.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (definition is null) return;
        foreach (var (boneId, delta) in definition.BoneDeltas)
        {
            var value = pose[boneId];
            pose[boneId] = value with
            {
                Translation = value.Translation + delta.Translation,
                Rotation = value.Rotation + delta.Rotation,
                Scale = value.Scale * delta.ScaleMultiplier,
                ZOrder = value.ZOrder + delta.ZOrderOffset,
            };
        }
    }

    private void ApplyLipSync(Pose pose, double amount)
    {
        if (amount < 0) return;
        var openness = 0.15f + Math.Clamp((float)amount, 0, 1) * 0.85f;
        foreach (var bone in rig.Bones.Where(x => IsTag(x, "Mouth") || IsTag(x, "Lip")))
        {
            var value = pose[bone.Id];
            pose[bone.Id] = value with { Scale = new Vector2(value.Scale.X, value.Scale.Y * openness) };
        }
    }

    private void ApplyBlink(Pose pose, TimeSpan time)
    {
        const float interval = 3.7f;
        const float duration = 0.14f;
        var phase = (float)(time.TotalSeconds % interval);
        if (phase >= duration) return;
        var normalized = phase / duration;
        var openness = MathF.Abs(normalized * 2 - 1);
        foreach (var bone in rig.Bones.Where(x => IsTag(x, "Eye") || IsTag(x, "Eyelid")))
        {
            var value = pose[bone.Id];
            pose[bone.Id] = value with { Scale = new Vector2(value.Scale.X, value.Scale.Y * openness) };
        }
    }

    private void ApplyPhysics(Pose pose, ProceduralChainDefinition definition, TimeSpan time)
    {
        if (!physics.TryGetValue(definition.Name, out var state) || time < state.LastTime)
        {
            var globals = evaluator.EvaluateGlobals(pose);
            var points = definition.BoneIds.Select(id => globals[id].Translation).ToList();
            var lastBone = rig.Bones.First(x => x.Id == definition.BoneIds[^1]);
            points.Add(Vector2.Transform(new Vector2(lastBone.Length, 0), globals[lastBone.Id]));
            state = new PhysicsState(new VerletChain(points), time);
        }

        var delta = Math.Clamp((float)(time - state.LastTime).TotalSeconds, 1f / 240f, 1f / 15f);
        var anchor = evaluator.EvaluateGlobals(pose)[definition.BoneIds[0]].Translation;
        state.Chain.Step(anchor, delta, definition.Acceleration, definition.Damping, definition.ConstraintIterations);
        var parentAngle = 0f;
        for (var index = 0; index < definition.BoneIds.Count; index++)
        {
            var direction = state.Chain.Positions[index + 1] - state.Chain.Positions[index];
            if (direction.LengthSquared() < 1e-6f) continue;
            var worldAngle = MathF.Atan2(direction.Y, direction.X);
            var id = definition.BoneIds[index];
            pose[id] = pose[id] with { Rotation = worldAngle - parentAngle };
            parentAngle = worldAngle;
        }
        physics[definition.Name] = state with { LastTime = time };
    }

    private static bool IsTag(BoneDefinition bone, string value) =>
        bone.RetargetTag?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;

    private static TimeSpan Mod(TimeSpan value, TimeSpan duration) =>
        TimeSpan.FromTicks((value.Ticks % duration.Ticks + duration.Ticks) % duration.Ticks);
}
