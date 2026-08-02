using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vortice.D3DCompiler;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using Ymm4TachieAnimationPlugin.Core.Model;
using Ymm4TachieAnimationPlugin.Core.Rendering;
using D2DPixelFormat = Vortice.DCommon.PixelFormat;

namespace Ymm4TachieAnimationPlugin.Plugin.Rendering;

/// <summary>Renders skinned triangle packets into a DXGI surface shared with Direct2D.</summary>
internal sealed class D3D11MeshRenderer : IDisposable
{
    private const string ShaderSource = """
        struct VSIn { float2 position : POSITION; float2 uv : TEXCOORD0; };
        struct VSOut { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
        VSOut VSMain(VSIn input) {
            VSOut output;
            output.position = float4(input.position, 0.0, 1.0);
            output.uv = input.uv;
            return output;
        }

        Texture2D sourceTexture : register(t0);
        SamplerState sourceSampler : register(s0);
        cbuffer TextureInfo : register(b0) { float2 texelSize; int filterMode; float padding; };

        float4 cubic(float2 uv) {
            float2 size = 1.0 / texelSize;
            float2 pixel = uv * size - 0.5;
            float2 basePixel = floor(pixel);
            float2 f = pixel - basePixel;
            float2 f2 = f * f;
            float2 f3 = f2 * f;
            float2 w0 = -0.5 * f3 + f2 - 0.5 * f;
            float2 w1 = 1.5 * f3 - 2.5 * f2 + 1.0;
            float2 w2 = -1.5 * f3 + 2.0 * f2 + 0.5 * f;
            float2 w3 = 0.5 * f3 - 0.5 * f2;
            float4 result = 0;
            [unroll] for (int y = 0; y < 4; y++) {
                float wy = y == 0 ? w0.y : (y == 1 ? w1.y : (y == 2 ? w2.y : w3.y));
                [unroll] for (int x = 0; x < 4; x++) {
                    float wx = x == 0 ? w0.x : (x == 1 ? w1.x : (x == 2 ? w2.x : w3.x));
                    float2 tap = (basePixel + float2(x - 1, y - 1) + 0.5) * texelSize;
                    result += sourceTexture.SampleLevel(sourceSampler, tap, 0) * wx * wy;
                }
            }
            return result;
        }

        float4 PSMain(VSOut input) : SV_TARGET {
            float4 color = filterMode == 2 ? cubic(input.uv) : sourceTexture.Sample(sourceSampler, input.uv);
            color.rgb *= color.a;
            return color;
        }
        """;

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct GpuVertex(Vector2 Position, Vector2 TextureCoordinate);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct TextureInfo(Vector2 TexelSize, int FilterMode, float Padding);

    private sealed record TextureResource(ID3D11Texture2D Texture, ID3D11ShaderResourceView View, int Width, int Height) : IDisposable
    {
        public void Dispose()
        {
            View.Dispose();
            Texture.Dispose();
        }
    }

    private readonly IGraphicsDevicesAndContext devices;
    private readonly ID3D11VertexShader vertexShader;
    private readonly ID3D11PixelShader pixelShader;
    private readonly ID3D11InputLayout inputLayout;
    private readonly ID3D11SamplerState pointSampler;
    private readonly ID3D11SamplerState linearSampler;
    private readonly ID3D11BlendState blendState;
    private readonly ID3D11RasterizerState rasterizerState;
    private readonly ID3D11Buffer textureInfoBuffer;
    private readonly Dictionary<string, TextureResource> textures = new(StringComparer.OrdinalIgnoreCase);
    private ID3D11Texture2D? targetTexture;
    private ID3D11RenderTargetView? targetView;
    private ID2D1Bitmap1? targetBitmap;
    private int targetWidth;
    private int targetHeight;

