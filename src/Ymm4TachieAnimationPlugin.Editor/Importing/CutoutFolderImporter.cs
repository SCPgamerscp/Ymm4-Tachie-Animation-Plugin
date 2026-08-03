using Ymm4TachieAnimationPlugin.Core.Model;

namespace Ymm4TachieAnimationPlugin.Editor.Importing;

public static class CutoutFolderImporter
{
    public static RigDefinition ImportPsd(string psdPath, string outputDirectory, string? name = null)
    {
        return Core.Importing.CutoutFolderImporter.ImportPsd(psdPath, outputDirectory, name);
    }

    public static RigDefinition Import(string directory, string? name = null)
    {
        return Core.Importing.CutoutFolderImporter.Import(directory, name);
    }
}
