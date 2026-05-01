using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AnnoMapEditor.DataArchives;
using AnnoMapEditor.MapTemplates.Models;
using AnnoMapEditor.MapTemplates.Enums;
using Avalonia.Layout;
using AnnoMapEditor.Utilities;
using AnnoMapEditor.Utilities.UndoRedo;

namespace AnnoMapEditor.UI.Avalonia.Controls
{
    public partial class MapView : UserControl
    {
        private Canvas? _canvas;
        private LayoutTransformControl? _zoomHost;
        private ScrollViewer? _scroller;
        private TextBlock? _zoomLabel;
        private TextBlock? _emptyHint;
        private double _scale = 1.0;
        private MapTemplate? _map;

        private static readonly IBrush OceanBrush         = new SolidColorBrush(Color.Parse("#082236"));
        private static readonly IBrush PlayableAreaBrush  = new SolidColorBrush(Color.Parse("#11375A"));
        private static readonly IBrush PlayableAreaStroke = new SolidColorBrush(Color.Parse("#1F5F95"));
        private static readonly IBrush RomanIslandBrush   = new SolidColorBrush(Color.Parse("#C97543"));
        private static readonly IBrush CelticIslandBrush  = new SolidColorBrush(Color.Parse("#5DA760"));
        private static readonly IBrush VolcanicBrush      = new SolidColorBrush(Color.Parse("#A53A2C"));
        private static readonly IBrush FixedIslandBrush   = new SolidColorBrush(Color.Parse("#E0C26A"));
        private static readonly IBrush IslandStroke       = new SolidColorBrush(Color.Parse("#1A1A1A"));
        private static readonly IBrush StartSpotFill      = new SolidColorBrush(Color.Parse("#3FA7E6"));
        private static readonly IBrush StartSpotStroke    = Brushes.White;
        private static readonly IBrush LabelBrush         = Brushes.White;

        // NPC-specific
        private static readonly IBrush ThirdPartyBrush    = new SolidColorBrush(Color.Parse("#F5C842")); // gold
        private static readonly IBrush PirateBrush        = new SolidColorBrush(Color.Parse("#8B1A1A")); // dark red
        private static readonly IBrush DecorationBrush    = new SolidColorBrush(Color.Parse("#5A6878")); // grey-blue
        private static readonly IBrush StarterBrush       = new SolidColorBrush(Color.Parse("#6FE07F")); // bright green

        public MapView()
        {
            InitializeComponent();
            Cursor = new Cursor(StandardCursorType.Hand);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _canvas = this.FindControl<Canvas>("MapCanvas");
            _zoomHost = this.FindControl<LayoutTransformControl>("ZoomHost");
            _scroller = this.FindControl<ScrollViewer>("Scroller");
            _zoomLabel = this.FindControl<TextBlock>("ZoomLabel");
            _emptyHint = this.FindControl<TextBlock>("EmptyHint");

            // Wheel must intercept the ScrollViewer → tunnel + handled events too.
            AddHandler(PointerWheelChangedEvent, OnPointerWheel,
                RoutingStrategies.Tunnel, handledEventsToo: true);
            // Pointer press/move/release are normal bubble so e.Source stays the deepest hit.
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            UpdateZoomLabel();
        }

        private bool _isPanning;
        private bool _isDraggingElement;
        private Point _panStartPointer;
        private Vector _panStartOffset;
        private const double DragThresholdPx = 4;
        private const int SnapTiles = 8;
        private bool _potentialClick;
        private MapElement? _pressedElement;
        private Control? _pressedVisual;
        private Point _dragOffsetInVisual;
        private Vector2 _dragOriginalPosition = Vector2.Zero;
        private Control? _selectedVisual;
        private MapElement? _selectedElement;
        private int _currentMapHeight;
        private static readonly IBrush SelectionStroke = new SolidColorBrush(Color.Parse("#FFEB3B"));

        public event EventHandler<MapElement?>? ElementSelected;
        public event EventHandler<MapElement>? ElementMoved;

        public void SetMap(MapTemplate? map)
        {
            _map = map;
            Render();
            // Defer the fit until the ScrollViewer has its real bounds (next layout pass).
            Dispatcher.UIThread.Post(FitToViewport, DispatcherPriority.Background);
        }

