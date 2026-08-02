using System.Windows;
using Ymm4TachieAnimationPlugin.Core.Model;
using Ymm4TachieAnimationPlugin.Editor.Views;

namespace Ymm4TachieAnimationPlugin.Editor;

public static class EditorLauncher
{
    public static RigEditorWindow Open(RigDefinition rig, string? documentPath = null, Window? owner = null)
    {
        var window = new RigEditorWindow(rig, documentPath) { Owner = owner };
        window.Show();
        return window;
    }
}
