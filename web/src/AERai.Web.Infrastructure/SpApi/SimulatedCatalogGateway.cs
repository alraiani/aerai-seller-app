using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using AERai.Web.Application.Abstractions;
using AERai.Web.Domain.Core;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Catalog gateway for <see cref="SpApiMode.Simulated"/>: every ASIN gets a generated, plain-colored
/// PNG (the same color each time), except roughly one in seven, which has no picture, so the "not on
/// Amazon" path can be seen locally too. Nothing leaves the machine.
/// </summary>
internal sealed class SimulatedCatalogGateway : IAmazonCatalogGateway
{
    private const int Size = 240;
    private const string PathPrefix = "/images/I/";
    private const string FilePrefix = "sim-";

    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<string, Uri>> FindMainImagesAsync(Marketplace marketplace, IReadOnlyCollection<string> asins, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asins);

        IReadOnlyDictionary<string, Uri> found = asins
            .Select(a => a.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .Where(a => Hash(a) % 7 != 0)
            .ToDictionary(a => a, a => new Uri($"https://m.media-amazon.com{PathPrefix}{FilePrefix}{Uri.EscapeDataString(a)}.png"), StringComparer.Ordinal);
        return Task.FromResult(found);
    }

    /// <inheritdoc/>
    public Task<Stream> OpenImageAsync(Uri url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        var name = Path.GetFileNameWithoutExtension(url.AbsolutePath);
        if (!url.AbsolutePath.StartsWith(PathPrefix, StringComparison.Ordinal) || !name.StartsWith(FilePrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Not a simulated picture address: {url}.");
        }

        var asin = Uri.UnescapeDataString(name[FilePrefix.Length..]);
        return Task.FromResult<Stream>(new MemoryStream(Png(asin), writable: false));
    }

    /// <summary>A square PNG: a colored tile derived from the ASIN on a light background.</summary>
    private static byte[] Png(string asin)
    {
        var hash = Hash(asin);
        var (r, g, b) = ((byte)(60 + (hash % 160)), (byte)(60 + ((hash / 160) % 160)), (byte)(60 + ((hash / 25600) % 160)));

        // Raw scanlines: a filter-type byte (0 = none) followed by RGB pixels.
        var raw = new byte[Size * ((Size * 3) + 1)];
        for (var y = 0; y < Size; y++)
        {
            var row = y * ((Size * 3) + 1);
            for (var x = 0; x < Size; x++)
            {
                var inTile = x is >= 30 and < Size - 30 && y is >= 30 and < Size - 30;
                var at = row + 1 + (x * 3);
                (raw[at], raw[at + 1], raw[at + 2]) = inTile ? (r, g, b) : ((byte)240, (byte)240, (byte)240);
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, Size);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), Size);
        header[8] = 8; // bit depth
        header[9] = 2; // color type: RGB

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream png, string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        Span<byte> number = stackalloc byte[4];

        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        png.Write(number);
        png.Write(typeBytes);
        png.Write(data);

        BinaryPrimitives.WriteUInt32BigEndian(number, Crc32([.. typeBytes, .. data]));
        png.Write(number);
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in bytes)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    // Stable across runs (string.GetHashCode is randomized per process), so an ASIN keeps its color.
    private static uint Hash(string value)
    {
        var hash = 2166136261u;
        foreach (var c in value)
        {
            hash = (hash ^ c) * 16777619u;
        }

        return hash;
    }
}
