using Godot;
using System.Security.Cryptography;
using System.Text.Json;

namespace L5R.Client;

/// <summary>
/// Playable card faces (<c>res://Content/Published/oracle-l5r-N.jpg</c>) read straight from the catalogue art pack, which already holds every one of them at the same size.
/// The exported game leaves those JPGs out (a second, converted copy was half a gigabyte), so this is the only source there; a development checkout falls back to the JPG.
/// Main thread only: one ZipReader stays open for the life of the process.
/// </summary>
public static class CardFaces
{
    private sealed class Receipt
    {
        public string File { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public int Bytes { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
    private sealed class Manifest
    {
        public int SchemaVersion { get; set; }
        public Dictionary<string, Receipt> Scans { get; set; } = [];
        public Dictionary<string, string> Resources { get; set; } = [];
    }
    private static Manifest? _manifest;
    private static ZipReader? _zip;
    private static bool _zipTried;

    private static Manifest Load()
    {
        if (_manifest is not null) return _manifest;
        using var file = Godot.FileAccess.Open("res://Content/catalogue-art.json", Godot.FileAccess.ModeFlags.Read);
        var parsed = file is null ? null : JsonSerializer.Deserialize<Manifest>(file.GetAsText(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        return _manifest = parsed is { SchemaVersion: 1 } ? parsed : new();
    }

    private static Receipt? ReceiptFor(string resourcePath) =>
        Load().Resources.TryGetValue(resourcePath, out var key) && Load().Scans.TryGetValue(key, out var receipt) ? receipt : null;

    /// <summary>True when the pack has a face for this resource path (the game's view of "does this card have art").</summary>
    public static bool Has(string resourcePath) => ReceiptFor(resourcePath) is not null && Godot.FileAccess.FileExists(CatalogueScans.PackPath);

    /// <summary>The face for a resource path, or null when the pack is missing, lacks it or the stored image fails its receipt.</summary>
    public static Texture2D? Get(string resourcePath)
    {
        if (ReceiptFor(resourcePath) is not { } receipt) return null;
        if (receipt.File.Contains('/') || receipt.File.Contains('\\') || !receipt.File.EndsWith(".webp", StringComparison.Ordinal) || receipt.Bytes is <= 0 or > 8 * 1024 * 1024) return null;
        if (!_zipTried)
        {
            _zipTried = true;
            var zip = new ZipReader();
            if (zip.Open(CatalogueScans.PackPath) == Error.Ok) _zip = zip; else zip.Dispose();
        }
        if (_zip is null || !_zip.FileExists(receipt.File)) return null;
        var bytes = _zip.ReadFile(receipt.File);
        if (bytes.Length != receipt.Bytes || !Convert.ToHexString(SHA256.HashData(bytes)).Equals(receipt.Sha256, StringComparison.OrdinalIgnoreCase)) return null;
        var image = new Image();
        if (image.LoadWebpFromBuffer(bytes) != Error.Ok || image.GetWidth() != receipt.Width || image.GetHeight() != receipt.Height) { image.Dispose(); return null; }
        var texture = ImageTexture.CreateFromImage(image);
        image.Dispose();
        return texture;
    }
}
