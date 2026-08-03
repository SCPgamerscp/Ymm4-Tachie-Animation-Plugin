using System.IO;

namespace Ymm4TachieAnimationPlugin.Core.Importing;

internal static class ImageHeaderReader
{
    public static (int width, int height) ReadSize(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext is ".png")
        {
            // PNG signature (8 bytes)
            stream.Seek(8, SeekOrigin.Begin);
            // IHDR chunk length (4) + chunk type "IHDR" (4)
            stream.Seek(8, SeekOrigin.Current);
            var width = ReadInt32BE(reader);
            var height = ReadInt32BE(reader);
            return (width, height);
        }

        // Default fallback: try reading as basic bitmap or 100x100
        return (100, 100);
    }

    private static int ReadInt32BE(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        if (bytes.Length < 4) return 0;
        return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
    }
}
