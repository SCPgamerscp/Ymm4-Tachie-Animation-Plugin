using System.IO;
using System.Numerics;
using System.Windows.Media.Imaging;
using Ymm4TachieAnimationPlugin.Core.Importing;
using Ymm4TachieAnimationPlugin.Core.Model;

namespace Ymm4TachieAnimationPlugin.Editor.Importing;

public static class CutoutFolderImporter
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff",
    };

    public static RigDefinition ImportPsd(string psdPath, string outputDirectory, string? name = null)
    {
        return PsdImporter.ImportPsdFile(psdPath, outputDirectory, name);
    }

    public static RigDefinition Import(string directory, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var fullDirectory = Path.GetFullPath(directory);

        if (File.Exists(fullDirectory) && Path.GetExtension(fullDirectory).Equals(".psd", StringComparison.OrdinalIgnoreCase))
        {
            var targetDir = Path.GetDirectoryName(fullDirectory) ?? fullDirectory;
            return ImportPsd(fullDirectory, targetDir, name);
        }

        if (!Directory.Exists(fullDirectory)) throw new DirectoryNotFoundException(fullDirectory);

        var psdFiles = Directory.EnumerateFiles(fullDirectory, "*.psd", SearchOption.TopDirectoryOnly).ToArray();
        var normalFiles = Directory.EnumerateFiles(fullDirectory)
            .Where(x => SupportedExtensions.Contains(Path.GetExtension(x)))
            .OrderBy(x => Path.GetFileName(x), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalFiles.Length == 0 && psdFiles.Length > 0)
        {
            return ImportPsd(psdFiles[0], fullDirectory, name);
        }

        var files = normalFiles;
        if (files.Length == 0) throw new InvalidDataException("No supported cut-out images were found.");

        var rootId = Guid.NewGuid();
        var parts = files.Select((path, index) => CreatePart(path, fullDirectory, rootId, index)).ToArray();
        var rig = new RigDefinition
        {
            Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileName(fullDirectory) : name,
            Bones =
            [
                new BoneDefinition
                {
                    Id = rootId,
                    Name = "Root",
                    RetargetTag = "Root",
                    Length = 100,
                },
            ],
            Parts = parts,
        };
        rig.Validate();
        return rig;
    }

    private static MeshPartDefinition CreatePart(string path, string rootDirectory, Guid rootId, int zOrder)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var halfWidth = frame.PixelWidth * 0.5f;
        var halfHeight = frame.PixelHeight * 0.5f;
        var weight = new[] { new BoneWeight(rootId, 1) };
        return new MeshPartDefinition
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileNameWithoutExtension(path),
            TexturePath = Path.GetRelativePath(rootDirectory, path),
            ZOrder = zOrder,
            Vertices =
            [
                new MeshVertex { Position = new Vector2(-halfWidth, -halfHeight), TextureCoordinate = new Vector2(0, 0), Weights = weight },
                new MeshVertex { Position = new Vector2(halfWidth, -halfHeight), TextureCoordinate = new Vector2(1, 0), Weights = weight },
                new MeshVertex { Position = new Vector2(halfWidth, halfHeight), TextureCoordinate = new Vector2(1, 1), Weights = weight },
                new MeshVertex { Position = new Vector2(-halfWidth, halfHeight), TextureCoordinate = new Vector2(0, 1), Weights = weight },
            ],
            TriangleIndices = [0, 1, 2, 0, 2, 3],
        };
    }
}