        /// <summary>
        /// Selects the given element from outside (e.g. tree-view click). Walks the canvas
        /// children to find the matching visual (each visual has Tag = its MapElement).
        /// </summary>
        public void SelectElement(MapElement? element)
        {
            if (_canvas is null) { Select(element, null); return; }
            Control? visual = null;
            if (element is not null)
            {
                foreach (Control child in _canvas.Children.OfType<Control>())
                {
                    if (ReferenceEquals(child.Tag, element)) { visual = child; break; }
                }
            }
            Select(element, visual);
        }

        public void RefreshElementPositions()
        {
            if (_canvas is null || _map is null) return;
            int h = _map.Size.Y;
            foreach (Control child in _canvas.Children.OfType<Control>())
            {
                if (child.Tag is not MapElement el) continue;
                int size = el is IslandElement isl && isl.SizeInTiles > 0 ? isl.SizeInTiles : 24;
                Canvas.SetLeft(child, el.Position.X);
                Canvas.SetTop(child, h - el.Position.Y - size);
            }
        }

        public void FitToViewport()
        {
            if (_scroller is null || _map is null || _map.Size.X <= 0)
                return;
            double availW = _scroller.Bounds.Width - 16;
            double availH = _scroller.Bounds.Height - 16;
            if (availW <= 0 || availH <= 0)
            {
                // Viewport not laid out yet; retry shortly.
                Dispatcher.UIThread.Post(FitToViewport, DispatcherPriority.Background);
                return;
            }
            double fit = Math.Min(availW / _map.Size.X, availH / _map.Size.Y);
            _scale = Math.Clamp(fit, 0.1, 8.0);
            ApplyZoom();
        }

        private void Render()
        {
            if (_canvas is null) return;
            _canvas.Children.Clear();

            if (_map is null || _map.Size.X <= 0)
            {
                _canvas.Width = 0;
                _canvas.Height = 0;
                if (_emptyHint != null) _emptyHint.IsVisible = true;
                return;
            }

            if (_emptyHint != null) _emptyHint.IsVisible = false;

            int w = _map.Size.X;
            int h = _map.Size.Y;
            _currentMapHeight = h;

            _canvas.Width = w;
            _canvas.Height = h;

            // Playable area
            if (_map.PlayableArea.Width > 0 && _map.PlayableArea.Height > 0)
            {
                Rect2 pa = _map.PlayableArea;
                var rect = new Rectangle
                {
                    Width = pa.Width,
                    Height = pa.Height,
                    Fill = PlayableAreaBrush,
                    Stroke = PlayableAreaStroke,
                    StrokeThickness = 1.5,
                    StrokeDashArray = new AvaloniaList<double> { 4, 3 }
                };
                Canvas.SetLeft(rect, pa.X);
                // Anno's Y axis grows upward; UI's Y axis grows downward.
                Canvas.SetTop(rect, h - pa.Y - pa.Height);
                _canvas.Children.Add(rect);
            }

            // Islands and starting spots
            foreach (MapElement element in _map.Elements)
            {
                Control? shape = BuildElementVisual(element, h);
                if (shape != null)
                    _canvas.Children.Add(shape);
            }

            // Randomly placed NPCs (campaign traders, pirate hubs)
            foreach (NPCPlacement npc in _map.NPCPlacements)
            {
                Control marker = BuildNpcMarker(npc, h);
                _canvas.Children.Add(marker);
            }

            ApplyZoom();
        }

        private static readonly IBrush NpcFlagBrush = new SolidColorBrush(Color.Parse("#FFD54F"));
        private static readonly IBrush NpcFlagStroke = new SolidColorBrush(Color.Parse("#5D4037"));

        private static Control BuildNpcMarker(NPCPlacement npc, int mapHeight)
        {
            const double markerSize = 18;
            // Diamond-shape (rotated square) for NPC: distinct from islands and starting spots.
            var diamond = new Rectangle
            {
                Width = markerSize,
                Height = markerSize,
                Fill = NpcFlagBrush,
                Stroke = NpcFlagStroke,
                StrokeThickness = 2,
                RenderTransform = new RotateTransform(45),
                RenderTransformOrigin = RelativePoint.Center
            };
            var label = new TextBlock
            {
                Text = "★",
                Foreground = NpcFlagStroke,
                FontWeight = FontWeight.Bold,
                FontSize = 11,
                IsHitTestVisible = false
            };
            var group = new Canvas { Width = 0, Height = 0 };
            Canvas.SetLeft(diamond, npc.Position.X - markerSize / 2);
            Canvas.SetTop(diamond, mapHeight - npc.Position.Y - markerSize / 2);
            Canvas.SetLeft(label, npc.Position.X - 4);
            Canvas.SetTop(label, mapHeight - npc.Position.Y - 8);
            group.Children.Add(diamond);
            group.Children.Add(label);
            ToolTip.SetTip(group, $"NPC placement (GUID {npc.Guid})");
            return group;
        }

