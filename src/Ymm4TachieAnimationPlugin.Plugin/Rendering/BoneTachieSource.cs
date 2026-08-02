using System.IO;
using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Tachie;
using Ymm4TachieAnimationPlugin.Core.Animation;
using Ymm4TachieAnimationPlugin.Core.Importing;
using Ymm4TachieAnimationPlugin.Core.Model;
using Ymm4TachieAnimationPlugin.Core.Rendering;
using Ymm4TachieAnimationPlugin.Core.Runtime;
using Ymm4TachieAnimationPlugin.Core.Serialization;
using Ymm4TachieAnimationPlugin.Plugin.Parameters;

namespace Ymm4TachieAnimationPlugin.Plugin.Rendering;

internal sealed class BoneTachieSource : ITachieSource
{
    private readonly IGraphicsDevicesAndContext devices;
    private readonly ID2D1Bitmap empty;
    private readonly Vortice.Direct2D1.Effects.AffineTransform2D transform;
    private readonly ID2D1Image output;
    private readonly D3D11MeshRenderer renderer;
    private RigDefinition? rig;
    private AnimationClip? animation;
    private RigEvaluator? evaluator;
    private RuntimePoseController? runtimeController;
    private MeshRenderPacketBuilder? packetBuilder;
    private string? rawDirectoryPath;
    private string? loadedDirectory;
    private string? loadedMotion;

    public BoneTachieSource(IGraphicsDevicesAndContext devices)
    {
        this.devices = devices;
        empty = devices.DeviceContext.CreateEmptyBitmap();
        transform = new Vortice.Direct2D1.Effects.AffineTransform2D(devices.DeviceContext);
        renderer = new D3D11MeshRenderer(devices);
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
        var face = faceParameter as BoneFaceParameter;
        if (character?.DirectoryPath != rawDirectoryPath || item?.MotionName != loadedMotion)
            Load(character?.DirectoryPath, item?.MotionName);

        if (rig is null || evaluator is null)
        {
            SetEmpty();
            return;
        }

        var sampleTime = TimeSpan.FromTicks((long)(tachieTime.Ticks * (item?.PlaybackSpeed ?? 1)));
        var sampledPose = animation?.Sample(rig, sampleTime, tachieLength) ?? Pose.FromRestPose(rig);
        var pose = runtimeController?.Apply(sampledPose, sampleTime, face?.Expression, kuchipaku) ?? sampledPose;
        _ = evaluator.EvaluateGlobals(pose);
        if (packetBuilder is null || string.IsNullOrWhiteSpace(loadedDirectory))
        {
            SetEmpty();
            return;
        }

        try
        {
            transform.SetInput(0, null, true);
            var bitmap = renderer.Render(packetBuilder.Build(pose), loadedDirectory, out var origin);
            transform.TransformMatrix = Matrix3x2.CreateTranslation(origin.X, -origin.Y);
            transform.SetInput(0, bitmap, true);
        }
        catch (Exception)
        {
            SetEmpty();
        }
    }

    private void Load(string? directory, string? motionName)
    {
        rawDirectoryPath = directory;
        loadedDirectory = directory;
        loadedMotion = motionName;
        rig = null;
        animation = null;
        evaluator = null;
        runtimeController = null;
        packetBuilder = null;

        if (string.IsNullOrWhiteSpace(directory)) return;

        // Auto-handle PSD file path or directory containing PSD file if rig.json does not exist
        if (File.Exists(directory) && Path.GetExtension(directory).Equals(".psd", StringComparison.OrdinalIgnoreCase))
        {
            var psdFile = directory;
            directory = Path.GetDirectoryName(directory) ?? directory;
            loadedDirectory = directory;
            var autoRigPath = Path.Combine(directory, "rig.json");
            if (!File.Exists(autoRigPath))
            {
                PsdImporter.ImportPsdFile(psdFile, directory);
            }
        }
        else if (Directory.Exists(directory))
        {
            var autoRigPath = Path.Combine(directory, "rig.json");
            if (!File.Exists(autoRigPath))
            {
                var psdFiles = Directory.EnumerateFiles(directory, "*.psd", SearchOption.TopDirectoryOnly).ToArray();
                if (psdFiles.Length > 0)
                {
                    PsdImporter.ImportPsdFile(psdFiles[0], directory);
                }
            }
        }

        var rigPath = Path.Combine(directory, "rig.json");
        if (!File.Exists(rigPath)) return;

        try
        {
            rig = RigSerializer.DeserializeRig(File.ReadAllText(rigPath));
            evaluator = new RigEvaluator(rig);
            runtimeController = new RuntimePoseController(rig);
            packetBuilder = new MeshRenderPacketBuilder(rig);
            var motionPath = Path.Combine(directory, $"{motionName ?? "idle"}.ymm4anim");
            if (File.Exists(motionPath))
                animation = RigSerializer.DeserializeAnimation(File.ReadAllText(motionPath));

        }
        catch (Exception)
        {
            rig = null;
            animation = null;
            evaluator = null;
            runtimeController = null;
            packetBuilder = null;
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
        renderer.Dispose();
    }
}
