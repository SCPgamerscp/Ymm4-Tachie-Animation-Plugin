using System.Numerics;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Ymm4TachieAnimationPlugin.Core.Editing;
using Ymm4TachieAnimationPlugin.Core.Model;
using Ymm4TachieAnimationPlugin.Editor.ViewModels;

namespace Ymm4TachieAnimationPlugin.Editor.Views;

public partial class RigEditorWindow : Window
{
    private Point? previousPointer;
    private Point? previousPanPointer;

    public RigEditorWindow(RigDefinition rig, string? documentPath = null)
    {
        InitializeComponent();
        DataContext = new RigEditorViewModel(new RigEditorSession(rig), documentPath);
    }

    private RigEditorViewModel ViewModel => (RigEditorViewModel)DataContext;

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        ViewModel.ActiveTool = e.Key switch
        {
            Key.W => EditorTransformTool.Translate,
            Key.E => EditorTransformTool.Rotate,
            Key.R => EditorTransformTool.Scale,
            Key.I => EditorTransformTool.Ik,
            _ => ViewModel.ActiveTool,
        };
    }

    private void ImportPsdFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "PSDまたは画像ファイルを選択",
            Filter = "PSD / 画像ファイル (*.psd;*.psb;*.png)|*.psd;*.psb;*.PSD;*.PSB;*.png;*.PNG|すべてのファイル (*.*)|*.*",
            FilterIndex = 1,
            Multiselect = false,
        };
        if (dialog.ShowDialog(this) == true) ViewModel.ImportFileOrDirectory(dialog.FileName);
    }

    private void ImportFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "バラバラ立ち絵フォルダーを選択" };
        if (dialog.ShowDialog(this) == true) ViewModel.ImportDirectory(dialog.FolderName);
    }

    private void EditorSurface_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files is { Length: > 0 }) ViewModel.ImportFileOrDirectory(files[0]);
        }
    }

    private void AddKeyframe_Click(object sender, RoutedEventArgs e) => ViewModel.AddKeyframe();
    private void DeleteKeyframe_Click(object sender, RoutedEventArgs e) => ViewModel.DeleteKeyframe();
    private void TranslateTool_Click(object sender, RoutedEventArgs e) => ViewModel.ActiveTool = EditorTransformTool.Translate;
    private void RotateTool_Click(object sender, RoutedEventArgs e) => ViewModel.ActiveTool = EditorTransformTool.Rotate;
    private void ScaleTool_Click(object sender, RoutedEventArgs e) => ViewModel.ActiveTool = EditorTransformTool.Scale;
    private void IkTool_Click(object sender, RoutedEventArgs e) => ViewModel.ActiveTool = EditorTransformTool.Ik;

    private void EditorSurface_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var factor = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
        ViewModel.ZoomLevel *= factor;
    }

    private void EditorSurface_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            previousPanPointer = e.GetPosition(EditorSurface);
            EditorSurface.CaptureMouse();
        }
    }

    private void EditorSurface_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            previousPanPointer = null;
            EditorSurface.ReleaseMouseCapture();
        }
    }

    private void EditorSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        previousPointer = e.GetPosition(EditorSurface);
        EditorSurface.CaptureMouse();
    }

    private void EditorSurface_MouseMove(object sender, MouseEventArgs e)
    {
        var current = e.GetPosition(EditorSurface);

        if (previousPanPointer is { } panPrev && e.MiddleButton == MouseButtonState.Pressed)
        {
            ViewModel.PanX += current.X - panPrev.X;
            ViewModel.PanY += current.Y - panPrev.Y;
            previousPanPointer = current;
            return;
        }

        if (previousPointer is { } previous && e.LeftButton == MouseButtonState.Pressed)
        {
            ViewModel.ApplyPointerDelta(new Vector2((float)(current.X - previous.X), (float)(current.Y - previous.Y)));
            previousPointer = current;
        }
    }

    private void EditorSurface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        previousPointer = null;
        EditorSurface.ReleaseMouseCapture();
    }
}
