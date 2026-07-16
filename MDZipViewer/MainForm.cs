using System.Text.Json;
using MDZipViewer.Controls;
using MDZipViewer.Model;
using MDZipViewer.Services;

namespace MDZipViewer;

public sealed class MainForm : Form
{
    private readonly MdzipReader _reader = new();
    private readonly Label _summary = new() { AutoSize = true, Padding = new Padding(8) };
    private readonly DataGridView _elements = CreateGrid();
    private readonly DataGridView _diagrams = CreateGrid();
    private readonly DiagramPreviewControl _diagramPreview = new();
    private readonly ListBox _documents = new() { Dock = DockStyle.Fill };
    private readonly ListBox _entries = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true };
    private MdzipInventory? _inventory;

    public MainForm(string? initialFile)
    {
        Text = "MDZip Viewer";
        Width = 1200;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(850, 550);

        var openButton = new Button { Text = "Open .mdzip", AutoSize = true };
        openButton.Click += (_, _) => SelectAndOpen();

        var exportButton = new Button { Text = "Export inventory JSON", AutoSize = true };
        exportButton.Click += (_, _) => ExportJson();

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8),
            WrapContents = false
        };
        toolbar.Controls.Add(openButton);
        toolbar.Controls.Add(exportButton);
        toolbar.Controls.Add(_summary);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(CreatePage("Diagram Preview", _diagramPreview));
        tabs.TabPages.Add(CreatePage("Elements", _elements));
        tabs.TabPages.Add(CreatePage("Diagrams", _diagrams));
        tabs.TabPages.Add(CreatePage("Model documents", _documents));
        tabs.TabPages.Add(CreatePage("Archive entries", _entries));

        Controls.Add(tabs);
        Controls.Add(toolbar);

        Shown += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(initialFile) && File.Exists(initialFile))
                OpenFile(initialFile);
        };
    }

    private static DataGridView CreateGrid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false
    };

    private static TabPage CreatePage(string title, Control content)
    {
        var page = new TabPage(title);
        page.Controls.Add(content);
        return page;
    }

    private void SelectAndOpen()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Open CATIA Magic / Cameo project",
            Filter = "MagicDraw project archive (*.mdzip)|*.mdzip|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            OpenFile(dialog.FileName);
    }

    private void OpenFile(string path)
    {
        try
        {
            Cursor = Cursors.WaitCursor;
            _inventory = _reader.Read(path);
            Text = $"MDZip Viewer - {Path.GetFileName(path)}";
            BindInventory(_inventory);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Unable to open MDZIP", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void BindInventory(MdzipInventory inventory)
    {
        _summary.Text = $"Packages: {inventory.PackageCount:N0}   Elements: {inventory.Elements.Count:N0}   Relationships: {inventory.RelationshipCount:N0}   Diagrams: {inventory.Diagrams.Count:N0}";
        _elements.DataSource = inventory.Elements.OrderBy(e => e.Type).ThenBy(e => e.Name).ToList();
        _diagrams.DataSource = inventory.Diagrams.OrderBy(d => d.Type).ThenBy(d => d.Name).ToList();
        _diagramPreview.Bind(inventory);

        _documents.Items.Clear();
        foreach (var document in inventory.Documents)
            _documents.Items.Add($"{document.EntryName} | root={document.RootName} | exporter={document.ExporterVersion ?? "unknown"}");

        _entries.Items.Clear();
        foreach (var entry in inventory.ArchiveEntries.OrderBy(e => e, StringComparer.OrdinalIgnoreCase))
            _entries.Items.Add(entry);
    }

    private void ExportJson()
    {
        if (_inventory is null)
        {
            MessageBox.Show(this, "Open an .mdzip file first.", "Nothing to export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Export MDZIP inventory",
            Filter = "JSON file (*.json)|*.json",
            FileName = Path.GetFileNameWithoutExtension(_inventory.SourceFile) + "-inventory.json"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var json = JsonSerializer.Serialize(_inventory, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(dialog.FileName, json);
        MessageBox.Show(this, "Inventory exported successfully.", "Export complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
