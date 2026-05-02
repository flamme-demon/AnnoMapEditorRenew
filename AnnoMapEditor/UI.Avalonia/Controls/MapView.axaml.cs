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
        // Two-tone playable area: the current PA gets a vivid teal so it pops against the dark
        // ocean background, the initial PA (DLC1 expanded reference rectangle) gets a paler
        // green so the two zones can be distinguished at a glance without overlapping noise.
        private static readonly IBrush PlayableAreaBrush  = new SolidColorBrush(Color.FromArgb(64, 0x4F, 0xC3, 0xF7));   // teal w/ alpha
        private static readonly IBrush PlayableAreaStroke = new SolidColorBrush(Color.Parse("#4FC3F7"));
        private static readonly IBrush InitialPaBrush     = new SolidColorBrush(Color.FromArgb(40, 0x9C, 0xCC, 0x65));   // green w/ alpha
        private static readonly IBrush InitialPaStroke    = new SolidColorBrush(Color.Parse("#9CCC65"));
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
            // Restore the saved orientation so the map opens at the same angle the user set
            // last session (persisted globally via UserSettings.MapViewRotationDeg).
            UpdateOrientationLabel();
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
        /// <summary>Raised once at pointer-release after a drag actually changed an
        /// element's position. Subscribers can rerun expensive things (validation,
        /// tree rebuild) without paying for them on every dragged frame.</summary>
        public event EventHandler<MapElement>? ElementMoveCommitted;
        /// <summary>
        /// Fires while the user drags a playable-area handle. Carries the live
        /// (x1, y1, x2, y2) margins so the right-pane editor can update its NumericUpDowns.
        /// </summary>
        public event EventHandler<(int x1, int y1, int x2, int y2)>? PlayableAreaResizing;
        /// <summary>
        /// Fires once on pointer release after a handle drag, with the committed margins.
        /// Subscribers should record an undo entry and persist via ResizeAndCommitMapTemplate.
        /// </summary>
        public event EventHandler<(int x1, int y1, int x2, int y2, int oldX1, int oldY1, int oldX2, int oldY2)>? PlayableAreaResized;

        // Drag-resize state for the playable area.
        private enum PaHandle { NW, N, NE, W, E, SW, S, SE }
        private Rectangle? _paRect;
        private readonly Dictionary<PaHandle, Ellipse> _paHandles = new();
        private PaHandle? _draggingPaHandle;
        private (int x1, int y1, int x2, int y2) _paAtDragStart;
        private (int x1, int y1, int x2, int y2) _paLive;
        private const double PaHandleRadius = 8;
        private static readonly IBrush PaHandleFill = new SolidColorBrush(Color.Parse("#FFEB3B"));
        private static readonly IBrush PaHandleStroke = Brushes.Black;

        private bool _editPlayableArea;
        // All island label badges, kept in sync with the zoom level so the text stays
        // readable (~12 px on screen) at any scale.
        private readonly List<Border> _islandLabels = new();
        // Reverse lookup so we can move a badge along when the user drags its island.
        private readonly Dictionary<MapElement, Border> _labelByElement = new();
        /// <summary>When false (default), the 8 yellow resize handles are hidden so the canvas
        /// stays uncluttered. Toggled by the "Edit playable area" switch in the side panel.</summary>
        public bool EditPlayableArea
        {
            get => _editPlayableArea;
            set
            {
                if (_editPlayableArea == value) return;
                _editPlayableArea = value;
                foreach (var h in _paHandles.Values)
                    h.IsVisible = value;
            }
        }

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
            _islandLabels.Clear();
            _labelByElement.Clear();

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

            // Initial playable area (DLC1 expanded reference frame) drawn FIRST so the actual
            // PA rectangle can paint over it where they overlap. Vanilla DLC1 always emits
            // (20, 20, 2020, 2020) — the original 2048-tile PA — alongside the expanded one.
            Rect2 initPa = _map.InitialPlayableArea;
            if (initPa.Width > 0 && initPa.Height > 0)
            {
                var initRect = new Rectangle
                {
                    Width = initPa.Width,
                    Height = initPa.Height,
                    Fill = InitialPaBrush,
                    Stroke = InitialPaStroke,
                    StrokeThickness = 1.5,
                    StrokeDashArray = new AvaloniaList<double> { 4, 3 },
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(initRect, initPa.X);
                Canvas.SetTop(initRect, h - initPa.Y - initPa.Height);
                _canvas.Children.Add(initRect);
            }

            // Playable area + 8 resize handles.
            _paRect = null;
            _paHandles.Clear();
            if (_map.PlayableArea.Width > 0 && _map.PlayableArea.Height > 0)
            {
                Rect2 pa = _map.PlayableArea;
                _paRect = new Rectangle
                {
                    Width = pa.Width,
                    Height = pa.Height,
                    Fill = PlayableAreaBrush,
                    Stroke = PlayableAreaStroke,
                    StrokeThickness = 1.5,
                    StrokeDashArray = new AvaloniaList<double> { 4, 3 }
                };
                Canvas.SetLeft(_paRect, pa.X);
                // Anno's Y axis grows upward; UI's Y axis grows downward.
                Canvas.SetTop(_paRect, h - pa.Y - pa.Height);
                _canvas.Children.Add(_paRect);

                CreatePaHandles();
                LayoutPaHandles();
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
                        // Apply rotation for FixedIslandElement.
                        // Negative sign: the editor uses (binary[0], binary[1]) directly for
                        // (Position.X, Position.Y) — same convention as the .a7tinfo. With that
                        // mapping plus Avalonia's screen-Y-down convention, a clockwise rotation
                        // r in game-space appears as counter-clockwise on screen, so we negate r.
                        if (island is FixedIslandElement fixedI && fixedI.Rotation is byte r)
                            border.RenderTransform = new RotateTransform(-r * 90);

                        // Two kinds of overlay we want to show on top of the thumbnail:
                        //   - "Frontière" (random Large/ExtraLarge/Continental) → yellow dashed cadre
                        //   - "Zone" (random without a <Size> tag = generic placement spot) →
                        //     cyan dashed cadre (high contrast over the dark blue map background)
                        bool isFrontier = island is RandomIslandElement && rawSize > 320;
                        bool isZonePlaceholder = island is RandomIslandElement r2 && !r2.HasExplicitSize;
                        // Debug aid: surface the zone count once so we can tell whether the
                        // detection itself is firing. (Goes to the app log, not the UI.)
                        if (isZonePlaceholder)
                            System.Diagnostics.Debug.WriteLine(
                                $"[MapView] zone placeholder @ {island.Position.X},{island.Position.Y} size={rawSize}");
                        if (isFrontier || isZonePlaceholder)
                        {
                            var overlay = new Rectangle
                            {
                                Width  = size,
                                Height = size,
                                Fill   = Brushes.Transparent,
                                Stroke = new SolidColorBrush(isFrontier
                                    ? Color.Parse("#FFD740")    // yellow — frontière
                                    : Color.Parse("#00E5FF")),  // cyan flashy — zone placeholder
                                StrokeThickness = 3,
                                StrokeDashArray = new AvaloniaList<double> { 6, 4 },
                                RadiusX = 3,
                                RadiusY = 3,
                                IsHitTestVisible = false
                            };
                            var stack = new Grid { Width = size, Height = size };
                            stack.Children.Add(border);
                            stack.Children.Add(overlay);
                            visual = stack;
                        }
                        else
                        {
                            visual = border;
                        }
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

                    // Type/size badge centered on the island — pill-shape with a translucent
                    // black background so the label stays legible over thumbnails of any color.
                    // The label's FontSize is recomputed on every zoom change so the on-screen
                    // text stays ~12 px tall regardless of the canvas scale.
                    string? labelText = BuildIslandLabel(island);
                    if (labelText is not null && _canvas is not null)
                    {
                        var labelTb = new TextBlock
                        {
                            Text = labelText,
                            Foreground = Brushes.White,
                            FontWeight = FontWeight.SemiBold,
                            TextAlignment = TextAlignment.Center,
                            IsHitTestVisible = false
                        };
                        var labelBg = new Border
                        {
                            Background = new SolidColorBrush(Color.FromArgb(190, 0, 0, 0)),
                            CornerRadius = new CornerRadius(3),
                            Padding = new Thickness(4, 1),
                            Child = labelTb,
                            IsHitTestVisible = false,
                            Tag = "island-label"
                        };
                        labelBg.ZIndex = 100;
                        ApplyLabelScale(labelBg, labelTb);

                        bool isContinental = rawSize > 1024;
                        // Tag the badge with its element so PositionLabelFor can recompute
                        // its position when the island is dragged or duplicated.
                        labelBg.Tag = (Element: (MapElement)island, IsContinental: isContinental, Size: size);

                        int bboxTop = mapHeight - island.Position.Y - size;
                        // Provisional placement so the first paint isn't (0,0); refined
                        // once the Border has a real measured width.
                        if (isContinental)
                        {
                            Canvas.SetLeft(labelBg, island.Position.X + 8);
                            Canvas.SetTop(labelBg, bboxTop + 8);
                        }
                        else
                        {
                            Canvas.SetLeft(labelBg, island.Position.X);
                            Canvas.SetTop(labelBg, bboxTop - 4 - 16);
                            labelBg.AttachedToVisualTree += (_, _) => PositionLabelFor(island);
                        }
                        _canvas.Children.Add(labelBg);
                        _islandLabels.Add(labelBg);
                        _labelByElement[island] = labelBg;
                    }

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

        /// <summary>
        /// Two-line label like "Random\nSmall" or "Fixed\nPirateland" — drawn over each
        /// island so the map is readable at a glance. Splitting on a newline keeps the
        /// pill narrow even for long type names like "Pirateland".
        /// </summary>
        private static string? BuildIslandLabel(IslandElement island)
        {
            string origin = island switch
            {
                FixedIslandElement => "Fixed",
                RandomIslandElement => "Random",
                _ => ""
            };

            // The element's IslandType comes from the .a7tinfo Config.Type.id, which is often
            // just "Normal" even when the underlying asset is a Volcanic / Cliff / etc. flavour.
            // Fall back to the asset's IslandType so the label reflects what's actually on disk.
            IslandType effectiveType = island.IslandType;
            if (effectiveType == IslandType.Normal && island is FixedIslandElement fixedIsland
                && fixedIsland.IslandAsset?.IslandType?.FirstOrDefault() is { } assetType
                && assetType != IslandType.Normal)
            {
                effectiveType = assetType;
            }

            // NPC / decoration islands: their role wins over their physical size.
            if (effectiveType == IslandType.ThirdParty)     return Join(origin, "ThirdParty");
            if (effectiveType == IslandType.PirateIsland)   return Join(origin, "Pirateland");
            if (effectiveType == IslandType.Decoration)     return Join(origin, "Deco");
            if (effectiveType == IslandType.Cliff)          return Join(origin, "Cliff");
            if (effectiveType == IslandType.VolcanicIsland) return Join(origin, "Volcanic");

            // Otherwise show the physical size (small/medium/large/extralarge/continental).
            string size = island switch
            {
                RandomIslandElement r => r.IslandSize?.Name ?? "?",
                FixedIslandElement f => f.IslandAsset?.IslandSize?.FirstOrDefault()?.Name ?? "?",
                _ => "?"
            };
            if (size == "Continental") return Join(origin, "Continental");
            if (island.IslandType == IslandType.Starter) return Join("Starter", size);
            return Join(origin, size);

            // Join two tokens onto two lines (top + bottom). If either is empty, return the other.
            static string Join(string top, string bottom)
            {
                if (string.IsNullOrEmpty(top)) return bottom;
                if (string.IsNullOrEmpty(bottom)) return top;
                return $"{top}\n{bottom}";
            }
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
            // Same fallback as BuildIslandLabel: prefer the asset's IslandType when the placed
            // element only carries the generic "Normal" tag, so volcanic-named assets keep
            // their red brush even if the .a7tinfo doesn't mark them explicitly.
            IslandType? type = element.IslandType;
            if (type == IslandType.Normal && element is FixedIslandElement fixedIsland
                && fixedIsland.IslandAsset?.IslandType?.FirstOrDefault() is { } assetType
                && assetType != IslandType.Normal)
            {
                type = assetType;
            }

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

            // PA handle hit test takes priority over normal element hit test, but only
            // when the user has switched on "Edit playable area".
            if (_editPlayableArea
                && props.Properties.IsLeftButtonPressed
                && e.Source is Control src
                && FindPaHandle(src) is { } h
                && _map is not null)
            {
                _draggingPaHandle = h;
                Rect2 pa = _map.PlayableArea;
                _paAtDragStart = (pa.X, pa.Y, pa.X + pa.Width, pa.Y + pa.Height);
                _paLive = _paAtDragStart;
                _panStartPointer = e.GetPosition(_scroller);
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }

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

            // Resize-PA branch: short-circuit before the normal element/pan logic.
            if (_draggingPaHandle is { } handle && _map is not null && _canvas is not null)
            {
                ApplyPaHandleDrag(handle, e.GetPosition(_canvas));
                e.Handled = true;
                return;
            }

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
            // Drag the badge along with its island so the label stays glued to it.
            PositionLabelFor(_pressedElement);
            ElementMoved?.Invoke(this, _pressedElement);
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            // Finish a PA-handle drag: notify subscribers so they can record undo
            // and persist via ResizeAndCommitMapTemplate.
            if (_draggingPaHandle is not null)
            {
                if (_paLive != _paAtDragStart)
                {
                    PlayableAreaResized?.Invoke(this,
                        (_paLive.x1, _paLive.y1, _paLive.x2, _paLive.y2,
                         _paAtDragStart.x1, _paAtDragStart.y1, _paAtDragStart.x2, _paAtDragStart.y2));
                }
                _draggingPaHandle = null;
                Cursor = new Cursor(StandardCursorType.Hand);
                e.Pointer.Capture(null);
                e.Handled = true;
                return;
            }

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
                    ElementMoveCommitted?.Invoke(this, _pressedElement);
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

        // Toggle between flat top-down view (0°) and the in-game isometric diamond view (-45°).
        // -45° lines up the .a7tinfo coordinate system with the in-game minimap for elements
        // *inside the 2048 reference frame* (= the green InitialPlayableArea zone). Elements
        // placed in the expanded NE quadrant don't follow this mapping — vanilla doesn't
        // lock them and the engine repositions them dynamically. Persisted globally.
        private int _rotationDeg = UserSettings.Default.MapViewRotationDeg;
        private void OnRotateView(object? sender, RoutedEventArgs e)
        {
            _rotationDeg = _rotationDeg == -45 ? 0 : -45;
            UserSettings.Default.MapViewRotationDeg = _rotationDeg;
            UserSettings.Default.Save();
            UpdateOrientationLabel();
            ApplyZoom();
        }

        private void UpdateOrientationLabel()
        {
            if (this.FindControl<Button>("OrientationBtn") is { } btn)
                btn.Content = _rotationDeg == -45 ? "▢ Vue plate" : "◇ Vue jeu";
        }

        private void ApplyZoom()
        {
            if (_zoomHost is null) return;
            // Combined scale + rotation transform so users can both zoom and reorient at once.
            var group = new TransformGroup();
            group.Children.Add(new ScaleTransform(_scale, _scale));
            if (_rotationDeg != 0)
                group.Children.Add(new RotateTransform(_rotationDeg));
            _zoomHost.LayoutTransform = group;
            UpdateZoomLabel();
            // Resize handles + island labels must stay readable at any zoom — keep their
            // on-screen size ~constant by scaling their map-space size inversely.
            LayoutPaHandles();
            foreach (var bg in _islandLabels)
            {
                if (bg.Child is TextBlock tb)
                    ApplyLabelScale(bg, tb);
            }
        }

        /// <summary>
        /// Recompute the badge position for one element from its current Position. Called
        /// from BuildElementVisual (initial placement) and from MoveElementToPointer
        /// (during a drag) so the label stays glued to its island.
        /// </summary>
        private void PositionLabelFor(MapElement element)
        {
            if (_map is null) return;
            if (!_labelByElement.TryGetValue(element, out var labelBg)) return;
            if (labelBg.Tag is not ValueTuple<MapElement, bool, int> tag) return;

            int size = tag.Item3;
            bool isContinental = tag.Item2;
            int bboxTop = _map.Size.Y - element.Position.Y - size;

            if (isContinental)
            {
                Canvas.SetLeft(labelBg, element.Position.X + 8);
                Canvas.SetTop(labelBg, bboxTop + 8);
                return;
            }

            double w = labelBg.Bounds.Width;
            double h = labelBg.Bounds.Height;
            if (w > 0)
                Canvas.SetLeft(labelBg, element.Position.X + size / 2.0 - w / 2.0);
            if (h > 0)
                Canvas.SetTop(labelBg, bboxTop - h - 2);
        }

        /// <summary>Set the FontSize so the label renders at ~12 px on screen regardless
        /// of the canvas zoom. Values below 1.0 in map space are silently clamped.</summary>
        private void ApplyLabelScale(Border bg, TextBlock tb)
        {
            const double TargetScreenFontSize = 12;
            double mapFontSize = TargetScreenFontSize / Math.Max(_scale, 0.05);
            tb.FontSize = mapFontSize;
            // Padding in map space too, so the pill doesn't shrink to nothing at low zoom.
            double padH = 6 / Math.Max(_scale, 0.05);
            double padV = 2 / Math.Max(_scale, 0.05);
            bg.Padding = new Thickness(padH, padV);
            bg.CornerRadius = new CornerRadius(4 / Math.Max(_scale, 0.05));
        }

        private void UpdateZoomLabel()
        {
            if (_zoomLabel != null)
                _zoomLabel.Text = $"{_scale * 100:F0}%";
        }

        // ------------------- Playable area resize handles -------------------

        private void CreatePaHandles()
        {
            if (_canvas is null) return;
            foreach (PaHandle h in System.Enum.GetValues(typeof(PaHandle)))
            {
                var ellipse = new Ellipse
                {
                    Width = PaHandleRadius * 2,
                    Height = PaHandleRadius * 2,
                    Fill = PaHandleFill,
                    Stroke = PaHandleStroke,
                    StrokeThickness = 1.5,
                    Tag = h,
                    IsVisible = _editPlayableArea
                };
                ToolTip.SetTip(ellipse, "Drag to resize the playable area");
                _paHandles[h] = ellipse;
                _canvas.Children.Add(ellipse);
            }
        }

        private void LayoutPaHandles()
        {
            if (_map is null || _paHandles.Count == 0) return;
            Rect2 pa = _map.PlayableArea;
            int h = _map.Size.Y;
            // Map coords: rectangle goes from (pa.X, h - pa.Y - pa.Height) to (pa.X + pa.Width, h - pa.Y)
            double left = pa.X;
            double top = h - pa.Y - pa.Height;
            double right = pa.X + pa.Width;
            double bottom = h - pa.Y;
            double midX = (left + right) / 2;
            double midY = (top + bottom) / 2;

            // Compensate for the canvas zoom so handles stay ~14 px on screen, big enough
            // to grab even at 24% zoom on a 2688-tile map. Z-index keeps them above the rect.
            const double TargetScreenRadius = 14;
            double radius = TargetScreenRadius / Math.Max(_scale, 0.05);
            double diam = radius * 2;

            void place(PaHandle which, double cx, double cy)
            {
                if (!_paHandles.TryGetValue(which, out var el)) return;
                el.Width = diam;
                el.Height = diam;
                el.StrokeThickness = Math.Max(1.0, 2.0 / _scale);
                Canvas.SetLeft(el, cx - radius);
                Canvas.SetTop(el, cy - radius);
                el.ZIndex = 200;
            }

            place(PaHandle.NW, left, top);
            place(PaHandle.N,  midX, top);
            place(PaHandle.NE, right, top);
            place(PaHandle.W,  left, midY);
            place(PaHandle.E,  right, midY);
            place(PaHandle.SW, left, bottom);
            place(PaHandle.S,  midX, bottom);
            place(PaHandle.SE, right, bottom);
        }

        private PaHandle? FindPaHandle(Control source)
        {
            for (Control? c = source; c is not null; c = c.Parent as Control)
            {
                if (c.Tag is PaHandle h) return h;
            }
            return null;
        }

        /// <summary>
        /// Update the playable area live during a handle drag. Edits the right boundary in
        /// E/NE/SE handles, the left in W/NW/SW, and the same logic top/bottom for vertical
        /// edges. The middle handles (N/S/W/E) only move one axis. Coordinates are clamped
        /// so x2 &gt; x1 and y2 &gt; y1, with at least 1 tile of separation.
        /// </summary>
        private void ApplyPaHandleDrag(PaHandle handle, Point pInCanvas)
        {
            if (_map is null || _paRect is null) return;
            int mapH = _map.Size.Y;
            int mapW = _map.Size.X;

            // Convert pointer (canvas coords, Y-down) to map coords (Y-up).
            int px = System.Math.Clamp((int)System.Math.Round(pInCanvas.X), 0, mapW);
            int py = System.Math.Clamp(mapH - (int)System.Math.Round(pInCanvas.Y), 0, mapH);

            (int x1, int y1, int x2, int y2) = _paAtDragStart;
            switch (handle)
            {
                case PaHandle.NW: x1 = px; y2 = py; break;
                case PaHandle.N:               y2 = py; break;
                case PaHandle.NE: x2 = px; y2 = py; break;
                case PaHandle.W:  x1 = px;          break;
                case PaHandle.E:  x2 = px;          break;
                case PaHandle.SW: x1 = px; y1 = py; break;
                case PaHandle.S:               y1 = py; break;
                case PaHandle.SE: x2 = px; y1 = py; break;
            }

            // Snap to the same grid as element drag for visual coherence.
            x1 = (x1 / SnapTiles) * SnapTiles;
            y1 = (y1 / SnapTiles) * SnapTiles;
            x2 = (x2 / SnapTiles) * SnapTiles;
            y2 = (y2 / SnapTiles) * SnapTiles;

            // Keep at least 1 tile of separation so the area doesn't invert.
            if (x2 <= x1) x2 = x1 + SnapTiles;
            if (y2 <= y1) y2 = y1 + SnapTiles;
            x1 = System.Math.Max(0, x1);
            y1 = System.Math.Max(0, y1);
            x2 = System.Math.Min(mapW, x2);
            y2 = System.Math.Min(mapH, y2);

            _paLive = (x1, y1, x2, y2);

            // Update the rectangle visually without going through the model — the model
            // commit happens once on pointer release for cheaper undo entries.
            _paRect.Width = x2 - x1;
            _paRect.Height = y2 - y1;
            Canvas.SetLeft(_paRect, x1);
            Canvas.SetTop(_paRect, mapH - y2);

            // Reposition handles to follow the new rectangle (size already correct
            // from the last LayoutPaHandles call; reuse it so handles stay clickable
            // at any zoom).
            double left = x1, top = mapH - y2, right = x2, bottom = mapH - y1;
            double midX = (left + right) / 2, midY = (top + bottom) / 2;
            double radius = (_paHandles.Values.FirstOrDefault()?.Width ?? PaHandleRadius * 2) / 2;
            void place(PaHandle which, double cx, double cy)
            {
                if (!_paHandles.TryGetValue(which, out var el)) return;
                Canvas.SetLeft(el, cx - radius);
                Canvas.SetTop(el, cy - radius);
            }
            place(PaHandle.NW, left, top);
            place(PaHandle.N,  midX, top);
            place(PaHandle.NE, right, top);
            place(PaHandle.W,  left, midY);
            place(PaHandle.E,  right, midY);
            place(PaHandle.SW, left, bottom);
            place(PaHandle.S,  midX, bottom);
            place(PaHandle.SE, right, bottom);

            PlayableAreaResizing?.Invoke(this, _paLive);
        }
    }
}
