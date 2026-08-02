using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using Ymm4TachieAnimationPlugin.Core.Model;

namespace Ymm4TachieAnimationPlugin.Core.Importing;

public static class PsdImporter
{
    public class PsdLayer
    {
        public string Name { get; set; } = string.Empty;
        public int Top { get; set; }
        public int Left { get; set; }
        public int Bottom { get; set; }
        public int Right { get; set; }
        public int Width => Right - Left;
        public int Height => Bottom - Top;
        public bool IsVisible { get; set; } = true;
        public byte[] PixelData { get; set; } = []; // BGRA32 byte array (Width * Height * 4)
    }

    public class PsdDocument
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public List<PsdLayer> Layers { get; set; } = [];
    }

    public static RigDefinition ImportPsdFile(string psdPath, string outputDirectory, string? rigName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(psdPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        if (!File.Exists(psdPath))
            throw new FileNotFoundException("PSD file not found.", psdPath);

        Directory.CreateDirectory(outputDirectory);

        using var stream = File.OpenRead(psdPath);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        var doc = ParsePsd(reader);

        var rootId = Guid.NewGuid();
        var parts = new List<MeshPartDefinition>();
        var canvasCenterX = doc.Width * 0.5f;
        var canvasCenterY = doc.Height * 0.5f;

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < doc.Layers.Count; i++)
        {
            var layer = doc.Layers[i];
            if (layer.Width <= 0 || layer.Height <= 0 || layer.PixelData.Length == 0)
                continue;

            var safeName = SanitizeFileName(layer.Name);
            if (string.IsNullOrWhiteSpace(safeName)) safeName = $"Layer_{i + 1}";
            var baseFileName = safeName;
            int suffix = 1;
            while (!usedNames.Add(safeName))
            {
                safeName = $"{baseFileName}_{suffix++}";
            }

            var pngFileName = $"{safeName}.png";
            var pngPath = Path.Combine(outputDirectory, pngFileName);

            SaveLayerAsPng(layer, pngPath);

            var layerCenterX = layer.Left + (layer.Width * 0.5f);
            var layerCenterY = layer.Top + (layer.Height * 0.5f);

            var offsetX = layerCenterX - canvasCenterX;
            var offsetY = canvasCenterY - layerCenterY;

            var halfWidth = layer.Width * 0.5f;
            var halfHeight = layer.Height * 0.5f;

            var weight = new[] { new BoneWeight(rootId, 1f) };

            var part = new MeshPartDefinition
            {
                Id = Guid.NewGuid(),
                Name = safeName,
                TexturePath = pngFileName,
                ZOrder = i,
                Vertices =
                [
                    new MeshVertex { Position = new Vector2(offsetX - halfWidth, offsetY + halfHeight), TextureCoordinate = new Vector2(0, 0), Weights = weight },
                    new MeshVertex { Position = new Vector2(offsetX + halfWidth, offsetY + halfHeight), TextureCoordinate = new Vector2(1, 0), Weights = weight },
                    new MeshVertex { Position = new Vector2(offsetX + halfWidth, offsetY - halfHeight), TextureCoordinate = new Vector2(1, 1), Weights = weight },
                    new MeshVertex { Position = new Vector2(offsetX - halfWidth, offsetY - halfHeight), TextureCoordinate = new Vector2(0, 1), Weights = weight },
                ],
                TriangleIndices = [0, 1, 2, 0, 2, 3],
            };

            parts.Add(part);
        }

        var rig = new RigDefinition
        {
            Name = string.IsNullOrWhiteSpace(rigName) ? Path.GetFileNameWithoutExtension(psdPath) : rigName,
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

        var rigJsonPath = Path.Combine(outputDirectory, "rig.json");
        File.WriteAllText(rigJsonPath, Ymm4TachieAnimationPlugin.Core.Serialization.RigSerializer.SerializeRig(rig));

        return rig;
    }

    public static PsdDocument ParsePsd(BinaryReader reader)
    {
        var signature = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (signature != "8BPS") throw new InvalidDataException("Invalid PSD signature.");

        var version = ReadUInt16BE(reader);
        if (version != 1) throw new InvalidDataException($"Unsupported PSD version: {version}");

        reader.ReadBytes(6);
        var channels = ReadUInt16BE(reader);
        var height = ReadInt32BE(reader);
        var width = ReadInt32BE(reader);
        var depth = ReadUInt16BE(reader);
        var colorMode = ReadUInt16BE(reader);

        if (colorMode != 3) throw new InvalidDataException($"Unsupported PSD color mode: {colorMode}. Only RGB is supported.");

        var colorModeLen = ReadUInt32BE(reader);
        reader.BaseStream.Position += colorModeLen;

        var imageResLen = ReadUInt32BE(reader);
        reader.BaseStream.Position += imageResLen;

        var layerMaskLen = ReadUInt32BE(reader);
        var layerMaskEnd = reader.BaseStream.Position + layerMaskLen;

        var doc = new PsdDocument { Width = width, Height = height };

        if (layerMaskLen == 0) return doc;

        var layerInfoLen = ReadUInt32BE(reader);
        if (layerInfoLen == 0) return doc;

        var layerCount = ReadInt16BE(reader);
        bool hasMergedAlpha = layerCount < 0;
        layerCount = Math.Abs(layerCount);

        var layerRecords = new List<LayerRecordHeader>();

        for (int i = 0; i < layerCount; i++)
        {
            var top = ReadInt32BE(reader);
            var left = ReadInt32BE(reader);
            var bottom = ReadInt32BE(reader);
            var right = ReadInt32BE(reader);
            var numChannels = ReadUInt16BE(reader);

            var channelInfos = new List<ChannelInfo>();
            for (int c = 0; c < numChannels; c++)
            {
                var id = ReadInt16BE(reader);
                var len = ReadUInt32BE(reader);
                channelInfos.Add(new ChannelInfo { Id = id, Length = len });
            }

            var blendSig = Encoding.ASCII.GetString(reader.ReadBytes(4));
            var blendKey = Encoding.ASCII.GetString(reader.ReadBytes(4));
            var opacity = reader.ReadByte();
            var clipping = reader.ReadByte();
            var flags = reader.ReadByte();
            var isVisible = (flags & (1 << 1)) == 0;
            reader.ReadByte();

            var extraLen = ReadUInt32BE(reader);
            var extraEnd = reader.BaseStream.Position + extraLen;

            var maskLen = ReadUInt32BE(reader);
            reader.BaseStream.Position += maskLen;

            var blendLen = ReadUInt32BE(reader);
            reader.BaseStream.Position += blendLen;

            var nameLen = reader.ReadByte();
            var nameBytes = reader.ReadBytes(nameLen);
            var layerName = Encoding.UTF8.GetString(nameBytes);

            var totalNameLen = 1 + nameLen;
            var pad = (4 - (totalNameLen % 4)) % 4;
            reader.BaseStream.Position += pad;

            while (reader.BaseStream.Position < extraEnd)
            {
                if (reader.BaseStream.Position + 12 > extraEnd) break;
                var addSig = Encoding.ASCII.GetString(reader.ReadBytes(4));
                if (addSig != "8BIM" && addSig != "8B64")
                {
                    reader.BaseStream.Position -= 3;
                    continue;
                }
                var addKey = Encoding.ASCII.GetString(reader.ReadBytes(4));
                var addLen = ReadUInt32BE(reader);
                var addEnd = reader.BaseStream.Position + addLen;

                if (addKey == "luni" && addLen >= 4)
                {
                    var charCount = ReadInt32BE(reader);
                    if (charCount > 0 && reader.BaseStream.Position + (charCount * 2) <= addEnd)
                    {
                        var uBytes = reader.ReadBytes(charCount * 2);
                        layerName = Encoding.BigEndianUnicode.GetString(uBytes).TrimEnd('\0');
                    }
                }

                reader.BaseStream.Position = Math.Min(addEnd, extraEnd);
            }

            reader.BaseStream.Position = extraEnd;

            layerRecords.Add(new LayerRecordHeader
            {
                Top = top,
                Left = left,
                Bottom = bottom,
                Right = right,
                Name = layerName,
                IsVisible = isVisible,
                Channels = channelInfos,
            });
        }

        foreach (var rec in layerRecords)
        {
            var w = rec.Right - rec.Left;
            var h = rec.Bottom - rec.Top;

            var rData = new byte[w * h];
            var gData = new byte[w * h];
            var bData = new byte[w * h];
            var aData = new byte[w * h];
            Array.Fill(aData, (byte)255);

            foreach (var ch in rec.Channels)
            {
                var chData = ReadChannelImageData(reader, w, h, ch.Length);
                if (ch.Id == 0 && rData.Length == chData.Length) Array.Copy(chData, rData, chData.Length);
                else if (ch.Id == 1 && gData.Length == chData.Length) Array.Copy(chData, gData, chData.Length);
                else if (ch.Id == 2 && bData.Length == chData.Length) Array.Copy(chData, bData, chData.Length);
                else if (ch.Id == -1 && aData.Length == chData.Length) Array.Copy(chData, aData, chData.Length);
            }

            var bgra = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++)
            {
                bgra[i * 4 + 0] = bData[i];
                bgra[i * 4 + 1] = gData[i];
                bgra[i * 4 + 2] = rData[i];
                bgra[i * 4 + 3] = aData[i];
            }

            doc.Layers.Add(new PsdLayer
            {
                Name = rec.Name,
                Top = rec.Top,
                Left = rec.Left,
                Bottom = rec.Bottom,
                Right = rec.Right,
                IsVisible = rec.IsVisible,
                PixelData = bgra,
            });
        }

        return doc;
    }

    private static byte[] ReadChannelImageData(BinaryReader reader, int width, int height, uint length)
    {
        if (width <= 0 || height <= 0 || length == 0) return Array.Empty<byte>();

        var startPos = reader.BaseStream.Position;
        var comp = ReadUInt16BE(reader);

        var result = new byte[width * height];

        if (comp == 0)
        {
            var readBytes = reader.ReadBytes(Math.Min(result.Length, (int)length - 2));
            Array.Copy(readBytes, result, Math.Min(readBytes.Length, result.Length));
        }
        else if (comp == 1)
        {
            var rleRowLengths = new ushort[height];
            for (int i = 0; i < height; i++)
            {
                rleRowLengths[i] = ReadUInt16BE(reader);
            }

            int destOffset = 0;
            for (int r = 0; r < height; r++)
            {
                var rowLen = rleRowLengths[r];
                var rowEnd = reader.BaseStream.Position + rowLen;

                int rowRead = 0;
                while (reader.BaseStream.Position < rowEnd && rowRead < width)
                {
                    sbyte n = reader.ReadSByte();
                    if (n >= 0)
                    {
                        int count = n + 1;
                        for (int k = 0; k < count && rowRead < width; k++)
                        {
                            result[destOffset + rowRead++] = reader.ReadByte();
                        }
                    }
                    else if (n > -128)
                    {
                        int count = 1 - n;
                        byte b = reader.ReadByte();
                        for (int k = 0; k < count && rowRead < width; k++)
                        {
                            result[destOffset + rowRead++] = b;
                        }
                    }
                }
                destOffset += width;
                reader.BaseStream.Position = rowEnd;
            }
        }

        reader.BaseStream.Position = startPos + length;
        return result;
    }

    private static void SaveLayerAsPng(PsdLayer layer, string outputPath)
    {
        using var fs = File.Create(outputPath);
        // Header
        fs.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        // IHDR Chunk
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), layer.Width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4, 4), layer.Height);
        ihdr[8] = 8; // Bit depth
        ihdr[9] = 6; // Color type (RGBA)
        ihdr[10] = 0; // Compression
        ihdr[11] = 0; // Filter
        ihdr[12] = 0; // Interlace
        WritePngChunk(fs, "IHDR", ihdr);

        // IDAT Chunk (Raw Scanlines converted BGRA -> RGBA + ZLib compressed)
        var scanlineLen = 1 + (layer.Width * 4);
        var rawData = new byte[scanlineLen * layer.Height];

        for (int y = 0; y < layer.Height; y++)
        {
            var rowStart = y * scanlineLen;
            rawData[rowStart] = 0; // Filter None

            var srcPixelStart = y * layer.Width * 4;
            for (int x = 0; x < layer.Width; x++)
            {
                var b = layer.PixelData[srcPixelStart + (x * 4) + 0];
                var g = layer.PixelData[srcPixelStart + (x * 4) + 1];
                var r = layer.PixelData[srcPixelStart + (x * 4) + 2];
                var a = layer.PixelData[srcPixelStart + (x * 4) + 3];

                var dest = rowStart + 1 + (x * 4);
                rawData[dest + 0] = r;
                rawData[dest + 1] = g;
                rawData[dest + 2] = b;
                rawData[dest + 3] = a;
            }
        }

        using (var compressedStream = new MemoryStream())
        {
            using (var zlibStream = new ZLibStream(compressedStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlibStream.Write(rawData, 0, rawData.Length);
            }
            WritePngChunk(fs, "IDAT", compressedStream.ToArray());
        }

        // IEND Chunk
        WritePngChunk(fs, "IEND", Array.Empty<byte>());
    }

    private static void WritePngChunk(Stream stream, string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        var lenBytes = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lenBytes, data.Length);
        stream.Write(lenBytes, 0, 4);
        stream.Write(typeBytes, 0, 4);

        if (data.Length > 0)
        {
            stream.Write(data, 0, data.Length);
        }

        var crcInput = new byte[4 + data.Length];
        Array.Copy(typeBytes, 0, crcInput, 0, 4);
        if (data.Length > 0)
        {
            Array.Copy(data, 0, crcInput, 4, data.Length);
        }

        uint crc = Crc32(crcInput);
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        stream.Write(crcBytes, 0, 4);
    }

    private static uint Crc32(byte[] bytes)
    {
        uint crc = 0xffffffff;
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            crc ^= b;
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 1) != 0) crc = (crc >> 1) ^ 0xedb88320;
                else crc >>= 1;
            }
        }
        return ~crc;
    }

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (var ch in name)
        {
            sb.Append(invalidChars.Contains(ch) ? '_' : ch);
        }
        return sb.ToString().Trim();
    }

    private struct ChannelInfo
    {
        public short Id;
        public uint Length;
    }

    private class LayerRecordHeader
    {
        public int Top { get; set; }
        public int Left { get; set; }
        public int Bottom { get; set; }
        public int Right { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsVisible { get; set; }
        public List<ChannelInfo> Channels { get; set; } = [];
    }

    private static ushort ReadUInt16BE(BinaryReader r) => BinaryPrimitives.ReadUInt16BigEndian(r.ReadBytes(2));
    private static short ReadInt16BE(BinaryReader r) => BinaryPrimitives.ReadInt16BigEndian(r.ReadBytes(2));
    private static uint ReadUInt32BE(BinaryReader r) => BinaryPrimitives.ReadUInt32BigEndian(r.ReadBytes(4));
    private static int ReadInt32BE(BinaryReader r) => BinaryPrimitives.ReadInt32BigEndian(r.ReadBytes(4));
}