        private Control? BuildElementVisual(MapElement element, int mapHeight)
        {
            switch (element)
            {
                case StartingSpotElement spot:
                {
                    const double radius = 12;
                    const double size = radius * 2;
                    // Tag goes on the group ONLY — that way the drag code moves the whole
                    // group, taking the number label along with the circle. (Previously the
                    // ellipse had its own Tag, so FindElementAt returned the ellipse and only
                    // the circle moved while the digit stayed put.)
                    var ellipse = new Ellipse
                    {
                        Width = size,
                        Height = size,
                        Fill = StartSpotFill,
                        Stroke = StartSpotStroke,
                        StrokeThickness = 2
                    };
                    Canvas.SetLeft(ellipse, 0);
                    Canvas.SetTop(ellipse, 0);
                    var label = new TextBlock
                    {
                        Text = spot.Index >= 0 ? (spot.Index + 1).ToString() : "?",
                        Foreground = LabelBrush,
                        FontWeight = FontWeight.Bold,
                        FontSize = 11,
                        IsHitTestVisible = false,
                        Width = size,
                        TextAlignment = TextAlignment.Center
                    };
                    Canvas.SetLeft(label, 0);
                    Canvas.SetTop(label, 5);
                    var group = new Canvas
                    {
                        Width = size,
                        Height = size,
                        // Transparent background makes the whole 24x24 area hit-testable.
                        Background = Brushes.Transparent,
                        Tag = spot
                    };
                    group.Children.Add(ellipse);
                    group.Children.Add(label);
                    Canvas.SetLeft(group, spot.Position.X - radius);
                    Canvas.SetTop(group, mapHeight - spot.Position.Y - radius);
                    return group;
                }
                case IslandElement island:
                {
                    // Continental islands have SizeInTiles = int.MaxValue. Clamp to a sensible visual size:
                    // 1) at most the smallest map dimension
                    // 2) at least 24 tiles for visibility
                    int rawSize = island.SizeInTiles > 0 ? island.SizeInTiles : 32;
                    int maxVisualSize = _map is not null
                        ? Math.Min(_map.Size.X, _map.Size.Y)
                        : 1024;
                    int size = Math.Clamp(rawSize, 24, maxVisualSize);
                    IBrush fill = ResolveIslandBrush(island);
                    string? badge = ResolveIslandBadge(island);

                    Bitmap? thumbnail = TryGetThumbnail(island);
                    Control visual;

                    if (thumbnail is not null)
                    {
                        var img = new Image
                        {
                            Width = size,
                            Height = size,
                            Source = thumbnail,
                            Stretch = Stretch.Uniform
                        };
                        var border = new Border
                        {
                            Width = size,
                            Height = size,
                            Background = Brushes.Transparent,
                            BorderBrush = fill,
                            BorderThickness = new Thickness(1.5),
                            CornerRadius = new CornerRadius(2),
                            Child = img,
                            RenderTransformOrigin = RelativePoint.Center
                        };
                        // Apply rotation for FixedIslandElement
                        if (island is FixedIslandElement fixedI && fixedI.Rotation is byte r)
                            border.RenderTransform = new RotateTransform(r * 90);
                        visual = border;
                    }
                    else
                    {
                        // No thumbnail: outline only, with diagonal hatch hint for placeholders.
                        // Continental/runtime placeholders are typically large; keep them subtle.
                        bool isLargePlaceholder = rawSize > 256;
                        visual = new Rectangle
                        {
                            Width = size,
                            Height = size,
                            Fill = Brushes.Transparent,
                            Stroke = fill,
                            StrokeThickness = isLargePlaceholder ? 1.5 : 1.5,
                            StrokeDashArray = isLargePlaceholder
                                ? new AvaloniaList<double> { 6, 4 }
                                : null,
                            RadiusX = 3,
                            RadiusY = 3,
                            Opacity = isLargePlaceholder ? 0.55 : 0.85
                        };
                    }

                    Canvas.SetLeft(visual, island.Position.X);
                    Canvas.SetTop(visual, mapHeight - island.Position.Y - size);
                    if (!string.IsNullOrEmpty(island.Label))
                        ToolTip.SetTip(visual, island.Label);
                    visual.Tag = island;

                    // Attach a badge for NPC types (T = trader/ThirdParty, P = pirate, etc.)
                    if (badge != null)
                    {
                        var badgeText = new Border
                        {
                            Background = fill,
                            CornerRadius = new CornerRadius(8),
                            Padding = new Thickness(5, 1),
                            HorizontalAlignment = HorizontalAlignment.Right,
                            VerticalAlignment = VerticalAlignment.Top,
                            IsHitTestVisible = false,
                            Child = new TextBlock
                            {
                                Text = badge,
                                Foreground = Brushes.White,
                                FontWeight = FontWeight.Bold,
                                FontSize = 10
                            }
                        };
                        Canvas.SetLeft(badgeText, island.Position.X + size - 18);
                        Canvas.SetTop(badgeText, mapHeight - island.Position.Y - size + 2);
                        // We add the badge to the canvas separately so it isn't rotated with the island.
                        if (_canvas != null) _canvas.Children.Add(badgeText);
                    }

                    return visual;
                }
            }
            return null;
        }

