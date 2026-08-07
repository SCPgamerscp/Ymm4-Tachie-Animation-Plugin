using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Ymm4TachieAnimationPlugin.Core.Importing;

namespace Ymm4TachieAnimationPlugin.Editor.Controls;

public partial class DirectorySelectorWithRigEditor : UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(DirectorySelectorWithRigEditor),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string? Value
    {
        get => (string?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public DirectorySelectorWithRigEditor()
    {
        InitializeComponent();
    }

    private void OnBrowseFolderClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "リグフォルダーを選択",
        };
        if (!string.IsNullOrWhiteSpace(Value) && Directory.Exists(Value))
        {
            dialog.InitialDirectory = Value;
        }

        if (dialog.ShowDialog() == true)
        {
            Value = dialog.FolderName;
        }
    }

    private void OnOpenRigEditorClicked(object sender, RoutedEventArgs e)
    {
        var path = Value;
        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show("リグフォルダーまたはPSDファイルが指定されていません。", "案内", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            if (File.Exists(path) && Path.GetExtension(path).Equals(".psd", StringComparison.OrdinalIgnoreCase))
            {
                var dir = Path.GetDirectoryName(path) ?? path;
                var rigJson = Path.Combine(dir, "rig.json");
                if (!File.Exists(rigJson))
                {
                    PsdImporter.ImportPsdFile(path, dir);
                }
                path = dir;
            }
            else if (Directory.Exists(path))
            {
                var rigJson = Path.Combine(path, "rig.json");
                if (!File.Exists(rigJson))
                {
                    CutoutFolderImporter.Import(path);
                }
            }

            if (Directory.Exists(path))
            {
                var rigJsonPath = Path.Combine(path, "rig.json");
                if (File.Exists(rigJsonPath))
                {
                    var rig = Core.Serialization.RigSerializer.DeserializeRig(File.ReadAllText(rigJsonPath));
                    EditorLauncher.Open(rig, rigJsonPath);
                }
            }
            else
            {
                MessageBox.Show("指定されたフォルダーまたはファイルが見つかりません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ボーンエディターの起動中にエラーが発生しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
