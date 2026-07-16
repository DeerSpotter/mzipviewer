using System.Drawing.Drawing2D;
using MDZipViewer.Model;

namespace MDZipViewer.Controls;

public sealed class DiagramPreviewControl : UserControl
{
    private readonly ComboBox _diagramSelector = new()
    {
        Dock = DockStyle.Top,
        DropDownStyle = ComboBoxStyle.DropDownList
    };

    private readonly Label _status = new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        Padding = new Padding(8, 6, 8, 6)
    };

    private readonly PreviewCanvas _canvas = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        BackColor = Color.White
    };

    private MdzipInventory? _inventory;

    public DiagramPreviewControl()
    {
        Dock = DockStyle.Fill;
        _diagramSelector.SelectedIndexChanged += (_, _) => ShowSelectedDiagram();

        Controls.Add(_canvas);
        Controls.Add(_status);
        Controls.Add(_diagramSelector);
    }

    public void Bind(MdzipInventory inventory)
    {
        _inventory = inventory;
        _diagramSelector.DataSource = null;
        _diagramSelector.DisplayMember = nameof(ModelDiagram.Name);
        _diagramSelector.DataSource = inventory.Diagrams
            .OrderBy(d => d.Type)
            .ThenBy(d => d.Name)
            .ToList();

        if (_diagramSelector.Items.Count == 0)
        {
            _status.Text = "No diagram records were detected in this archive.";
            _canvas.SetGraph([], []);
        }
    }

    private void ShowSelectedDiagram()
    {
        if (_inventory is null || _diagramSelector.SelectedItem is not ModelDiagram diagram)
            return;

        var candidateNodes = SelectCandidateNodes(_inventory, diagram).Take(80).ToList();
        var candidateIds = candidateNodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        var relationships = _inventory.Relationships
            .Where(r => r.SourceId is not null && r.TargetId is not null)
            .Where(r => candidateIds.Contains(r.SourceId!) && candidateIds.Contains(r.TargetId!))
            .Take(160)
            .ToList();

        _status.Text = $"Approximate preview: {candidateNodes.Count:N0} nodes, {relationships.Count:N0} connectors. " +
                       "Layout is reconstructed and may not match Cameo positioning.";
        _canvas.SetGraph(candidateNodes, relationships);
    }

    private static IEnumerable<ModelElement> SelectCandidateNodes(MdzipInventory inventory, ModelDiagram diagram)
    {
        var elements = inventory.Elements
            .Where(e => !MdzipTypeClassifier.IsRelationship(e.Type))
            .Where(e => !e.Id.Equals(diagram.Id, StringComparison.Ordinal))
            .ToList();

        var byOwner = elements
            .Where(e => !string.IsNullOrWhiteSpace(diagram.OwnerId) && e.OwnerId == diagram.OwnerId)
            .ToList();
        if (byOwner.Count > 0)
            return byOwner;

        var sameDocument = elements.Where(e => e.Document == diagram.Document).ToList();
        if (sameDocument.Count > 0)
            return sameDocument;

        return elements;
    }

    private sealed class PreviewCanvas : Panel
    {
        private IReadOnlyList<ModelElement> _nodes = [];
        private IReadOnlyList<ModelRelationship> _relationships = [];
        private readonly Dictionary<string, Rectangle> _bounds = new(StringComparer.Ordinal);

        public PreviewCanvas()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        public void SetGraph(
            IReadOnlyList<ModelElement> nodes,
            IReadOnlyList<ModelRelationship> relationships)
        {
            _nodes = nodes;
            _relationships = relationships;
            BuildLayout();
            Invalidate();
        }

        private void BuildLayout()
        {
            _bounds.Clear();

            const int nodeWidth = 190;
            const int nodeHeight = 72;
            const int horizontalGap = 70;
            const int verticalGap = 46;
            const int margin = 40;

            var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Math.Max(1, _nodes.Count))));
            for (var index = 0; index < _nodes.Count; index++)
            {
                var row = index / columns;
                var column = index % columns;
                var x = margin + column * (nodeWidth + horizontalGap);
                var y = margin + row * (nodeHeight + verticalGap);
                _bounds[_nodes[index].Id] = new Rectangle(x, y, nodeWidth, nodeHeight);
            }

            var rows = Math.Max(1, (int)Math.Ceiling(_nodes.Count / (double)columns));
            AutoScrollMinSize = new Size(
                margin * 2 + columns * (nodeWidth + horizontalGap),
                margin * 2 + rows * (nodeHeight + verticalGap));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);

            using var edgePen = new Pen(Color.DimGray, 1.4f)
            {
                CustomEndCap = new AdjustableArrowCap(4, 5)
            };
            using var edgeTextBrush = new SolidBrush(Color.FromArgb(70, 70, 70));
            using var edgeFont = new Font(Font.FontFamily, 7.5f);

            foreach (var relationship in _relationships)
            {
                if (relationship.SourceId is null || relationship.TargetId is null ||
                    !_bounds.TryGetValue(relationship.SourceId, out var source) ||
                    !_bounds.TryGetValue(relationship.TargetId, out var target))
                    continue;

                var start = Center(source);
                var end = Center(target);
                e.Graphics.DrawLine(edgePen, start, end);

                var label = relationship.Name == "(unnamed)" ? relationship.Type : relationship.Name;
                var midpoint = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
                e.Graphics.DrawString(label, edgeFont, edgeTextBrush, midpoint);
            }

            using var fillBrush = new SolidBrush(Color.FromArgb(241, 246, 252));
            using var borderPen = new Pen(Color.FromArgb(60, 91, 125), 1.5f);
            using var titleFont = new Font(Font.FontFamily, 9f, FontStyle.Bold);
            using var typeFont = new Font(Font.FontFamily, 8f);
            using var titleBrush = new SolidBrush(Color.FromArgb(25, 35, 45));
            using var typeBrush = new SolidBrush(Color.FromArgb(85, 95, 105));

            foreach (var node in _nodes)
            {
                if (!_bounds.TryGetValue(node.Id, out var bounds))
                    continue;

                e.Graphics.FillRectangle(fillBrush, bounds);
                e.Graphics.DrawRectangle(borderPen, bounds);

                var titleArea = new Rectangle(bounds.X + 8, bounds.Y + 8, bounds.Width - 16, 34);
                var typeArea = new Rectangle(bounds.X + 8, bounds.Bottom - 25, bounds.Width - 16, 18);
                e.Graphics.DrawString(node.Name, titleFont, titleBrush, titleArea);
                e.Graphics.DrawString(node.Type, typeFont, typeBrush, typeArea);
            }
        }

        private static Point Center(Rectangle rectangle) =>
            new(rectangle.Left + rectangle.Width / 2, rectangle.Top + rectangle.Height / 2);
    }
}
