using System.IO.Compression;
using MDZipViewer.Model;

namespace MDZipViewer.Services;

public sealed class MdzipReader
{
    private readonly PortedMagicDrawParser _parser = new();

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
        _parser.Parse(archive, inventory);
        return inventory;
    }
}