    public D3D11MeshRenderer(IGraphicsDevicesAndContext devices)
    {
        this.devices = devices;
        using var vsBlob = Compiler.Compile(ShaderSource, "VSMain", nameof(D3D11MeshRenderer), "vs_5_0");
        using var psBlob = Compiler.Compile(ShaderSource, "PSMain", nameof(D3D11MeshRenderer), "ps_5_0");
        var device = devices.D3D.Device;
        vertexShader = device.CreateVertexShader(vsBlob);
        pixelShader = device.CreatePixelShader(psBlob);
        inputLayout = device.CreateInputLayout(
        [
            new Vortice.Direct3D11.InputElementDescription("POSITION", 0, Format.R32G32_Float, 0, 0),
            new Vortice.Direct3D11.InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 8, 0),
        ], vsBlob);
        pointSampler = device.CreateSamplerState(SamplerDescription.PointClamp);
        linearSampler = device.CreateSamplerState(SamplerDescription.LinearClamp);
        blendState = device.CreateBlendState(Vortice.Direct3D11.BlendDescription.AlphaBlend);
        rasterizerState = device.CreateRasterizerState(RasterizerDescription.CullNone);
        textureInfoBuffer = device.CreateBuffer(16, BindFlags.ConstantBuffer);
    }

    public ID2D1Bitmap1 Render(IReadOnlyList<MeshRenderPacket> packets, string baseDirectory, out Vector2 origin)
    {
        if (packets.Count == 0)
        {
            origin = Vector2.Zero;
            EnsureTarget(1, 1);
            ClearTarget();
            return targetBitmap!;
        }

        var minimum = packets.Select(x => x.Bounds.Minimum).Aggregate(Vector2.Min);
        var maximum = packets.Select(x => x.Bounds.Maximum).Aggregate(Vector2.Max);
        minimum = new Vector2(MathF.Floor(minimum.X), MathF.Floor(minimum.Y));
        maximum = new Vector2(MathF.Ceiling(maximum.X), MathF.Ceiling(maximum.Y));
        var width = Math.Clamp((int)MathF.Max(1, maximum.X - minimum.X), 1, 16384);
        var height = Math.Clamp((int)MathF.Max(1, maximum.Y - minimum.Y), 1, 16384);
        origin = minimum;
        EnsureTarget(width, height);
        ClearTarget();

        var context = devices.D3D.DeviceContext;
        context.OMSetRenderTargets(targetView!);
        context.OMSetBlendState(blendState);
        context.RSSetState(rasterizerState);
        context.RSSetViewport(0, 0, width, height);
        context.IASetInputLayout(inputLayout);
        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.VSSetShader(vertexShader);
        context.PSSetShader(pixelShader);
        context.PSSetConstantBuffer(0, textureInfoBuffer);

        foreach (var packet in packets.OrderBy(x => x.ZOrder))
            DrawPacket(context, packet, baseDirectory, minimum, width, height);

        context.PSSetShaderResource(0, null!);
        context.OMSetRenderTargets((ID3D11RenderTargetView)null!);
        context.Flush();
        return targetBitmap!;
    }

    private void DrawPacket(
        ID3D11DeviceContext context,
        MeshRenderPacket packet,
        string baseDirectory,
        Vector2 minimum,
        int width,
        int height)
    {
        if (packet.Vertices.Count == 0 || packet.TriangleIndices.Count == 0) return;
        var path = Path.IsPathRooted(packet.TexturePath)
            ? packet.TexturePath
            : Path.GetFullPath(Path.Combine(baseDirectory, packet.TexturePath));
        if (!File.Exists(path)) return;
        var texture = GetTexture(path);
        var vertices = packet.Vertices.Select(vertex =>
        {
            var local = vertex.Position - minimum;
            return new GpuVertex(
                new Vector2(local.X / width * 2 - 1, 1 - local.Y / height * 2),
                vertex.TextureCoordinate);
        }).ToArray();
        var indices = packet.TriangleIndices.Select(Convert.ToUInt32).ToArray();
        using var vertexBuffer = devices.D3D.Device.CreateBuffer(BindFlags.VertexBuffer, vertices);
        using var indexBuffer = devices.D3D.Device.CreateBuffer(BindFlags.IndexBuffer, indices);
        var info = new TextureInfo(
            new Vector2(1f / texture.Width, 1f / texture.Height),
            (int)packet.Filtering,
            0);
        context.UpdateSubresource(info, textureInfoBuffer);
        context.IASetVertexBuffer(0, vertexBuffer, Marshal.SizeOf<GpuVertex>());
        context.IASetIndexBuffer(indexBuffer, Format.R32_UInt, 0);
        context.PSSetShaderResource(0, texture.View);
        context.PSSetSampler(0, packet.Filtering == TextureFiltering.Bilinear ? linearSampler : pointSampler);
        context.DrawIndexed(indices.Length, 0, 0);
    }

    private TextureResource GetTexture(string path)
    {
        if (textures.TryGetValue(path, out var cached)) return cached;
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        BitmapSource source = decoder.Frames[0];
        if (source.Format != PixelFormats.Bgra32)
            source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = checked(source.PixelWidth * 4);
        var pixels = new byte[checked(stride * source.PixelHeight)];
        source.CopyPixels(pixels, stride, 0);
        var description = new Texture2DDescription(
            Format.B8G8R8A8_UNorm,
            source.PixelWidth,
            source.PixelHeight,
            bindFlags: BindFlags.ShaderResource);
        var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        ID3D11Texture2D texture;
        try
        {
            texture = devices.D3D.Device.CreateTexture2D(description,
            [
                new SubresourceData(pinned.AddrOfPinnedObject(), stride),
            ]);
        }
        finally
        {
            pinned.Free();
        }
        var resource = new TextureResource(
            texture,
            devices.D3D.Device.CreateShaderResourceView(texture),
            source.PixelWidth,
            source.PixelHeight);
        textures.Add(path, resource);
        return resource;
    }

    private void EnsureTarget(int width, int height)
    {
        if (width == targetWidth && height == targetHeight && targetBitmap is not null) return;
        targetBitmap?.Dispose();
        targetView?.Dispose();
        targetTexture?.Dispose();
        targetWidth = width;
        targetHeight = height;
        var description = new Texture2DDescription(
            Format.B8G8R8A8_UNorm,
            width,
            height,
            bindFlags: BindFlags.RenderTarget | BindFlags.ShaderResource);
        targetTexture = devices.D3D.Device.CreateTexture2D(description);
        targetView = devices.D3D.Device.CreateRenderTargetView(targetTexture);
        using var surface = targetTexture.QueryInterface<IDXGISurface>();
        targetBitmap = devices.DeviceContext.CreateBitmapFromDxgiSurface(
            surface,
            new BitmapProperties1(
                new D2DPixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                96,
                96,
                BitmapOptions.None));
    }

    private void ClearTarget()
    {
        devices.D3D.DeviceContext.ClearRenderTargetView(targetView!, new Color4(0, 0, 0, 0));
    }

    public void Dispose()
    {
        targetBitmap?.Dispose();
        targetView?.Dispose();
        targetTexture?.Dispose();
        foreach (var texture in textures.Values) texture.Dispose();
        textureInfoBuffer.Dispose();
        rasterizerState.Dispose();
        blendState.Dispose();
        linearSampler.Dispose();
        pointSampler.Dispose();
        inputLayout.Dispose();
        pixelShader.Dispose();
        vertexShader.Dispose();
    }
}
