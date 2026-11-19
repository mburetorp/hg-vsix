using System;
using System.Windows;
using System.Windows.Media;
using HgVsix.Core.Diff;
using HgVsix.Options;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace HgVsix.Editor
{
    // Draws change markers for the visible lines only. Positions come from the editor layout
    // so scrolling, zoom, word wrap and collapsed regions follow automatically.
    internal sealed class ChangeMargin : FrameworkElement, IWpfTextViewMargin
    {
        public const string MarginName = "HgVsix.ChangeMargin";

        private const double MarkerWidth = 4;
        private const double WedgeHalfHeight = 4;

        private readonly IWpfTextView _view;
        private readonly DocumentChangeTracker _tracker;
        private readonly IEditorFormatMap _formatMap;
        private Brush _added = Brushes.Green;
        private Brush _modified = Brushes.Blue;
        private Brush _deleted = Brushes.Red;
        private bool _disposed;

        public ChangeMargin(IWpfTextView view, DocumentChangeTracker tracker, IEditorFormatMap formatMap)
        {
            _view = view;
            _tracker = tracker;
            _formatMap = formatMap;
            Width = MarkerWidth + 1;
            ClipToBounds = true;
            IsHitTestVisible = false;

            LoadBrushes();
            UpdateVisibility();

            _view.LayoutChanged += OnLayoutChanged;
            _view.Closed += OnClosed;
            _tracker.ChangesUpdated += OnChangesUpdated;
            _formatMap.FormatMappingChanged += OnFormatMappingChanged;
            HgVsixSettings.Instance.Changed += OnSettingsChanged;
        }

        public FrameworkElement VisualElement => this;

        public double MarginSize => ActualWidth;

        public bool Enabled => !_disposed;

        public ITextViewMargin? GetTextViewMargin(string marginName) =>
            string.Equals(marginName, MarginName, StringComparison.OrdinalIgnoreCase) ? this : null;

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _view.LayoutChanged -= OnLayoutChanged;
            _view.Closed -= OnClosed;
            _tracker.ChangesUpdated -= OnChangesUpdated;
            _formatMap.FormatMappingChanged -= OnFormatMappingChanged;
            HgVsixSettings.Instance.Changed -= OnSettingsChanged;
            _tracker.Release();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            var map = _tracker.Map;
            var mapSnapshot = _tracker.MapSnapshot;
            if (_disposed || _view.IsClosed || _view.InLayout || mapSnapshot == null || map.IsEmpty)
            {
                return;
            }

            var lastLine = map.LineCount - 1;
            foreach (var line in _view.TextViewLines)
            {
                if (!TryGetLineRange(line, mapSnapshot, out var first, out var last))
                {
                    continue;
                }

                var top = line.Top - _view.ViewportTop;
                var bottom = top + line.Height;
                LineChange change;
                if (first == last)
                {
                    change = map.GetLineChange(first);
                }
                else
                {
                    // Collapsed region: one marker for all hidden lines
                    change = map.GetRangeChange(first, last, out var innerDeletion);
                    if (innerDeletion)
                    {
                        DrawWedge(drawingContext, (top + bottom) / 2);
                    }
                }

                if (change != LineChange.None)
                {
                    drawingContext.DrawRectangle(change == LineChange.Added ? _added : _modified, null, new Rect(0, top, MarkerWidth, line.Height));
                }

                if (line.IsFirstTextViewLineForSnapshotLine && map.HasDeletionBefore(first))
                {
                    DrawWedge(drawingContext, top);
                }

                if (line.IsLastTextViewLineForSnapshotLine && last == lastLine && map.HasDeletionBefore(map.LineCount))
                {
                    DrawWedge(drawingContext, bottom);
                }
            }
        }

        // Maps a view line to line numbers in the snapshot the change map was built from
        private bool TryGetLineRange(ITextViewLine line, ITextSnapshot mapSnapshot, out int first, out int last)
        {
            first = last = -1;
            var start = ToMapSnapshot(line.Start, mapSnapshot);
            var end = ToMapSnapshot(line.End, mapSnapshot);
            if (start == null || end == null)
            {
                return false;
            }

            first = start.Value.GetContainingLine().LineNumber;
            last = Math.Max(first, end.Value.GetContainingLine().LineNumber);
            return true;
        }

        private SnapshotPoint? ToMapSnapshot(SnapshotPoint viewPoint, ITextSnapshot mapSnapshot)
        {
            var point = viewPoint.Snapshot.TextBuffer == mapSnapshot.TextBuffer
                ? viewPoint
                : _view.BufferGraph.MapDownToBuffer(viewPoint, PointTrackingMode.Negative, mapSnapshot.TextBuffer, PositionAffinity.Successor);
            return point?.TranslateTo(mapSnapshot, PointTrackingMode.Negative);
        }

        // Small triangle at a line boundary where lines were removed
        private void DrawWedge(DrawingContext drawingContext, double y)
        {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(0, y - WedgeHalfHeight), true, true);
                context.LineTo(new Point(Width, y), false, false);
                context.LineTo(new Point(0, y + WedgeHalfHeight), false, false);
            }

            geometry.Freeze();
            drawingContext.DrawGeometry(_deleted, null, geometry);
        }

        private void LoadBrushes()
        {
            _added = GetBrush(ChangeFormatNames.Added, _added);
            _modified = GetBrush(ChangeFormatNames.Modified, _modified);
            _deleted = GetBrush(ChangeFormatNames.Deleted, _deleted);
        }

        private Brush GetBrush(string formatName, Brush fallback)
        {
            var properties = _formatMap.GetProperties(formatName);
            if (properties.Contains(EditorFormatDefinition.BackgroundBrushId) && properties[EditorFormatDefinition.BackgroundBrushId] is Brush brush)
            {
                return brush;
            }

            if (properties.Contains(EditorFormatDefinition.BackgroundColorId) && properties[EditorFormatDefinition.BackgroundColorId] is Color color)
            {
                var solid = new SolidColorBrush(color);
                solid.Freeze();
                return solid;
            }

            return fallback;
        }

        private void UpdateVisibility()
        {
            var settings = HgVsixSettings.Instance;
            Visibility = settings.Enabled && settings.ShowMargin && _tracker.RepositoryRoot != null
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e) => InvalidateVisual();

        private void OnChangesUpdated(object sender, EventArgs e)
        {
            UpdateVisibility();
            InvalidateVisual();
        }

        private void OnFormatMappingChanged(object sender, FormatItemsEventArgs e)
        {
            LoadBrushes();
            InvalidateVisual();
        }

        private void OnSettingsChanged(object sender, EventArgs e)
        {
            UpdateVisibility();
            InvalidateVisual();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Dispose();
        }
    }
}
