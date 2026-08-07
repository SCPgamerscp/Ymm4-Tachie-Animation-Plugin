using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Ymm4TachieAnimationPlugin.Core.Importing;

namespace Ymm4TachieAnimationPlugin.Editor.Controls;

public partial class FileSelectorWithRigEditor : UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(FileSelectorWithRigEditor),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string? Value
    {
        get => (string?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public FileSelectorWithRigEditor()
    {
        InitializeComponent();
    }

    private void OnBrowseFileClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "PSDファイルを選択",
            Filter = "PSDファイル (*.psd;*.psb)|*.psd;*.psb|すべてのファイル (*.*)|*.*",
            FilterIndex = 1,
            Multiselect = false,
        };
        if (!string.IsNullOrWhiteSpace(Value) && File.Exists(Value))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(Value);
            dialog.FileName = Value;
        }

        if (dialog.ShowDialog() == true)
        {
            Value = dialog.FileName;
        }
    }

    private void OnOpenRigEditorClicked(object sender, RoutedEventArgs e)
    {
        var path = Value;

        if (string.IsNullOrWhiteSpace(path) && DataContext != null)
        {
            var dt = DataContext.GetType();
            var owner = dt.GetProperty("Item")?.GetValue(DataContext) 
                     ?? dt.GetProperty("PropertyOwner")?.GetValue(DataContext);
            if (owner != null)
            {
                var dirVal = owner.GetType().GetProperty("DirectoryPath")?.GetValue(owner) as string;
                var psdVal = owner.GetType().GetProperty("PsdFilePath")?.GetValue(owner) as string;
                path = !string.IsNullOrWhiteSpace(dirVal) ? dirVal : psdVal;
            }
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show("PSDファイルまたはリグフォルダーが指定されていません。", "案内", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show("指定されたファイルまたはフォルダーが見つかりません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ボーンエディターの起動中にエラーが発生しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
