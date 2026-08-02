using System.IO;
using Ymm4BoneAnimation.Core.Serialization;
using Ymm4BoneAnimation.Editor;
using Ymm4BoneAnimation.Editor.Views;

namespace Ymm4BoneAnimation.Plugin;

public static class RigEditorService
{
    public static RigEditorWindow Open(string rigDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rigDirectory);
        var path = Path.Combine(rigDirectory, "rig.json");
        if (!File.Exists(path)) throw new FileNotFoundException("rig.json was not found.", path);
        var rig = RigSerializer.DeserializeRig(File.ReadAllText(path));
        return EditorLauncher.Open(rig, path);
    }
}
