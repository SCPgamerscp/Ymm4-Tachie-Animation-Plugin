using System.Numerics;

namespace Ymm4BoneAnimation.Core.Editing;

public sealed record SnapSettings
{
    public bool GridEnabled { get; init; } = true;
    public float GridSize { get; init; } = 10;
    public bool AngleEnabled { get; init; } = true;
    public float AngleStepDegrees { get; init; } = 15;
    public bool GuidesEnabled { get; init; } = true;
    public float GuideThreshold { get; init; } = 6;
}

public readonly record struct SnapResult(Vector2 Value, bool SnappedX, bool SnappedY);

public static class SnapEngine
{
    public static SnapResult SnapPoint(Vector2 point, SnapSettings settings, IEnumerable<Vector2>? guides = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var value = point;
        var snappedX = false;
        var snappedY = false;
        if (settings.GridEnabled)
        {
            if (!float.IsFinite(settings.GridSize) || settings.GridSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(settings), "Grid size must be positive.");
            value = new Vector2(
                MathF.Round(value.X / settings.GridSize) * settings.GridSize,
                MathF.Round(value.Y / settings.GridSize) * settings.GridSize);
            snappedX = snappedY = true;
        }

        if (settings.GuidesEnabled && guides is not null)
        {
            var guideArray = guides.ToArray();
            var nearestX = guideArray.OrderBy(x => MathF.Abs(x.X - point.X)).FirstOrDefault();
            var nearestY = guideArray.OrderBy(x => MathF.Abs(x.Y - point.Y)).FirstOrDefault();
            if (guideArray.Length > 0 && MathF.Abs(nearestX.X - point.X) <= settings.GuideThreshold)
            {
                value.X = nearestX.X;
                snappedX = true;
            }
            if (guideArray.Length > 0 && MathF.Abs(nearestY.Y - point.Y) <= settings.GuideThreshold)
            {
                value.Y = nearestY.Y;
                snappedY = true;
            }
        }
        return new SnapResult(value, snappedX, snappedY);
    }

    public static float SnapAngle(float radians, SnapSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.AngleEnabled) return radians;
        if (!float.IsFinite(settings.AngleStepDegrees) || settings.AngleStepDegrees <= 0)
            throw new ArgumentOutOfRangeException(nameof(settings), "Angle step must be positive.");
        var step = settings.AngleStepDegrees * MathF.PI / 180;
        return MathF.Round(radians / step) * step;
    }

    public static IReadOnlyList<Vector2> CreateImageGuides(Vector2 minimum, Vector2 maximum) =>
    [
        minimum,
        maximum,
        new((minimum.X + maximum.X) * 0.5f, (minimum.Y + maximum.Y) * 0.5f),
        new(minimum.X, maximum.Y),
        new(maximum.X, minimum.Y),
    ];
}
