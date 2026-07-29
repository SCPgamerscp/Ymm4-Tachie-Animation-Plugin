using System.Numerics;

namespace Ymm4BoneAnimation.Core.Model;

public readonly record struct BoneTransform(Vector2 Translation, float Rotation, Vector2 Scale, int ZOrder)
{
    public static BoneTransform Identity => new(Vector2.Zero, 0, Vector2.One, 0);

    public Matrix3x2 ToMatrix() =>
        Matrix3x2.CreateScale(Scale) *
        Matrix3x2.CreateRotation(Rotation) *
        Matrix3x2.CreateTranslation(Translation);

    public static BoneTransform Lerp(BoneTransform from, BoneTransform to, float amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        var delta = MathF.IEEERemainder(to.Rotation - from.Rotation, MathF.Tau);
        return new(
            Vector2.Lerp(from.Translation, to.Translation, amount),
            from.Rotation + delta * amount,
            Vector2.Lerp(from.Scale, to.Scale, amount),
            amount < 0.5f ? from.ZOrder : to.ZOrder);
    }
}

public sealed class Pose
{
    private readonly Dictionary<Guid, BoneTransform> transforms;

    public Pose(IEnumerable<KeyValuePair<Guid, BoneTransform>> transforms) =>
        this.transforms = transforms.ToDictionary();

    public IReadOnlyDictionary<Guid, BoneTransform> Transforms => transforms;

    public BoneTransform this[Guid boneId]
    {
        get => transforms.TryGetValue(boneId, out var value) ? value : BoneTransform.Identity;
        set => transforms[boneId] = value;
    }

    public Pose Clone() => new(transforms);

    public static Pose FromRestPose(RigDefinition rig) => new(rig.Bones.Select(x =>
        KeyValuePair.Create(x.Id, new BoneTransform(x.Translation, x.Rotation, x.Scale, x.ZOrder))));

    public static Pose Blend(Pose from, Pose to, float amount, IEnumerable<Guid> boneIds) =>
        new(boneIds.Select(id => KeyValuePair.Create(id, BoneTransform.Lerp(from[id], to[id], amount))));
}
