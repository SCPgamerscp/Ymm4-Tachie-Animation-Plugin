using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Tachie;
using Ymm4TachieAnimationPlugin.Plugin.Parameters;
using Ymm4TachieAnimationPlugin.Plugin.Rendering;

namespace Ymm4TachieAnimationPlugin.Plugin;

public sealed class BoneAnimationTachiePlugin : ITachiePlugin
{
    public string Name => "2D\u30DC\u30FC\u30F3\u30A2\u30CB\u30E1\u30FC\u30B7\u30E7\u30F3\u7ACB\u3061\u7D75";

    public ITachieCharacterParameter CreateCharacterParameter() => new BoneCharacterParameter();
    public ITachieItemParameter CreateItemParameter() => new BoneItemParameter();
    public ITachieFaceParameter CreateFaceParameter() => new BoneFaceParameter();
    public ITachieSource CreateTachieSource(IGraphicsDevicesAndContext devices) => new BoneTachieSource(devices);

    public bool HasScriptFile => false;
    public void CreateScriptFile(string scriptDirectoryPath) { }

    public IEnumerable<ExoItem> CreateExoItems(
        int FPS,
        IEnumerable<TachieItemExoDescription> tachieItemDescriptions,
        IEnumerable<TachieFaceItemExoDescription> faceItemDescriptions,
        IEnumerable<TachieVoiceItemExoDescription> voiceDescriptions) => [];
}
