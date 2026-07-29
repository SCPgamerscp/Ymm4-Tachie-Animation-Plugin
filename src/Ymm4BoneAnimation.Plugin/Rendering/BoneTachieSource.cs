using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Tachie;
using Ymm4BoneAnimation.Core.Animation;
using Ymm4BoneAnimation.Core.Model;
using Ymm4BoneAnimation.Core.Runtime;
using Ymm4BoneAnimation.Core.Serialization;
using Ymm4BoneAnimation.Plugin.Parameters;

namespace Ymm4BoneAnimation.Plugin.Rendering;

internal sealed class BoneTachieSource : ITachieSource
{
    private readonly IGraphicsDevicesAndContext devices;
    private readonly ID2D1Bitmap empty;
    private readonly Vortice.Direct2D1.Effects.AffineTransform2D transform;
    private readonly ID2D1Image output;
    private IImageFileSource? image;
    private RigDefinition? rig;
    private AnimationClip? animation;
    private RigEvaluator? evaluator;
    private string? loadedDirectory;
    private string? loadedMotion;

    public BoneTachieSource(IGraphicsDevicesAndContext devices)
    {
        this.devices = devices;
        empty = devices.DeviceContext.CreateEmptyBitmap();
        transform = new Vortice.Direct2D1.Effects.AffineTransform2D(devices.DeviceContext);
        output = transform.Output;
        transform.SetInput(0, empty, true);
    }

    public ID2D1Image Output => output;

    public void Update(
        TimeSpan tachieTime,
        TimeSpan tachieLength,
        TimeSpan faceTime,
        TimeSpan faceLength,
        ITachieCharacterParameter characterParameter,
        ITachieItemParameter itemParameter,
        ITachieFaceParameter faceParameter,
        double kuchipaku)
    {
        var character = characterParameter as BoneCharacterParameter;
        var item = itemParameter as BoneItemParameter;
        if (character?.DirectoryPath != loadedDirectory || item?.MotionName != loadedMotion)
            Load(character?.DirectoryPath, item?.MotionName);

        if (rig is null || evaluator is null)
        {
            SetEmpty();
            return;
        }

        var sampleTime = TimeSpan.FromTicks((long)(tachieTime.Ticks * (item?.PlaybackSpeed ?? 1)));
        var pose = animation?.Sample(rig, sampleTime, tachieLength) ?? Pose.FromRestPose(rig);
        _ = evaluator.EvaluateGlobals(pose);

        // Phase 1 presents the first cut-out part through YMM4's D2D pipeline. The evaluator already
        // produces all mesh vertices; the custom D3D11 mesh renderer will consume them in Phase 2.
        if (image is not null)
        {
            transform.TransformMatrix = Matrix3x2.CreateTranslation(-image.Output.Size.Width / 2, -image.Output.Size.Height / 2);
            transform.SetInput(0, image.Output, true);
        }
    }

    private void Load(string? directory, string? motionName)
    {
        loadedDirectory = directory;
        loadedMotion = motionName;
        image?.Dispose();
        image = null;
        rig = null;
        animation = null;
        evaluator = null;

        if (string.IsNullOrWhiteSpace(directory)) return;
        var rigPath = Path.Combine(directory, "rig.json");
        if (!File.Exists(rigPath)) return;

        try
        {
            rig = RigSerializer.DeserializeRig(File.ReadAllText(rigPath));
            evaluator = new RigEvaluator(rig);
            var motionPath = Path.Combine(directory, $"{motionName ?? "idle"}.ymm4anim");
            if (File.Exists(motionPath))
                animation = RigSerializer.DeserializeAnimation(File.ReadAllText(motionPath));

            var firstPart = rig.Parts.OrderBy(x => x.ZOrder).FirstOrDefault();
            if (firstPart is not null)
            {
                var texturePath = Path.IsPathRooted(firstPart.TexturePath)
                    ? firstPart.TexturePath
                    : Path.Combine(directory, firstPart.TexturePath);
                if (File.Exists(texturePath)) image = ImageFileSourceFactory.Create(devices, texturePath);
            }
        }
        catch (Exception)
        {
            rig = null;
            animation = null;
            evaluator = null;
            image?.Dispose();
            image = null;
        }
    }

    private void SetEmpty()
    {
        transform.SetInput(0, empty, true);
        transform.TransformMatrix = Matrix3x2.CreateTranslation(-empty.Size.Width / 2, -empty.Size.Height / 2);
    }

    public void Dispose()
    {
        transform.SetInput(0, null, true);
        output.Dispose();
        transform.Dispose();
        empty.Dispose();
        image?.Dispose();
    }
}
