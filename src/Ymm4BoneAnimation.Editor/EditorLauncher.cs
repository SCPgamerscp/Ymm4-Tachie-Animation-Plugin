using System.Windows;
using Ymm4BoneAnimation.Core.Model;
using Ymm4BoneAnimation.Editor.Views;

namespace Ymm4BoneAnimation.Editor;

public static class EditorLauncher
{
    public static RigEditorWindow Open(RigDefinition rig, string? documentPath = null, Window? owner = null)
    {
        var window = new RigEditorWindow(rig, documentPath) { Owner = owner };
        window.Show();
        return window;
    }
}