        private static Bitmap? TryGetThumbnail(IslandElement island)
        {
            if (island is FixedIslandElement fixedIsland)
                return fixedIsland.IslandAsset?.Thumbnail;

            // Random islands resolve their actual visual at game runtime.
            // We can still hint at it by picking the first matching pool island thumbnail.
            if (island is RandomIslandElement random && DataManager.Instance.IsInitialized)
            {
                try
                {
                    foreach (var fixedAsset in DataManager.Instance.FixedIslandRepository)
                    {
                        if (fixedAsset.Thumbnail is null) continue;
                        if (random.IslandSize is not null
                            && fixedAsset.SizeInTiles >= random.IslandSize.DefaultSizeInTiles - 32
                            && fixedAsset.SizeInTiles <= random.IslandSize.DefaultSizeInTiles + 32)
                        {
                            return fixedAsset.Thumbnail;
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        private static IBrush ResolveIslandBrush(IslandElement element)
        {
            // IslandType wins over label-based detection: NPC types have dedicated colors.
            IslandType? type = element.IslandType;
            if (type == IslandType.ThirdParty) return ThirdPartyBrush;
            if (type == IslandType.PirateIsland) return PirateBrush;
            if (type == IslandType.Decoration) return DecorationBrush;
            if (type == IslandType.Starter) return StarterBrush;
            if (type == IslandType.VolcanicIsland) return VolcanicBrush;

            // Normal/Cliff/null: keep the region/biome detection from the label
            if (element is FixedIslandElement)
                return FixedIslandBrush;
            string lowered = (element.Label ?? "").ToLowerInvariant();
            if (lowered.Contains("celtic"))
                return CelticIslandBrush;
            if (lowered.Contains("volcanic"))
                return VolcanicBrush;
            return RomanIslandBrush;
        }

        private static string? ResolveIslandBadge(IslandElement element)
        {
            IslandType? type = element.IslandType;
            if (type == IslandType.ThirdParty) return "T";
            if (type == IslandType.PirateIsland) return "P";
            if (type == IslandType.Decoration) return "D";
            if (type == IslandType.Starter) return "S";
            if (type == IslandType.VolcanicIsland) return "V";
            return null;
        }

        private void OnPointerWheel(object? sender, PointerWheelEventArgs e)
        {
            if (_scroller is null || _canvas is null || _map is null || _map.Size.X <= 0)
                return;

            // Position of the cursor in unzoomed canvas coordinates BEFORE the zoom change
            Point cursorInScroller = e.GetPosition(_scroller);
            double oldScale = _scale;
            double mapX = (_scroller.Offset.X + cursorInScroller.X) / oldScale;
            double mapY = (_scroller.Offset.Y + cursorInScroller.Y) / oldScale;

            double factor = e.Delta.Y > 0 ? 1.15 : 1 / 1.15;
            _scale = Math.Clamp(_scale * factor, 0.05, 12.0);
            ApplyZoom();

            // Re-center so the same map point stays under the cursor (focal zoom)
            Dispatcher.UIThread.Post(() =>
            {
                if (_scroller is null) return;
                double newOffsetX = mapX * _scale - cursorInScroller.X;
                double newOffsetY = mapY * _scale - cursorInScroller.Y;
                _scroller.Offset = new Vector(
                    Math.Max(0, newOffsetX),
                    Math.Max(0, newOffsetY));
            }, DispatcherPriority.Render);

            e.Handled = true;
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var props = e.GetCurrentPoint(_scroller);
            if (!(props.Properties.IsLeftButtonPressed
                  || props.Properties.IsMiddleButtonPressed
                  || props.Properties.IsRightButtonPressed))
                return;

            // Detect if the press happened on a map element (walk up the visual tree).
            _pressedElement = FindElementAt(e.Source as Control, out _pressedVisual);
            _potentialClick = props.Properties.IsLeftButtonPressed && _pressedElement != null;

            // Remember the offset between pointer and the visual's top-left, in map-tile coords.
            if (_pressedElement != null && _pressedVisual != null && _canvas != null)
            {
                Point pInCanvas = e.GetPosition(_canvas);
                _dragOffsetInVisual = new Point(
                    pInCanvas.X - Canvas.GetLeft(_pressedVisual),
                    pInCanvas.Y - Canvas.GetTop(_pressedVisual));
                _dragOriginalPosition = new Vector2(_pressedElement.Position);
            }

            _panStartPointer = e.GetPosition(_scroller);
            _panStartOffset = _scroller?.Offset ?? new Vector();
            _isPanning = false;
            _isDraggingElement = false;
            e.Pointer.Capture(this);
            e.Handled = true;
        }

        private static MapElement? FindElementAt(Control? source, out Control? visual)
        {
            visual = null;
            for (Control? c = source; c is not null; c = c.Parent as Control)
            {
                if (c.Tag is MapElement me)
                {
                    visual = c;
                    return me;
                }
            }
            return null;
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_scroller is null) return;
            if (!e.Pointer.Captured?.Equals(this) ?? true)
                return;

            Point pScroller = e.GetPosition(_scroller);
            Vector delta = pScroller - _panStartPointer;

            // First time we cross the threshold, decide: drag-element or pan.
            if (!_isPanning && !_isDraggingElement && delta.Length > DragThresholdPx)
            {
                _potentialClick = false;
                bool leftButton = e.GetCurrentPoint(_scroller).Properties.IsLeftButtonPressed;
                bool isMovableElement = _pressedElement is IslandElement
                                        || _pressedElement is StartingSpotElement;

                if (leftButton && isMovableElement && _selectedElement == _pressedElement)
                {
                    _isDraggingElement = true;
                    Cursor = new Cursor(StandardCursorType.Hand);
                }
                else
                {
                    _isPanning = true;
                    Cursor = new Cursor(StandardCursorType.DragMove);
                }
            }

            if (_isDraggingElement && _pressedElement != null && _pressedVisual != null && _canvas != null)
            {
                MoveElementToPointer(e);
                e.Handled = true;
            }
            else if (_isPanning)
            {
                _scroller.Offset = new Vector(
                    Math.Max(0, _panStartOffset.X - delta.X),
                    Math.Max(0, _panStartOffset.Y - delta.Y));
                e.Handled = true;
            }
        }

        private void MoveElementToPointer(PointerEventArgs e)
        {
            if (_pressedElement is null || _pressedVisual is null || _canvas is null || _map is null)
                return;

            Point pInCanvas = e.GetPosition(_canvas);
            // SizeInTiles can be int.MaxValue for "continental" islands — that overflows
            // _map.Size.X - size into a huge negative and breaks Math.Clamp. Cap it to the map.
            int rawSize = _pressedElement is IslandElement isl ? isl.SizeInTiles : 24;
            int size = Math.Clamp(rawSize > 0 ? rawSize : 24, 24, Math.Max(24, _map.Size.X));
            // visual top-left in canvas coords
            double newCanvasLeft = pInCanvas.X - _dragOffsetInVisual.X;
            double newCanvasTop  = pInCanvas.Y - _dragOffsetInVisual.Y;

            int snap = SnapTiles;
            int snappedX = (int)Math.Round(newCanvasLeft / snap) * snap;
            int snappedTopY = (int)Math.Round(newCanvasTop / snap) * snap;

            // Clamp inside map. Guard against degenerate maps where size > Size, which would
            // make maxX/maxY negative and crash Math.Clamp(min > max).
            int maxX = Math.Max(0, _map.Size.X - size);
            int maxY = Math.Max(0, _map.Size.Y - size);
            snappedX = Math.Clamp(snappedX, 0, maxX);
            snappedTopY = Math.Clamp(snappedTopY, 0, maxY);

            Canvas.SetLeft(_pressedVisual, snappedX);
            Canvas.SetTop(_pressedVisual, snappedTopY);

            // Convert UI Y back to Anno's Y-up convention
            int annoY = _currentMapHeight - snappedTopY - size;

            _pressedElement.Position = new Vector2(snappedX, annoY);
            ElementMoved?.Invoke(this, _pressedElement);
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_potentialClick && _pressedElement != null)
            {
                Select(_pressedElement, _pressedVisual);
            }
            else if (_pressedElement is null && !_isPanning && !_isDraggingElement
                     && e.InitialPressMouseButton == MouseButton.Left)
            {
                // Empty click on background → clear selection
                Select(null, null);
            }
            else if (_isDraggingElement && _pressedElement != null)
            {
                // Push to undo stack only if the position actually changed.
                Vector2 finalPos = new(_pressedElement.Position);
                if (!Vector2.Equals(finalPos, _dragOriginalPosition))
                {
                    UndoRedoStack.Instance.Do(
                        new MapElementTransformStackEntry(_pressedElement, _dragOriginalPosition, finalPos));
                }
                ElementSelected?.Invoke(this, _pressedElement);
            }

            _isPanning = false;
            _isDraggingElement = false;
            _potentialClick = false;
            _pressedElement = null;
            _pressedVisual = null;
            Cursor = new Cursor(StandardCursorType.Hand);
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        private void Select(MapElement? element, Control? visual)
        {
            // Restore previous visual
            if (_selectedVisual is Border prevBorder && _selectedElement is IslandElement prevIsland)
            {
                prevBorder.BorderBrush = ResolveIslandBrush(prevIsland);
                prevBorder.BorderThickness = new Thickness(1.5);
            }
            else if (_selectedVisual is Rectangle prevRect)
            {
                prevRect.Stroke = IslandStroke;
                prevRect.StrokeThickness = 1;
            }
            else if (_selectedVisual is Ellipse prevEllipse)
            {
                prevEllipse.Stroke = StartSpotStroke;
                prevEllipse.StrokeThickness = 2;
            }

            _selectedElement = element;
            _selectedVisual = visual;

            // Highlight new
            if (visual is Border border)
            {
                border.BorderBrush = SelectionStroke;
                border.BorderThickness = new Thickness(2.5);
            }
            else if (visual is Rectangle rect)
            {
                rect.Stroke = SelectionStroke;
                rect.StrokeThickness = 2.5;
            }
            else if (visual is Ellipse ellipse)
            {
                ellipse.Stroke = SelectionStroke;
                ellipse.StrokeThickness = 3;
            }

            ElementSelected?.Invoke(this, element);
        }

        private void OnZoomIn(object? sender, RoutedEventArgs e)
        {
            _scale = Math.Clamp(_scale * 1.25, 0.1, 8.0);
            ApplyZoom();
        }

        private void OnZoomOut(object? sender, RoutedEventArgs e)
        {
            _scale = Math.Clamp(_scale / 1.25, 0.1, 8.0);
            ApplyZoom();
        }

        private void OnZoomReset(object? sender, RoutedEventArgs e)
        {
            FitToViewport();
        }

        private void ApplyZoom()
        {
            if (_zoomHost is null) return;
            _zoomHost.LayoutTransform = new ScaleTransform(_scale, _scale);
            UpdateZoomLabel();
        }

        private void UpdateZoomLabel()
        {
            if (_zoomLabel != null)
                _zoomLabel.Text = $"{_scale * 100:F0}%";
        }
    }
}
