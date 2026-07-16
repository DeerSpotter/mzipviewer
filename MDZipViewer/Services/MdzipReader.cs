using System.IO.Compression;
using MDZipViewer.Model;

namespace MDZipViewer.Services;

public sealed class MdzipReader
{
    private readonly PortedMagicDrawParser _parser = new();
    private readonly ModernMagicDrawVisualParser _visualParser = new();

    public MdzipInventory Read(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("A file path is required.", nameof(filePath));
        if (!File.Exists(filePath))
            throw new FileNotFoundException("The MDZIP file was not found.", filePath);
        if (!filePath.EndsWith(".mdzip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The selected file must have the .mdzip extension.");

        var inventory = new MdzipInventory
        {
            SourceFile = Path.GetFullPath(filePath),
            ScannedAtUtc = DateTime.UtcNow
        };

        using var archive = ZipFile.OpenRead(filePath);
        inventory.ArchiveEntries.AddRange(archive.Entries.Select(e => e.FullName));

        // Retain the broader semantic extraction, then replace the visual layer
        // with the parser validated against CATIA Magic 2026.1 bundled samples.
        _parser.Parse(archive, inventory);
        _visualParser.ReplaceVisuals(archive, inventory);

        return inventory;
    }
}
