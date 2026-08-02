using System.IO;
using System.Text;
using Xunit;
using Ymm4TachieAnimationPlugin.Core.Importing;

namespace Ymm4TachieAnimationPlugin.Core.Tests;

public class PsdImporterTests
{
    [Fact]
    public void ParsePsd_WithInvalidSignature_ThrowsInvalidDataException()
    {
        var invalidData = Encoding.ASCII.GetBytes("NOT_PSD_HEADER_DATA");
        using var stream = new MemoryStream(invalidData);
        using var reader = new BinaryReader(stream);

        Assert.Throws<InvalidDataException>(() => PsdImporter.ParsePsd(reader));
    }

    [Fact]
    public void ImportPsdFile_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.psd");
        var outputDir = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}");

        Assert.Throws<FileNotFoundException>(() => PsdImporter.ImportPsdFile(nonExistentPath, outputDir));
    }
}
