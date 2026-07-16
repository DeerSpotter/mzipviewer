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
            _canvas.SetGraph([], [], []);
        }
    }

    private void ShowSelectedDiagram()
    {
        if (_inventory is null || _diagramSelector.SelectedItem is not ModelDiagram diagram)
            return;

        var presentations = _inventory.Presentations
            .Where(p => p.DiagramId.Equals(diagram.Id, StringComparison.Ordinal))
            .ToList();
        var presentedIds = presentations.Select(p => p.ModelElementId).ToHashSet(StringComparer.Ordinal);

        var candidateNodes = presentedIds.Count > 0
            ? _inventory.Elements.Where(e => presentedIds.Contains(e.Id) && !MdzipTypeClassifier.IsRelationship(e.Type)).Take(250).ToList()
            : SelectCandidateNodes(_inventory, diagram).Take(80).ToList();

        var candidateIds = candidateNodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        var relationships = _inventory.Relationships
            .Where(r => r.SourceId is not null && r.TargetId is not null)
            .Where(r => candidateIds.Contains(r.SourceId!) && candidateIds.Contains(r.TargetId!))
            .Take(400)
            .ToList();

        var preservedNodes = presentations.Count(p => p.Bounds is not null && candidateIds.Contains(p.ModelElementId));
        var preservedRoutes = presentations.Count(p => p.RoutePoints.Count >= 2);
        _status.Text = preservedNodes > 0
            ? $"Preserved layout: {preservedNodes:N0} positioned symbols, {preservedRoutes:N0} stored routes. Missing positions use fallback layout."
            : $"Approximate preview: {candidateNodes.Count:N0} nodes, {relationships.Count:N0} connectors. No usable Cameo coordinates were found for this diagram.";

        _canvas.SetGraph(candidateNodes, relationships, presentations);
    }

    private static IEnumerable<ModelElement> SelectCandidateNodes(MdzipInventory inventory, ModelDiagram diagram)
    {
        var elements = inventory.Elements
            .Where(e => !MdzipTypeClassifier.IsRelationship(e.Type))
            .Where(e => !e.Id.Equals(diagram.Id, StringComparison.Ordinal))
            .ToList();
        var byOwner = elements.Where(e => !string.IsNullOrWhiteSpace(diagram.OwnerId) && e.OwnerId == diagram.OwnerId).ToList();
        if (byOwner.Count > 0) return byOwner;
        var sameDocument = elements.Where(e => e.Document == diagram.Document).ToList();
        return sameDocument.Count > 0 ? sameDocument : elements;
    }

    private sealed class PreviewCanvas : Panel
    {
        private IReadOnlyList<ModelElement> _nodes = [];
        private IReadOnlyList<ModelRelationship> _relationships = [];
        private IReadOnlyList<DiagramPresentation> _presentations = [];
        private readonly Dictionary<string, Rectangle> _bounds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyList<Point>> _routes = new(StringComparer.Ordinal);

        public PreviewCanvas()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        public void SetGraph(IReadOnlyList<ModelElement> nodes, IReadOnlyList<ModelRelationship> relationships, IReadOnlyList<DiagramPresentation> presentations)
        {
            _nodes = nodes;
            _relationships = relationships;
            _presentations = presentations;
            BuildLayout();
            Invalidate();
        }

        private void BuildLayout()
        {
            _bounds.Clear();
            _routes.Clear();
            const int margin = 40;
            var positioned = _presentations.Where(p => p.Bounds is not null).ToList();
            var minX = positioned.Count > 0 ? positioned.Min(p => p.Bounds!.X) : 0;
            var minY = positioned.Count > 0 ? positioned.Min(p => p.Bounds!.Y) : 0;
            var scale = DetermineScale(positioned);

            foreach (var presentation in positioned)
            {
                var b = presentation.Bounds!;
                _bounds[presentation.ModelElementId] = new Rectangle(
                    margin + (int)Math.Round((b.X - minX) * scale),
                    margin + (int)Math.Round((b.Y - minY) * scale),
                    Math.Max(30, (int)Math.Round(b.Width * scale)),
                    Math.Max(20, (int)Math.Round(b.Height * scale)));
            }

            foreach (var presentation in _presentations.Where(p => p.RoutePoints.Count >= 2))
            {
                _routes[presentation.ModelElementId] = presentation.RoutePoints
                    .Select(p => new Point(
                        margin + (int)Math.Round((p.X - minX) * scale),
                        margin + (int)Math.Round((p.Y - minY) * scale)))
                    .ToList();
            }

            const int fallbackWidth = 190;
            const int fallbackHeight = 72;
            const int gap = 45;
            var missing = _nodes.Where(n => !_bounds.ContainsKey(n.Id)).ToList();
            var right = _bounds.Count > 0 ? _bounds.Values.Max(b => b.Right) + 80 : margin;
            for (var i = 0; i < missing.Count; i++)
            {
                var row = i / 3;
                var column = i % 3;
                _bounds[missing[i].Id] = new Rectangle(right + column * (fallbackWidth + gap), margin + row * (fallbackHeight + gap), fallbackWidth, fallbackHeight);
            }

            var maxRight = _bounds.Count > 0 ? _bounds.Values.Max(b => b.Right) : 800;
            var maxBottom = _bounds.Count > 0 ? _bounds.Values.Max(b => b.Bottom) : 600;
            AutoScrollMinSize = new Size(maxRight + margin, maxBottom + margin);
        }

        private static double DetermineScale(IReadOnlyList<DiagramPresentation> positioned)
        {
            if (positioned.Count == 0) return 1;
            var maxWidth = positioned.Max(p => p.Bounds!.Width);
            return maxWidth > 1000 ? 0.1 : maxWidth > 400 ? 0.25 : 1.0;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
            using var edgePen = new Pen(Color.DimGray, 1.4f);
            using var edgeTextBrush = new SolidBrush(Color.FromArgb(70, 70, 70));
            using var edgeFont = new Font(Font.FontFamily, 7.5f);

            foreach (var relationship in _relationships)
            {
                if (relationship.SourceId is null || relationship.TargetId is null ||
                    !_bounds.TryGetValue(relationship.SourceId, out var source) ||
                    !_bounds.TryGetValue(relationship.TargetId, out var target)) continue;

                var points = _routes.TryGetValue(relationship.Id, out var stored) && stored.Count >= 2
                    ? stored
                    : new[] { Center(source), Center(target) };
                e.Graphics.DrawLines(edgePen, points.ToArray());
                DrawArrowHead(e.Graphics, edgePen.Color, points[^2], points[^1]);
                var label = relationship.Name == "(unnamed)" ? relationship.Type : relationship.Name;
                var midpoint = points[points.Count / 2];
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
                if (!_bounds.TryGetValue(node.Id, out var bounds)) continue;
                e.Graphics.FillRectangle(fillBrush, bounds);
                e.Graphics.DrawRectangle(borderPen, bounds);
                var titleArea = new Rectangle(bounds.X + 6, bounds.Y + 6, Math.Max(10, bounds.Width - 12), Math.Max(15, bounds.Height - 26));
                var typeArea = new Rectangle(bounds.X + 6, Math.Max(bounds.Y + 20, bounds.Bottom - 20), Math.Max(10, bounds.Width - 12), 16);
                e.Graphics.DrawString(node.Name, titleFont, titleBrush, titleArea);
                e.Graphics.DrawString(node.Type, typeFont, typeBrush, typeArea);
            }
        }

        private static void DrawArrowHead(Graphics graphics, Color color, Point start, Point end)
        {
            var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
            const double spread = Math.PI / 7;
            const int length = 10;
            var p1 = new Point(end.X - (int)(length * Math.Cos(angle - spread)), end.Y - (int)(length * Math.Sin(angle - spread)));
            var p2 = new Point(end.X - (int)(length * Math.Cos(angle + spread)), end.Y - (int)(length * Math.Sin(angle + spread)));
            using var brush = new SolidBrush(color);
            graphics.FillPolygon(brush, [end, p1, p2]);
        }

        private static Point Center(Rectangle rectangle) => new(rectangle.Left + rectangle.Width / 2, rectangle.Top + rectangle.Height / 2);
    }
}