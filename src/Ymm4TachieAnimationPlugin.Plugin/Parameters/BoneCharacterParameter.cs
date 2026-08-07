using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Windows.Input;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Tachie;
using YukkuriMovieMaker.Settings;
using Ymm4TachieAnimationPlugin.Core.Importing;

namespace Ymm4TachieAnimationPlugin.Plugin.Parameters;

internal sealed class BoneCharacterParameter : TachieCharacterParameterBase
{
    [Display(Name = "リグフォルダー", Description = "rig.json、テクスチャ、モーションを格納したフォルダー")]
    [DirectorySelector]
    public string? DirectoryPath
    {
        get => directoryPath;
        set => Set(ref directoryPath, value);
    }
    private string? directoryPath;

    [Display(Name = "PSDファイル", Description = "PSDファイルを直接指定する場合はこちら。指定した場合はリグフォルダーより優先され、自動でリグに変換されます。")]
    [FileSelector(FileGroupType.TachieParts, CustomFilterName = "PSDファイル", CustomFilterValue = "*.psd;*.psb")]
    public string? PsdFilePath
    {
        get => psdFilePath;
        set => Set(ref psdFilePath, value);
    }
    private string? psdFilePath;

    [Display(Name = "ボーンリグ編集", Description = "2Dボーンリグエディターを開いて、ボーン構造やキーフレームモーションを編集します。")]
    public ICommand OpenRigEditorCommand => openRigEditorCommand ??= new DelegateCommand(OpenRigEditor);
    private ICommand? openRigEditorCommand;

    private void OpenRigEditor()
    {
        var targetPath = !string.IsNullOrWhiteSpace(PsdFilePath) ? PsdFilePath : DirectoryPath;
        if (string.IsNullOrWhiteSpace(targetPath)) return;

        try
        {
            var dir = targetPath;
            if (File.Exists(targetPath) && Path.GetExtension(targetPath).Equals(".psd", StringComparison.OrdinalIgnoreCase))
            {
                dir = Path.GetDirectoryName(targetPath) ?? targetPath;
                var rigJson = Path.Combine(dir, "rig.json");
                if (!File.Exists(rigJson))
                {
                    PsdImporter.ImportPsdFile(targetPath, dir);
                }
            }
            else if (Directory.Exists(targetPath))
            {
                var rigJson = Path.Combine(targetPath, "rig.json");
                if (!File.Exists(rigJson))
                {
                    CutoutFolderImporter.Import(targetPath);
                }
            }

            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            {
                RigEditorService.Open(dir);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BoneCharacterParameter] Failed to open RigEditor: {ex}");
        }
    }
}

internal sealed class DelegateCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}
