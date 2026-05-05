using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AnnoMapEditor.DataArchives;
using AnnoMapEditor.DataArchives.Assets.Models;
using AnnoMapEditor.MapTemplates;
using AnnoMapEditor.MapTemplates.Enums;
using AnnoMapEditor.MapTemplates.Models;
using AnnoMapEditor.MapTemplates.Serializing;
using AnnoMapEditor.Mods.Serialization;
using AnnoMapEditor.UI.Avalonia.Controls;
using AnnoMapEditor.UI.Avalonia.ViewModels;
using AnnoMapEditor.Utilities;
using AnnoMapEditor.Utilities.UndoRedo;
using Avalonia.Layout;
using Avalonia.Media;

namespace AnnoMapEditor.UI.Avalonia.Windows
{
    public partial class MainWindow : Window
    {
        private TextBlock? _gameTitle;
        private TextBlock? _mapsCountLabel;
        private TreeView? _mapsList;
        private TextBlock? _mapDetailTitle;
        private TextBlock? _mapDetailSubtitle;
        private TextBlock? _statSize;
        private TextBlock? _statPlayable;
        private TextBlock? _statElements;
        private MapView? _mapRenderer;
        private TextBlock? _statusBar;
        private ItemsControl? _dlcFilters;
        private TextBlock? _selHint;
        private StackPanel? _selProperties;
        // Live-updating position readout in the right-hand properties panel.
        // Populated by BuildPropertyControls, refreshed by OnMapElementMovedLive
        // so the user can read X/Y change frame by frame while dragging.
        private TextBlock? _positionValueText;
        private MapElement? _positionTrackedElement;
        private bool _suppressPropertyEvents;
        private ListBox? _historyList;
        private Button? _undoButton;
        private Button? _redoButton;
        private TreeView? _elementsTree;
        private bool _suppressTreeEvents;
        private Border? _sessionPropsPanel;
        private TextBlock? _sessionRegionLabel;
        private TextBlock? _sessionMapSizeLabel;
        private Slider? _sessionPaSlider;
        private TextBlock? _sessionPaSizeLabel;
        private CheckBox? _sessionEditPaToggle;
        private NumericUpDown? _paX1Box, _paY1Box, _paX2Box, _paY2Box;
        private (int x1, int y1, int x2, int y2)? _originalPlayableArea;
        private bool _suppressSessionPaSlider;
        private StackPanel? _issuesPanel;
        private List<MapListItem> _allMaps = new();
        private readonly ObservableCollection<DlcFilter> _filters = new();

        public MainWindow()
        {
            InitializeComponent();
            WindowStateService.Attach(this, WindowKind.Main);
            // Append the version to the window title so users can quote it on bug reports / PRs.
            Title = $"{Title}  ·  {AppInfo.ShortVersionLabel}";
            Opened += OnOpened;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _gameTitle = this.FindControl<TextBlock>("GameTitle");
            _mapsCountLabel = this.FindControl<TextBlock>("MapsCountLabel");
            _mapsList = this.FindControl<TreeView>("MapsList");
            _mapDetailTitle = this.FindControl<TextBlock>("MapDetailTitle");
            _mapDetailSubtitle = this.FindControl<TextBlock>("MapDetailSubtitle");
            _statSize = this.FindControl<TextBlock>("StatSize");
            _statPlayable = this.FindControl<TextBlock>("StatPlayable");
            _statElements = this.FindControl<TextBlock>("StatElements");
            _mapRenderer = this.FindControl<MapView>("MapRenderer");
            _statusBar = this.FindControl<TextBlock>("StatusBar");
            _dlcFilters = this.FindControl<ItemsControl>("DlcFilters");
            _selHint = this.FindControl<TextBlock>("SelHint");
            _selProperties = this.FindControl<StackPanel>("SelProperties");
            _historyList = this.FindControl<ListBox>("HistoryList");
            _undoButton = this.FindControl<Button>("UndoButton");
            _redoButton = this.FindControl<Button>("RedoButton");
            _elementsTree = this.FindControl<TreeView>("ElementsTree");
            _sessionPropsPanel = this.FindControl<Border>("SessionPropsPanel");
            _sessionRegionLabel = this.FindControl<TextBlock>("SessionRegionLabel");
            _sessionMapSizeLabel = this.FindControl<TextBlock>("SessionMapSizeLabel");
            _sessionPaSlider = this.FindControl<Slider>("SessionPaSlider");
            _sessionPaSizeLabel = this.FindControl<TextBlock>("SessionPaSizeLabel");
            _sessionEditPaToggle = this.FindControl<CheckBox>("SessionEditPaToggle");
            _issuesPanel = this.FindControl<StackPanel>("IssuesPanel");
            _paX1Box = this.FindControl<NumericUpDown>("PaX1Box");
            _paY1Box = this.FindControl<NumericUpDown>("PaY1Box");
            _paX2Box = this.FindControl<NumericUpDown>("PaX2Box");
            _paY2Box = this.FindControl<NumericUpDown>("PaY2Box");
            _actionButtons = this.FindControl<StackPanel>("ActionButtons");
            _rotateButton = this.FindControl<Button>("RotateButton");
            _saveModButton = this.FindControl<Button>("SaveModButton");
            if (_dlcFilters != null) _dlcFilters.ItemsSource = _filters;
            if (_mapRenderer != null)
            {
                _mapRenderer.ElementSelected += OnMapElementSelected;
                _mapRenderer.ElementMoved += OnMapElementMovedLive;
                _mapRenderer.ElementMoveCommitted += OnElementMoveCommitted;
                _mapRenderer.PlayableAreaResizing += OnPlayableAreaResizing;
                _mapRenderer.PlayableAreaResized += OnPlayableAreaResized;
                _mapRenderer.ContextMenuRequested += OnMapContextMenuRequested;
            }
            if (_historyList != null)
                _historyList.ItemsSource = UndoRedoStack.Instance.UndoHistory;
            UndoRedoStack.Instance.PropertyChanged += (_, __) => RefreshUndoRedoButtons();
            RefreshUndoRedoButtons();
            KeyDown += OnWindowKeyDown;
            // Arrow keys: capture in tunnel routing so the map list / element tree
            // (which natively consume Up/Down/Left/Right for ListBox navigation)
            // can't swallow them before we get a chance to nudge the selected
            // island. We still bail out when a text input has focus so typing in
            // a NumericUpDown / TextBox keeps working.
            // KeyDown in Avalonia bubbles, doesn't tunnel. The map list / element tree
            // mark the event Handled when they consume an arrow key for their own
            // navigation, so a plain bubble handler never sees it. Subscribe with
            // handledEventsToo:true to bypass that and run our nudge regardless.
            this.AddHandler(KeyDownEvent, OnTunnelArrowKeys, RoutingStrategies.Bubble, handledEventsToo: true);
        }

        private void OnOpened(object? sender, EventArgs e)
        {
            DataManager dm = DataManager.Instance;
            if (_gameTitle != null)
                _gameTitle.Text = dm.DetectedGame?.Title ?? "—";
            if (_statusBar != null)
                _statusBar.Text = dm.HasError
                    ? $"⚠ {dm.ErrorMessage}"
                    : "Prêt. Sélectionne une carte pour en voir le contenu.";

            // Sync the bottom-bar theme FAB with whichever theme was applied at
            // startup (UserSettings.ThemeVariant) so its icon matches.
            UpdateThemeToggleIcon();
            LoadMaps();
        }

        private void LoadMaps()
        {
            DataManager dm = DataManager.Instance;
            if (!dm.IsInitialized || _mapsList == null)
                return;

            try
            {
                var vanillaMaps = dm.AssetRepository.GetAll<MapTemplateAsset>()
                    .OrderBy(m => m.TemplateRegion?.Name ?? "")
                    .ThenBy(m => m.TemplateMapType?.Name ?? "")
                    .ThenBy(m => m.Name ?? "")
                    .SelectMany(MapListItem.AllVariants);

                var modMaps = MapListItem.ScanModsFolder(Settings.Instance.ModsPath)
                    .OrderBy(m => m.ModFolderName)
                    .ThenBy(m => m.DisplayName);

                _allMaps = modMaps.Concat(vanillaMaps).ToList();

                RebuildDlcFilters();
                ApplyDlcFilters();
            }
            catch (Exception ex)
            {
                if (_statusBar != null)
                    _statusBar.Text = $"⚠ Échec du listing des cartes : {ex.Message}";
            }
        }

        private void RebuildDlcFilters()
        {
            // Unsubscribe old
            foreach (var f in _filters)
                f.PropertyChanged -= OnFilterChanged;
            _filters.Clear();

            var disabled = Settings.Instance.DisabledDlcFilters;

            // "Base" first, then sorted DLCs
            var distinct = _allMaps
                .Select(m => m.DlcId)
                .Distinct()
                .OrderBy(id => id == "Base" ? 0 : 1)
                .ThenBy(id => id);

            foreach (string id in distinct)
            {
                // Restore the toggle state from disk so the user's last choice
                // persists across sessions.
                var f = new DlcFilter(id) { IsEnabled = !disabled.Contains(id) };
                f.PropertyChanged += OnFilterChanged;
                _filters.Add(f);
            }
        }

        private void OnFilterChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(DlcFilter.IsEnabled)) return;
            // Persist the new disabled set, then refresh the visible map list.
            Settings.Instance.DisabledDlcFilters = new System.Collections.Generic.HashSet<string>(
                _filters.Where(f => !f.IsEnabled).Select(f => f.Id),
                System.StringComparer.OrdinalIgnoreCase);
            ApplyDlcFilters();
        }

        private void ApplyDlcFilters()
        {
            if (_mapsList is null) return;
            HashSet<string> enabled = new(_filters.Where(f => f.IsEnabled).Select(f => f.Id));
            var filtered = _allMaps.Where(m => enabled.Contains(m.DlcId)).ToList();

            // Build a hierarchy:
            //   - Mods that ship multiple maps → one expandable group per mod folder
            //     (Header = mod name, Children = each .a7tinfo).
            //   - Mods with a single map and all vanilla maps → flat leaves.
            var entries = new System.Collections.Generic.List<MapListEntry>();

            var modGroups = filtered
                .Where(m => m.IsMod && m.ModFolderName != null)
                .GroupBy(m => m.ModFolderName!)
                .OrderBy(g => g.Key);
            foreach (var g in modGroups)
            {
                if (g.Count() == 1)
                {
                    entries.Add(new MapListEntry(g.First()));
                }
                else
                {
                    var group = new MapListEntry(
                        $"⚙ {g.Key}",
                        $"Mod · {g.Count()} cartes");
                    foreach (var item in g.OrderBy(i => i.DisplayName))
                        group.Children.Add(new MapListEntry(item));
                    entries.Add(group);
                }
            }

            // Vanilla maps stay flat (sorted as before).
            foreach (var m in filtered.Where(m => !m.IsMod))
                entries.Add(new MapListEntry(m));

            _mapsList.ItemsSource = entries;
            if (_mapsCountLabel != null)
                _mapsCountLabel.Text = $"{filtered.Count} / {_allMaps.Count} cartes";
        }

        private async void OnMapSelected(object? sender, SelectionChangedEventArgs e)
        {
            // The TreeView selection can be either a leaf (a real MapListItem) or a group
            // header. We only act on leaves; clicking a group just expands/collapses it.
            if (_mapsList?.SelectedItem is not MapListEntry entry || entry.Item is null)
                return;

            await LoadMapDetailAsync(entry.Item);
        }

        private async Task LoadMapDetailAsync(MapListItem item)
        {
            if (_mapDetailTitle != null)
                _mapDetailTitle.Text = item.DisplayName;
            if (_mapDetailSubtitle != null)
                _mapDetailSubtitle.Text = item.SubLabel + "  ·  " + item.TemplatePath;
            if (_statSize != null) _statSize.Text = "…";
            if (_statPlayable != null) _statPlayable.Text = "…";
            if (_statElements != null) _statElements.Text = "…";
            _mapRenderer?.SetMap(null);
            if (_statusBar != null) _statusBar.Text = $"Chargement de « {item.DisplayName} »…";

            try
            {
                var reader = new MapTemplateReader();
                MapTemplate map = item.IsMod && item.AbsoluteFilePath != null
                    ? await reader.FromBinaryFileAsync(item.AbsoluteFilePath)
                    : await reader.FromDataArchiveAsync(item.TemplatePath);

                var elements = await Task.Run(() => map.Elements
                    .Select(MapElementItem.From)
                    .ToList());

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_statSize != null)
                        _statSize.Text = $"{map.Size.X}×{map.Size.Y}";
                    if (_statPlayable != null)
                        _statPlayable.Text = $"{map.PlayableArea.Width}×{map.PlayableArea.Height}";
                    if (_statElements != null)
                        _statElements.Text = elements.Count.ToString();
                    _currentMap = map;
                    _currentMapItem = item;
                    UndoRedoStack.Instance.ClearStacks();
                    _mapRenderer?.SetMap(map);
                    RebuildElementsTree();
                    PopulatePlayableAreaSection();
                    RefreshIssuesPanel();
                    if (_saveModButton != null) _saveModButton.IsEnabled = true;
                    RefreshUndoRedoButtons();
                    if (_statusBar != null)
                        _statusBar.Text = $"« {item.DisplayName} » chargée. {elements.Count} éléments. Molette pour zoomer.";
                });
            }
            catch (Exception ex)
            {
                List<MapElementItem> diag = await Task.Run(() => DiagnoseRawTemplate(item.TemplatePath));
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_statSize != null) _statSize.Text = "—";
                    if (_statPlayable != null) _statPlayable.Text = "—";
                    if (_statElements != null) _statElements.Text = "format inconnu";
                    _mapRenderer?.SetMap(null);
                    if (_statusBar != null)
                        _statusBar.Text = $"⚠ Format binaire non supporté pour « {item.DisplayName} ». Diag : {string.Join(" / ", diag.ConvertAll(d => d.Kind + ':' + d.Description))}";
                });
            }
        }

        private static List<MapElementItem> DiagnoseRawTemplate(string templateFilename)
        {
            var rows = new List<MapElementItem>
            {
                MapElementItem.Diag("info", "—", "Désérialisation FileDB échouée. Analyse approfondie :"),
                MapElementItem.Diag("path", "—", templateFilename),
            };

            try
            {
                using Stream? s = DataManager.Instance.DataArchive.OpenRead(templateFilename);
                if (s is null)
                {
                    rows.Add(MapElementItem.Diag("error", "—", "Fichier introuvable dans l'archive."));
                    return rows;
                }

                byte[] all;
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    all = ms.ToArray();
                }

                rows.Add(MapElementItem.Diag("size", "—", $"{all.Length:N0} octets"));

                int headLen = Math.Min(64, all.Length);
                string headAscii = new string(all.Take(headLen)
                    .Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray());
                string headHex = string.Join(" ", all.Take(Math.Min(headLen, 32))
                    .Select(b => b.ToString("X2")));
                rows.Add(MapElementItem.Diag("head", "ascii", headAscii));
                rows.Add(MapElementItem.Diag("head", "hex", headHex));

                if (all.Length >= 16)
                {
                    int tailLen = Math.Min(32, all.Length);
                    var tailBytes = all.Skip(all.Length - tailLen).ToArray();
                    string tailHex = string.Join(" ", tailBytes.Select(b => b.ToString("X2")));
                    rows.Add(MapElementItem.Diag("tail", "hex", tailHex));

                    byte[] magicV2 = { 0x08, 0x00, 0x00, 0x00, 0xFE, 0xFF, 0xFF, 0xFF };
                    byte[] magicV3 = { 0x08, 0x00, 0x00, 0x00, 0xFD, 0xFF, 0xFF, 0xFF };
                    var last8 = all.Skip(all.Length - 8).ToArray();

                    if (last8.SequenceEqual(magicV2))
                        rows.Add(MapElementItem.Diag("BBDom", "magic V2", "✓ trouvé en footer — c'est bien un BBDocument V2 wrappé !"));
                    else if (last8.SequenceEqual(magicV3))
                        rows.Add(MapElementItem.Diag("BBDom", "magic V3", "✓ trouvé en footer — c'est bien un BBDocument V3 wrappé !"));
                    else
                        rows.Add(MapElementItem.Diag("BBDom", "—", "magic V2/V3 absent du footer."));
                }

                int firstNonFE = -1;
                for (int i = 19; i < Math.Min(all.Length, 4096); i++)
                {
                    if (all[i] != 0xFE) { firstNonFE = i; break; }
                }
                rows.Add(MapElementItem.Diag("payload", "offset",
                    firstNonFE >= 0 ? $"premier byte ≠ 0xFE à offset {firstNonFE}" : "padding 0xFE > 4 KiB"));

                if (firstNonFE > 0 && firstNonFE < all.Length - 8)
                {
                    int peek = Math.Min(32, all.Length - firstNonFE);
                    string payloadHex = string.Join(" ", all.Skip(firstNonFE).Take(peek)
                        .Select(b => b.ToString("X2")));
                    rows.Add(MapElementItem.Diag("payload", "hex", payloadHex));
                }

                try
                {
                    string outPath = Path.Combine(Path.GetTempPath(),
                        $"anno117-{Path.GetFileName(templateFilename)}");
                    File.WriteAllBytes(outPath, all);
                    rows.Add(MapElementItem.Diag("dump", "—", outPath));
                }
                catch { }

                string trimmedAscii = headAscii.TrimEnd('.', ' ', '\0').Trim();
                if (trimmedAscii.StartsWith("Resource File", StringComparison.OrdinalIgnoreCase))
                {
                    rows.Add(MapElementItem.Diag("verdict", "—",
                        "« Resource File V2.2 » : préfixe wrapper Anvil. Si le magic V2/V3 est présent en footer, le payload BBDoc est extrayable."));
                }
            }
            catch (Exception ex)
            {
                rows.Add(MapElementItem.Diag("error", "—", ex.Message));
            }

            return rows;
        }

        private void OnMapElementSelected(object? sender, MapElement? element)
        {
            _selectedElementForPanel = element;
            if (_selProperties is null || _selHint is null) return;

            _selProperties.Children.Clear();

            if (element is null)
            {
                _selHint.Text = "Clique sur une île ou un point de départ pour voir ses détails.";
                if (_actionButtons != null) _actionButtons.IsVisible = false;
                return;
            }

            _selHint.Text = $"Sélectionné : {element.GetType().Name}";
            BuildPropertyControls(element, _selProperties);

            if (_actionButtons != null)
                _actionButtons.IsVisible = element is IslandElement || element is StartingSpotElement;
            if (_rotateButton != null)
                _rotateButton.IsVisible = element is FixedIslandElement;
        }

        private void BuildPropertyControls(MapElement element, StackPanel host)
        {
            _suppressPropertyEvents = true;
            try
            {
                host.Children.Add(SectionHeader("Édition"));

                // Position (read-only display, modifiable via drag).
                // We hold on to the value TextBlock so OnMapElementMovedLive can rewrite
                // it frame by frame while the user drags the element on the canvas.
                var posLbl = new TextBlock
                {
                    Text = "Position",
                    Opacity = 0.7,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                };
                _positionValueText = new TextBlock
                {
                    Text = $"({element.Position.X}, {element.Position.Y})",
                    FontSize = 12,
                    FontFamily = new FontFamily("Cascadia Code,Consolas,monospace"),
                    TextWrapping = TextWrapping.Wrap
                };
                _positionTrackedElement = element;
                var posGrid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("110,*"),
                    Margin = new Thickness(0, 2)
                };
                Grid.SetColumn(posLbl, 0); posGrid.Children.Add(posLbl);
                Grid.SetColumn(_positionValueText, 1); posGrid.Children.Add(_positionValueText);
                host.Children.Add(posGrid);

                // IslandType — editable for IslandElement, IslandSize — editable for RandomIslandElement.
                // The two combos are wired together below: when the type changes, we re-fill the size
                // combo so ExtraLarge only appears for Starter (the only context Anno 117 uses 0300 in).
                ComboBox? sizeCombo = null;
                Action? rebuildSizeOptions = null;

                if (element is IslandElement island)
                {
                    // Continental fixed assets must always export with Type=null — the engine
                    // refuses Type=Starter (and anything else) on the unique DLC1 continental
                    // and falls back to a single-fertility default. Disable the combo on those
                    // assets so the user can't pick something we'd silently override at write.
                    bool isContinentalFixed = island is FixedIslandElement fxIsl
                        && fxIsl.IslandAsset?.FilePath?.Contains("continental",
                            System.StringComparison.OrdinalIgnoreCase) == true;

                    var typeCombo = new ComboBox { Width = 160, IsEnabled = !isContinentalFixed };
                    foreach (var t in IslandType.All) typeCombo.Items.Add(t);
                    typeCombo.SelectedItem = island.IslandType ?? IslandType.Normal;
                    ToolTip.SetTip(typeCombo, isContinentalFixed
                        ? "Le moteur Anno 117 force Type=null sur les continentales (Vésuve). " +
                          "Toute autre valeur casse le binding FertilitySet → fertilité Obsidian unique."
                        : "Rôle de l'île dans la session (jouable / NPC / déco). " +
                          "Le biome visuel (volcanique, neige…) vient du fichier .a7m, pas de ce champ.");
                    typeCombo.SelectionChanged += (_, _) =>
                    {
                        if (_suppressPropertyEvents) return;
                        if (typeCombo.SelectedItem is not IslandType newType) return;
                        var oldType = island.IslandType ?? IslandType.Normal;
                        if (oldType == newType) return;
                        island.IslandType = newType;
                        UndoRedoStack.Instance.Do(new IslandPropertiesStackEntry(
                            island, oldIslandType: oldType, newIslandType: newType));
                        // Refresh the size combo so ExtraLarge appears/disappears for this type.
                        rebuildSizeOptions?.Invoke();
                        RefreshAfterEdit();
                        RefreshUndoRedoButtons();
                    };
                    host.Children.Add(LabeledRow("Type (rôle)", typeCombo));
                }

                if (element is RandomIslandElement random)
                {
                    sizeCombo = new ComboBox { Width = 160 };

                    // (Re)build the size options based on the element's current IslandType.
                    // ExtraLarge is only valid for Starter (vanilla never uses 0300 elsewhere);
                    // Continental never appears as a random <Size> bucket.
                    rebuildSizeOptions = () =>
                    {
                        if (sizeCombo is null) return;
                        _suppressPropertyEvents = true;
                        try
                        {
                            sizeCombo.Items.Clear();
                            bool isStarter = (random.IslandType ?? IslandType.Normal) == IslandType.Starter;
                            foreach (var s in IslandSize.All)
                            {
                                if (s == IslandSize.Continental) continue;
                                if (s == IslandSize.ExtraLarge && !isStarter) continue;
                                sizeCombo.Items.Add(s);
                            }
                            // Keep the current size selected if still allowed; otherwise fall back
                            // to the closest allowed bucket (Large for ExtraLarge, etc.).
                            var current = random.IslandSize;
                            if (sizeCombo.Items.Contains(current))
                                sizeCombo.SelectedItem = current;
                            else
                                sizeCombo.SelectedItem = IslandSize.Large;
                        }
                        finally { _suppressPropertyEvents = false; }
                    };
                    rebuildSizeOptions();

                    sizeCombo.SelectionChanged += (_, _) =>
                    {
                        if (_suppressPropertyEvents) return;
                        if (sizeCombo.SelectedItem is not IslandSize newSize) return;
                        var oldSize = random.IslandSize;
                        if (oldSize == newSize) return;
                        random.IslandSize = newSize;
                        UndoRedoStack.Instance.Do(new IslandPropertiesStackEntry(
                            random, oldIslandSize: oldSize, newIslandSize: newSize));
                        RefreshAfterEdit();
                        RefreshUndoRedoButtons();
                    };
                    host.Children.Add(LabeledRow("Island size", sizeCombo));
                }

                // FixedIslandElement specific flags
                if (element is FixedIslandElement fixedIsland)
                {
                    AddBoolCheckbox(host, "Random rotation",
                        fixedIsland.RandomizeRotation,
                        v => SetFixedFlag(fixedIsland, randomizeRotation: v));
                    // When the user opts out of random rotation, surface a manual selector
                    // so they can pin a specific 0/90/180/270 angle.
                    if (!fixedIsland.RandomizeRotation)
                        BuildManualRotationEditor(host, fixedIsland);

                    AddBoolCheckbox(host, "Random fertilities",
                        fixedIsland.RandomizeFertilities,
                        v => SetFixedFlag(fixedIsland, randomizeFertilities: v));
                    if (!fixedIsland.RandomizeFertilities)
                        BuildManualFertilitiesEditor(host, fixedIsland);

                    AddBoolCheckbox(host, "Random slots",
                        fixedIsland.RandomizeSlots,
                        v => SetFixedFlag(fixedIsland, randomizeSlots: v));
                    if (!fixedIsland.RandomizeSlots)
                        BuildManualSlotsEditor(host, fixedIsland);
                }

                // Replace section: convert between Random and Fixed at the same position.
                if (element is IslandElement islForReplace)
                    BuildReplaceSection(host, islForReplace);

                // Read-only details below
                host.Children.Add(SectionHeader("Détails"));
                if (element is StartingSpotElement spot)
                    host.Children.Add(ReadOnlyRow("Player #", (spot.Index + 1).ToString()));

                if (element is IslandElement isl2)
                {
                    host.Children.Add(ReadOnlyRow("Size (tiles)", isl2.SizeInTiles.ToString()));
                    if (!string.IsNullOrEmpty(isl2.Label))
                        host.Children.Add(ReadOnlyRow("Label", isl2.Label));
                }

                if (element is FixedIslandElement fi)
                {
                    // Show MapFilePath even when the asset wasn't resolved (DLC continental placeholders)
                    if (!string.IsNullOrEmpty(fi.MapFilePath))
                        host.Children.Add(ReadOnlyRow("a7m terrain", fi.MapFilePath!));

                    if (fi.IslandAsset is { } asset)
                    {
                        if (!string.IsNullOrEmpty(asset.DisplayName))
                            host.Children.Add(ReadOnlyRow("Asset", asset.DisplayName));
                        if (asset.Region is { } region && !string.IsNullOrEmpty(region.Name))
                            host.Children.Add(ReadOnlyRow("Region", region.Name));
                        if (asset.IslandType is { } intrinsicTypes && intrinsicTypes.Any())
                            host.Children.Add(ReadOnlyRow("Type (asset)",
                                string.Join(", ", intrinsicTypes.Select(t => t.Name))));
                        if (asset.Slots is { Count: > 0 } slots)
                            host.Children.Add(ReadOnlyRow("Slots", slots.Count.ToString()));
                    }
                    if (fi.Fertilities is { Count: > 0 } fert)
                        host.Children.Add(ReadOnlyRow("Fertilities",
                            string.Join(", ", fert.Select(f => f?.Name ?? "?"))));
                }
            }
            finally
            {
                _suppressPropertyEvents = false;
            }
        }

        private void SetFixedFlag(FixedIslandElement fi,
            bool? randomizeRotation = null, bool? randomizeFertilities = null, bool? randomizeSlots = null)
        {
            if (_suppressPropertyEvents) return;
            if (randomizeRotation.HasValue) fi.RandomizeRotation = randomizeRotation.Value;
            if (randomizeFertilities.HasValue) fi.RandomizeFertilities = randomizeFertilities.Value;
            if (randomizeSlots.HasValue) fi.RandomizeSlots = randomizeSlots.Value;
            UndoRedoStack.Instance.Do(new IslandPropertiesStackEntry(fi,
                randomizeRotation: randomizeRotation,
                randomizeFertilities: randomizeFertilities,
                randomizeSlots: randomizeSlots));
            RefreshUndoRedoButtons();
            // Rebuild the panel so the matching manual editor (rotation / fertilities /
            // slots) appears or disappears.
            OnMapElementSelected(this, fi);
        }

        // ------------------- Manual editors (when randomize flags are off) -------------------

        private void BuildManualRotationEditor(StackPanel host, FixedIslandElement fi)
        {
            var combo = new ComboBox { Width = 160 };
            foreach (var deg in new[] { 0, 90, 180, 270 })
                combo.Items.Add($"{deg}°");
            byte current = fi.Rotation ?? 0;
            combo.SelectedIndex = current % 4;
            combo.SelectionChanged += (_, _) =>
            {
                if (_suppressPropertyEvents) return;
                if (combo.SelectedIndex < 0) return;
                byte newRot = (byte)combo.SelectedIndex;
                byte oldRot = fi.Rotation ?? 0;
                if (oldRot == newRot) return;
                fi.Rotation = newRot;
                UndoRedoStack.Instance.Do(new MapElementTransformStackEntry(
                    fi, fi.Position, fi.Position, oldRot, newRot));
                RefreshAfterEdit();
                RefreshUndoRedoButtons();
            };
            host.Children.Add(LabeledRow("Rotation", combo));
        }

        private void BuildManualFertilitiesEditor(StackPanel host, FixedIslandElement fi)
        {
            var allowed = _currentMap?.Session?.Region?.AllowedFertilities
                          ?? new List<FertilityAsset>();
            if (allowed.Count == 0)
            {
                host.Children.Add(new TextBlock
                {
                    Text = "(no fertilities listed for this region)",
                    Opacity = 0.6, FontSize = 11, Margin = new Thickness(0, 2, 0, 4)
                });
                return;
            }
            host.Children.Add(new TextBlock
            {
                Text = "Fertilities (toggle to assign):",
                Opacity = 0.6, FontSize = 11, Margin = new Thickness(0, 2, 0, 4)
            });
            foreach (var fertility in allowed.OrderBy(f => LocalizedFertilityName(f)))
            {
                host.Children.Add(BuildFertilityToggleRow(fi, fertility));
            }
        }

        // One row = real Anno icon (or category swatch if missing) + localized name +
        // ToggleSwitch on the right.
        private Control BuildFertilityToggleRow(FixedIslandElement fi, FertilityAsset fertility)
        {
            bool isAssigned = fi.Fertilities.Contains(fertility);

            // Try to load the real game icon from the .rda archive (Pfim already handles
            // .dds → Avalonia bitmap conversion). Cache it on the asset itself so we only
            // pay the I/O cost the first time the user opens the editor for a region.
            Control swatch = TryLoadFertilityIcon(fertility) ?? (Control)new Border
            {
                Width = 16, Height = 16,
                CornerRadius = new CornerRadius(2),
                Background = ResolveFertilityCategoryColor(fertility),
                VerticalAlignment = VerticalAlignment.Center
            };
            var label = new TextBlock
            {
                Text = LocalizedFertilityName(fertility),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            var toggle = new ToggleSwitch
            {
                IsChecked = isAssigned,
                OnContent = "",
                OffContent = "",
                Padding = new Thickness(0),
                MinWidth = 0
            };
            toggle.IsCheckedChanged += (_, _) =>
            {
                if (_suppressPropertyEvents) return;
                bool nowChecked = toggle.IsChecked == true;
                bool wasAssigned = fi.Fertilities.Contains(fertility);
                if (nowChecked == wasAssigned) return;
                if (nowChecked) fi.Fertilities.Add(fertility);
                else fi.Fertilities.Remove(fertility);
                UndoRedoStack.Instance.Do(new IslandFertilitiesStackEntry(
                    fi, added: nowChecked, fertility));
                RefreshUndoRedoButtons();
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                Margin = new Thickness(0, 2)
            };
            Grid.SetColumn(swatch, 0); grid.Children.Add(swatch);
            Grid.SetColumn(label, 1);  grid.Children.Add(label);
            Grid.SetColumn(toggle, 2); grid.Children.Add(toggle);
            return grid;
        }

        /// <summary>Looks up "fertility.&lt;Name without 'Fertility '/'Deposit ' prefix&gt;"
        /// in the i18n table and falls back to the asset's English DisplayName when no
        /// translation exists. Asset names come prefixed (e.g. "Fertility Oats",
        /// "Deposit Iron") — we strip those so the JSON keys can stay short and readable.</summary>
        private static string LocalizedFertilityName(FertilityAsset f)
        {
            string raw = f.Name ?? "";
            string stripped = raw;
            if (raw.StartsWith("Fertility ", System.StringComparison.OrdinalIgnoreCase))
                stripped = raw.Substring("Fertility ".Length);
            else if (raw.StartsWith("Deposit ", System.StringComparison.OrdinalIgnoreCase))
                stripped = raw.Substring("Deposit ".Length);

            string key = $"fertility.{stripped}";
            string translated = Localizer.Current.Get(key);
            return translated == key ? (f.DisplayName ?? f.Name ?? "?") : translated;
        }

        /// <summary>
        /// Resolve the real Anno fertility/deposit icon by reading IconFilename from the
        /// .rda archive (the same path the game uses). Cached on the asset so we don't
        /// re-decode the .dds on every panel rebuild. Returns null when the icon path
        /// is missing or the archive can't open it (e.g. mod-only run with no game data).
        /// </summary>
        private static Control? TryLoadFertilityIcon(FertilityAsset f)
        {
            try
            {
                if (f.Icon is null && !string.IsNullOrEmpty(f.IconFilename)
                    && DataManager.Instance.IsInitialized)
                {
                    f.Icon = DataManager.Instance.DataArchive.TryLoadIcon(
                        f.IconFilename, new global::Avalonia.PixelSize(32, 32));
                }
                if (f.Icon is null) return null;
                return new Image
                {
                    Source = f.Icon,
                    Width = 20, Height = 20,
                    Stretch = global::Avalonia.Media.Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            catch { return null; }
        }

        /// <summary>Fallback color hint when the .rda icon cannot be loaded — e.g. when
        /// running on a snapshot without game data.</summary>
        private static IBrush ResolveFertilityCategoryColor(FertilityAsset f)
        {
            string n = (f.Name ?? "").ToLowerInvariant();
            if (n.Contains("deposit") || n.Contains("iron") || n.Contains("coal")
                || n.Contains("clay") || n.Contains("marble") || n.Contains("mineral")
                || n.Contains("gold") || n.Contains("tin") || n.Contains("copper")
                || n.Contains("silver") || n.Contains("granite") || n.Contains("obsidian"))
                return new SolidColorBrush(Color.Parse("#9E9E9E"));    // mine / deposit — grey
            if (n.Contains("fish") || n.Contains("sardin") || n.Contains("mackerel")
                || n.Contains("oyster") || n.Contains("snail") || n.Contains("sturgeon")
                || n.Contains("samphire") || n.Contains("shell"))
                return new SolidColorBrush(Color.Parse("#3FA7E6"));    // sea — blue
            if (n.Contains("beaver") || n.Contains("bird") || n.Contains("ponies")
                || n.Contains("sheep"))
                return new SolidColorBrush(Color.Parse("#B07A48"));    // animal — brown
            return new SolidColorBrush(Color.Parse("#6FE07F"));        // crop — green
        }

        private void BuildManualSlotsEditor(StackPanel host, FixedIslandElement fi)
        {
            int totalAssignments = fi.SlotAssignments.Count;
            int totalIslandSlots = fi.IslandAsset?.Slots?.Count ?? 0;

            // Slots = mines, deposits, rivers/waterfalls, volcanoes, oil — every "random
            // pickable" object the engine places at map gen. They live in the .a7minfo
            // sibling of each .a7m, under <ObjectMetaInfo><SlotObjects>. If both counts
            // are zero, it just means this terrain doesn't define any.
            if (totalIslandSlots == 0)
            {
                host.Children.Add(new TextBlock
                {
                    Text = "This island has no random slots (mines, rivers, volcanoes…)\n"
                           + "in its .a7minfo, so there's nothing to fix manually.",
                    Opacity = 0.6, FontSize = 11, TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 4)
                });
                return;
            }

            host.Children.Add(new TextBlock
            {
                Text = $"Slots ({totalAssignments} / {totalIslandSlots}):",
                Opacity = 0.7, FontSize = 11, FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(0, 2, 0, 4)
            });

            if (totalAssignments == 0)
            {
                host.Children.Add(new TextBlock
                {
                    Text = "(no slot assignments yet — they'll be populated on save)",
                    Opacity = 0.6, FontSize = 11, Margin = new Thickness(0, 0, 0, 4)
                });
                return;
            }

            // Show every assignment, even when the resolved SlotAsset is null — without
            // a label we still display the ObjectId so the user knows something exists.
            foreach (var entry in fi.SlotAssignments.Values
                .OrderBy(a => a.Slot?.SlotAsset?.DisplayName
                              ?? a.Slot?.SlotAsset?.Name
                              ?? a.Slot?.ObjectId.ToString()
                              ?? ""))
            {
                host.Children.Add(BuildSlotRow(entry));
            }
        }

        // One row per slot: icon + default name + ComboBox listing "(default)" plus the
        // replacement options the engine accepts (Slot.SlotAsset.ReplacementSlotAssets).
        private Control BuildSlotRow(SlotAssignment assignment)
        {
            // Slot.SlotAsset can be null when the island has slots referenced by GUIDs
            // that aren't in the loaded assets.xml (DLC mismatch, etc.). Render a minimal
            // row so the user still sees the slot exists.
            var defaultSlot = assignment.Slot?.SlotAsset;
            // "(default)" sentinel used as ComboBox item — IslandAsset = null distinguishes it.
            var defaultMarker = new SlotAsset();
            var options = new List<SlotAsset> { defaultMarker };
            if (defaultSlot != null)
                options.AddRange(defaultSlot.ReplacementSlotAssets ?? Array.Empty<SlotAsset>());

            Control icon = (defaultSlot != null ? TryLoadSlotIcon(defaultSlot) : null)
                ?? (Control)new Border
                {
                    Width = 16, Height = 16,
                    CornerRadius = new CornerRadius(2),
                    Background = new SolidColorBrush(Color.Parse("#9E9E9E")),
                    VerticalAlignment = VerticalAlignment.Center
                };

            var nameBlock = new TextBlock
            {
                Text = defaultSlot?.DisplayName
                       ?? defaultSlot?.Name
                       ?? $"slot #{assignment.Slot?.ObjectId}",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var combo = new ComboBox { Width = 140, FontSize = 11 };
            foreach (var opt in options) combo.Items.Add(opt);
            combo.ItemTemplate = new global::Avalonia.Controls.Templates.FuncDataTemplate<SlotAsset>(
                (s, _) =>
                {
                    if (s is null) return new TextBlock();
                    bool isDefault = ReferenceEquals(s, defaultMarker);
                    return new TextBlock
                    {
                        Text = isDefault ? "(default)" : (s.DisplayName ?? s.Name ?? "?"),
                        FontStyle = isDefault
                            ? global::Avalonia.Media.FontStyle.Italic
                            : global::Avalonia.Media.FontStyle.Normal,
                        FontSize = 11
                    };
                });
            // Pre-select: default if AssignedSlot is null or equals the original; otherwise
            // the actual replacement.
            int initialIndex = 0;
            if (assignment.AssignedSlot is { } current
                && !ReferenceEquals(current, defaultSlot))
            {
                int found = options.IndexOf(current);
                if (found > 0) initialIndex = found;
            }
            combo.SelectedIndex = initialIndex;

            combo.SelectionChanged += (_, _) =>
            {
                if (_suppressPropertyEvents) return;
                if (combo.SelectedItem is not SlotAsset chosen) return;
                assignment.AssignedSlot = ReferenceEquals(chosen, defaultMarker)
                    ? defaultSlot
                    : chosen;
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                Margin = new Thickness(0, 1)
            };
            Grid.SetColumn(icon, 0);      grid.Children.Add(icon);
            Grid.SetColumn(nameBlock, 1); grid.Children.Add(nameBlock);
            Grid.SetColumn(combo, 2);     grid.Children.Add(combo);
            return grid;
        }

        /// <summary>Reads the slot icon from the .rda archive (cached on the asset).
        /// Returns null when there's no icon path or the archive isn't loaded.</summary>
        private static Control? TryLoadSlotIcon(SlotAsset s)
        {
            try
            {
                if (s.Icon is null && !string.IsNullOrEmpty(s.IconFilename)
                    && DataManager.Instance.IsInitialized)
                {
                    s.Icon = DataManager.Instance.DataArchive.TryLoadIcon(
                        s.IconFilename, new global::Avalonia.PixelSize(32, 32));
                }
                if (s.Icon is null) return null;
                return new Image
                {
                    Source = s.Icon,
                    Width = 18, Height = 18,
                    Stretch = global::Avalonia.Media.Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            catch { return null; }
        }

        private static TextBlock SectionHeader(string text) => new()
        {
            Text = text,
            FontWeight = FontWeight.SemiBold,
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 2),
            Opacity = 0.85
        };

        private static Grid LabeledRow(string label, Control editor)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("110,*") };
            var lbl = new TextBlock { Text = label, Opacity = 0.7, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lbl, 0); grid.Children.Add(lbl);
            Grid.SetColumn(editor, 1); grid.Children.Add(editor);
            grid.Margin = new Thickness(0, 2);
            return grid;
        }

        private static Grid ReadOnlyRow(string label, string value)
        {
            var lbl = new TextBlock { Text = label, Opacity = 0.7, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            var val = new TextBlock { Text = value, FontSize = 12, FontFamily = new FontFamily("Cascadia Code,Consolas,monospace"), TextWrapping = TextWrapping.Wrap };
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("110,*"), Margin = new Thickness(0, 2) };
            Grid.SetColumn(lbl, 0); grid.Children.Add(lbl);
            Grid.SetColumn(val, 1); grid.Children.Add(val);
            return grid;
        }

        private static void AddBoolCheckbox(StackPanel host, string label, bool value, Action<bool> setter)
        {
            var cb = new CheckBox { Content = label, IsChecked = value, FontSize = 12 };
            cb.IsCheckedChanged += (_, _) => setter(cb.IsChecked == true);
            host.Children.Add(cb);
        }

        private void OnDuplicateClicked(object? sender, RoutedEventArgs e) => DuplicateSelected();
        private void OnRotateClicked(object? sender, RoutedEventArgs e) => RotateSelected();
        private void OnDeleteClicked(object? sender, RoutedEventArgs e) => DeleteSelected();

        /// <summary>Bottom-bar FAB: flip between Light (parchment) and Dark
        /// (navy) themes. Updates the icon glyph to reflect the *next* theme,
        /// like a flashlight toggle.</summary>
        private void OnToggleThemeClick(object? sender, RoutedEventArgs e)
        {
            App.ToggleTheme();
            UpdateThemeToggleIcon();
        }

        private void UpdateThemeToggleIcon()
        {
            // No-op for the new Anno menu layout: the theme toggle is now a
            // toolbar-entry Border with an Anno PNG, not a font Button.
        }

        private void DuplicateSelected()
        {
            if (_currentMap is null || _selectedElementForPanel is not IslandElement src) return;

            IslandElement? clone = CloneIslandElement(src);
            if (clone is null) return;

            // Offset by 16 tiles diagonally so the duplicate is visible.
            Vector2 offset = new(16, 16);
            clone.Position = src.Position + offset;

            _currentMap.Elements.Add(clone);
            UndoRedoStack.Instance.Do(new IslandAddStackEntry(clone, _currentMap));
            RefreshAfterEdit();
            RefreshUndoRedoButtons();
            if (_statusBar != null) _statusBar.Text = "Île dupliquée.";
        }

        private static IslandElement? CloneIslandElement(IslandElement src)
        {
            switch (src)
            {
                case FixedIslandElement fixedSrc when fixedSrc.IslandAsset is not null:
                {
                    var clone = new FixedIslandElement(fixedSrc.IslandAsset, fixedSrc.IslandType)
                    {
                        Position = new Vector2(fixedSrc.Position),
                        Label = fixedSrc.Label
                    };

                    // Rotation
                    if (fixedSrc.Rotation.HasValue)
                        clone.Rotation = fixedSrc.Rotation.Value;
                    else
                        clone.RandomizeRotation = true;

                    // Fertilities (deposits, soils, fishing grounds…)
                    clone.RandomizeFertilities = fixedSrc.RandomizeFertilities;
                    clone.Fertilities.Clear();
                    foreach (var fertility in fixedSrc.Fertilities)
                        clone.Fertilities.Add(fertility);

                    // Slot assignments (mines, rivers, oil, etc.)
                    clone.RandomizeSlots = fixedSrc.RandomizeSlots;
                    clone.SlotAssignments.Clear();
                    foreach (var (key, sa) in fixedSrc.SlotAssignments)
                    {
                        clone.SlotAssignments[key] = new SlotAssignment
                        {
                            Slot = sa.Slot,
                            AssignedSlot = sa.AssignedSlot
                        };
                    }

                    return clone;
                }
                case RandomIslandElement randomSrc when randomSrc.IslandSize is not null:
                {
                    return new RandomIslandElement(randomSrc.IslandSize, randomSrc.IslandType)
                    {
                        Position = new Vector2(randomSrc.Position),
                        Label = randomSrc.Label
                    };
                }
            }
            return null;
        }

        private void RotateSelected()
        {
            if (_selectedElementForPanel is not FixedIslandElement fixedIsland) return;
            if (_currentMap is null) return;

            byte oldRotation = fixedIsland.Rotation ?? 0;
            byte newRotation = (byte)((oldRotation + 1) % 4);
            fixedIsland.Rotation = newRotation;

            UndoRedoStack.Instance.Do(new MapElementTransformStackEntry(
                fixedIsland,
                fixedIsland.Position, fixedIsland.Position,
                oldRotation, newRotation));

            // Re-render so the rotation is visible.
            RefreshAfterEdit();
            RefreshUndoRedoButtons();
            OnMapElementSelected(this, fixedIsland);
            if (_statusBar != null) _statusBar.Text = $"Rotation : {newRotation * 90}°";
        }

        private void DeleteSelected()
        {
            if (_currentMap is null) return;
            if (_selectedElementForPanel is not IslandElement target) return;

            _currentMap.Elements.Remove(target);
            UndoRedoStack.Instance.Do(new IslandRemoveStackEntry(target, _currentMap));
            RefreshAfterEdit();
            _selectedElementForPanel = null;
            OnMapElementSelected(this, null);
            RefreshUndoRedoButtons();
            if (_statusBar != null) _statusBar.Text = "Île supprimée.";
        }

        // Legacy method kept for reference — replaced by BuildPropertyControls.
        private static List<PropertyItem> BuildProperties_unused(MapElement element)
        {
            var list = new List<PropertyItem>
            {
                new("Type", element.GetType().Name),
                new("Position", $"({element.Position.X}, {element.Position.Y})")
            };

            switch (element)
            {
                case StartingSpotElement spot:
                    list.Add(new("Player #", (spot.Index + 1).ToString()));
                    break;

                case FixedIslandElement fixedIsland:
                    list.Add(new("Size (tiles)", fixedIsland.SizeInTiles.ToString()));
                    list.Add(new("Island type", fixedIsland.IslandType?.ToString() ?? "?"));
                    if (!string.IsNullOrEmpty(fixedIsland.Label))
                        list.Add(new("Label", fixedIsland.Label));
                    if (fixedIsland.IslandAsset is { } asset)
                    {
                        if (!string.IsNullOrEmpty(asset.DisplayName))
                            list.Add(new("Asset", asset.DisplayName));
                        if (asset.Region is { } region)
                            list.Add(new("Region", region.Name ?? "?"));
                        if (!string.IsNullOrEmpty(asset.FilePath))
                            list.Add(new("File", asset.FilePath));
                        if (asset.Slots is { Count: > 0 } slots)
                            list.Add(new("Slots", slots.Count.ToString()));
                    }
                    if (fixedIsland.Fertilities is { Count: > 0 } fert)
                    {
                        list.Add(new("Fertilities", string.Join(", ",
                            fert.Select(f => f?.Name ?? "?"))));
                    }
                    list.Add(new("Random rotation", fixedIsland.RandomizeRotation ? "yes" : "no"));
                    list.Add(new("Random fertilities", fixedIsland.RandomizeFertilities ? "yes" : "no"));
                    list.Add(new("Random slots", fixedIsland.RandomizeSlots ? "yes" : "no"));
                    break;

                case RandomIslandElement random:
                    list.Add(new("Size", random.IslandSize?.ToString() ?? "?"));
                    list.Add(new("Island type", random.IslandType?.ToString() ?? "?"));
                    list.Add(new("Size (tiles)", random.SizeInTiles.ToString()));
                    if (!string.IsNullOrEmpty(random.Label))
                        list.Add(new("Label", random.Label));
                    break;
            }

            return list;
        }

        private void OnWindowKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                if (e.Key == Key.Z && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    DoUndo();
                    e.Handled = true;
                }
                else if (e.Key == Key.Y || (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Shift)))
                {
                    DoRedo();
                    e.Handled = true;
                }
                else if (e.Key == Key.D)
                {
                    DuplicateSelected();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.R)
            {
                RotateSelected();
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                DeleteSelected();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Captures arrow keys before they navigate the map list / element tree, and
        /// nudges the currently selected island/starting spot by one Anno tile (=8 px,
        /// the native grid the engine snaps to via Vector2.Normalize) per tap, or
        /// 8 tiles (=64 px) with Shift. Skipped when a text input has focus so typing
        /// in NumericUpDown / TextBox keeps working as usual.
        /// </summary>
        private void OnTunnelArrowKeys(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Left && e.Key != Key.Right
                && e.Key != Key.Up && e.Key != Key.Down)
                return;
            // Bail out if a text-editing control has focus.
            var focused = FocusManager?.GetFocusedElement();
            if (focused is TextBox || focused is NumericUpDown || focused is AutoCompleteBox)
                return;
            // NumericUpDown wraps a TextBox; its inner TextBox is what actually has
            // focus when the user is typing. Walk up the parent chain to detect.
            for (var p = focused as Visual; p is not null; p = p.GetVisualParent())
            {
                if (p is NumericUpDown || p is AutoCompleteBox)
                    return;
            }
            // Always swallow arrow keys when an island/spot is selected, even if the
            // nudge is clamped to the current edge (no movement possible). Otherwise
            // the flèche bubbles up to the maps ListBox / element TreeView and steals
            // the selection — which is exactly what we're trying to prevent.
            if (_selectedElementForPanel is IslandElement
                || _selectedElementForPanel is StartingSpotElement)
            {
                int step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 64 : 8;
                NudgeSelected(e.Key, step);
                e.Handled = true;
            }
        }

        /// <summary>
        /// Move the currently selected island/starting spot by <paramref name="step"/> tiles
        /// in the direction of the arrow key. Pushes a single undo entry so each tap
        /// is reversible. Returns true when the move actually happened (selection was a
        /// movable element and the new position is inside the map).
        /// </summary>
        private bool NudgeSelected(Key key, int step)
        {
            if (_selectedElementForPanel is not (IslandElement or StartingSpotElement) || _currentMap is null)
                return false;
            MapElement el = _selectedElementForPanel;
            int dx = 0, dy = 0;
            switch (key)
            {
                case Key.Left:  dx = -step; break;
                case Key.Right: dx = +step; break;
                case Key.Up:    dy = +step; break; // Anno Y is up
                case Key.Down:  dy = -step; break;
                default: return false;
            }
            int rawSize = el is IslandElement isl && isl.SizeInTiles > 0 ? isl.SizeInTiles : 24;
            int size = System.Math.Clamp(rawSize, 24, System.Math.Max(24, _currentMap.Size.X));
            int maxX = System.Math.Max(0, _currentMap.Size.X - size);
            int maxY = System.Math.Max(0, _currentMap.Size.Y - size);
            int newX = System.Math.Clamp(el.Position.X + dx, 0, maxX);
            int newY = System.Math.Clamp(el.Position.Y + dy, 0, maxY);
            if (newX == el.Position.X && newY == el.Position.Y) return false;

            var oldPos = new Vector2(el.Position);
            var newPos = new Vector2(newX, newY);
            // Apply the move FIRST, then record an undo entry — same pattern the
            // mouse drag uses. UndoRedoStack.Do() only memoises old/new state, it
            // doesn't replay the change, so an unapplied entry would silently no-op.
            el.Position = newPos;
            UndoRedoStack.Instance.Do(new MapElementTransformStackEntry(el, oldPos, newPos));
            // Light refresh: just reposition the existing visual rather than rebuilding
            // the whole canvas + refit the viewport. SetMap() would zoom-reset and
            // SelectElement() would re-flash + re-scroll on every keypress, which is
            // jarring when the user is repeatedly nudging an island into place.
            // The selection (and its border highlight) is already on this element so
            // there's nothing else to do beyond the position update + the live readout.
            _mapRenderer?.RefreshElementPositions();
            OnMapElementMovedLive(this, el);
            RebuildElementsTree();
            RefreshIssuesPanel();
            return true;
        }

        private void OnUndoClicked(object? sender, RoutedEventArgs e) => DoUndo();
        private void OnRedoClicked(object? sender, RoutedEventArgs e) => DoRedo();

        private void DoUndo()
        {
            UndoRedoStack.Instance.Undo();
            _mapRenderer?.RefreshElementPositions();
            RebuildElementsTree();
            if (_selectedElementForPanel != null)
                OnMapElementSelected(this, _selectedElementForPanel);
            RefreshUndoRedoButtons();
        }

        private void DoRedo()
        {
            UndoRedoStack.Instance.Redo();
            _mapRenderer?.RefreshElementPositions();
            RebuildElementsTree();
            if (_selectedElementForPanel != null)
                OnMapElementSelected(this, _selectedElementForPanel);
            RefreshUndoRedoButtons();
        }

        private MapElement? _selectedElementForPanel;
        private MapTemplate? _currentMap;
        private MapListItem? _currentMapItem;
        private StackPanel? _actionButtons;
        private Button? _rotateButton;
        private Button? _saveModButton;

        private void RefreshUndoRedoButtons()
        {
            if (_undoButton != null) _undoButton.IsEnabled = UndoRedoStack.Instance.UndoStackAvailable;
            if (_redoButton != null) _redoButton.IsEnabled = UndoRedoStack.Instance.RedoStackAvailable;
        }

        private void OnHistoryItemSelected(object? sender, SelectionChangedEventArgs e)
        {
            // Defer: we can't mutate the same ObservableCollection while ListBox is reacting to it.
            if (_historyList?.SelectedItem is UndoRedoStack.HistoryEntry entry)
            {
                int target = entry.Index;
                Dispatcher.UIThread.Post(() =>
                {
                    while (UndoRedoStack.Instance.UndoStackAvailable
                           && UndoRedoStack.Instance.UndoHistory.Count > 0
                           && UndoRedoStack.Instance.UndoHistory.First().Index > target)
                    {
                        UndoRedoStack.Instance.Undo();
                    }
                    _mapRenderer?.RefreshElementPositions();
                    if (_selectedElementForPanel != null)
                        OnMapElementSelected(this, _selectedElementForPanel);
                    RefreshUndoRedoButtons();
                    if (_historyList != null) _historyList.SelectedItem = null;
                }, DispatcherPriority.Background);
            }
        }

        private async void OnSaveModClicked(object? sender, RoutedEventArgs e)
        {
            if (_currentMap is null || _currentMapItem is null) return;

            string defaultName;
            if (_currentMapItem.IsMod && _currentMapItem.ModFolderName != null)
            {
                // Reusing an existing mod: propose its name back so the same folder is updated.
                defaultName = _currentMapItem.ModFolderName.StartsWith("AME_")
                    ? _currentMapItem.ModFolderName.Substring(4).Replace('_', ' ')
                    : _currentMapItem.ModFolderName;
            }
            else
            {
                defaultName = _currentMapItem.DisplayName.Replace("★", "").Replace("DLC01", "").Trim() + " (custom)";
            }

            string? modName;
            while (true)
            {
                var dialog = new SaveModDialog(defaultName);
                modName = await dialog.ShowDialog<string?>(this);
                if (string.IsNullOrWhiteSpace(modName)) return;

                if (Anno117ModWriter.IsReservedName(modName))
                {
                    if (_statusBar != null)
                        _statusBar.Text = Localizer.Current.Format("status.reserved_name", modName);
                    defaultName = modName + " v2";
                    continue;
                }
                break;
            }

            string? modsRoot = Settings.Instance.ModsPath;
            if (string.IsNullOrEmpty(modsRoot))
            {
                if (_statusBar != null) _statusBar.Text = Localizer.Current["status.mods_path_unknown"];
                return;
            }

            try
            {
                if (_statusBar != null) _statusBar.Text = Localizer.Current.Format("status.writing_mod", modName);
                var writer = new Anno117ModWriter();

                string folder = _currentMapItem.IsMod
                    // Updating an existing mod: only rewrite the .a7t/.a7te/.a7tinfo in-place.
                    ? await writer.UpdateExistingModAsync(_currentMap, _currentMapItem.AbsoluteFilePath!)
                    // Fresh export from a vanilla map: full mod scaffolding.
                    : await writer.WriteAsync(_currentMap,
                          _currentMapItem.Asset ?? throw new Exception("No source asset available."),
                          _currentMapItem.TemplatePath, modName, modsRoot);

                if (_statusBar != null)
                    _statusBar.Text = Localizer.Current.Format("status.mod_written", folder);
            }
            catch (Exception ex)
            {
                if (_statusBar != null)
                    _statusBar.Text = Localizer.Current.Format("status.export_failed", ex.Message);
            }
        }

        private void OnBackToStart(object? sender, RoutedEventArgs e)
        {
            var start = new StartWindow();
            start.Show();
            Close();
        }

        private async void OnSettingsClicked(object? sender, RoutedEventArgs e)
        {
            var dialog = new SettingsDialog();
            await dialog.ShowDialog(this);
        }

        // ------------------- Biome / Map tabs (concept) -------------------
        // Pour cette première itération, ces handlers se contentent de
        // refléter visuellement la sélection (titre du panneau Place Islands +
        // badge difficulté + StatusBar). Le câblage à la session/template
        // active dans DataManager viendra avec le ViewModel dédié.

        private void OnBiomeSelected(object? sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb || rb.IsChecked != true) return;
            string biomeId = rb.Tag as string ?? "latium";
            string biomeLabel = biomeId switch
            {
                "albion"  => "ALBION",
                "desert"  => "DÉSERT",
                "nordic"  => "NORDIQUE",
                _         => "LATIUM",
            };
            var title = this.FindControl<TextBlock>("PlaceIslandsBiomeTitle");
            if (title is not null) title.Text = biomeLabel;

            if (_statusBar is not null)
                _statusBar.Text = $"Biome actif : {biomeLabel}";
        }

        private void OnMapTabSelected(object? sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb || rb.IsChecked != true) return;
            int idx = int.TryParse(rb.Tag as string, out int i) ? i : 0;
            string diffLabel = idx switch
            {
                1 => "NORMAL",
                2 => "DIFFICILE",
                _ => "FACILE",
            };
            var label = this.FindControl<TextBlock>("DifficultyBadgeLabel");
            if (label is not null) label.Text = diffLabel;

            if (_statusBar is not null)
                _statusBar.Text = $"Carte {idx + 1} · {diffLabel}";
        }

        // ------------------- Elements TreeView -------------------

        private void RebuildElementsTree()
        {
            if (_elementsTree is null) return;
            _suppressTreeEvents = true;
            try { _elementsTree.ItemsSource = MapElementsTreeBuilder.Build(_currentMap); }
            finally { _suppressTreeEvents = false; }
        }

        /// <summary>
        /// Re-render the canvas AND rebuild the categorized tree. Call this after every edit
        /// that may have changed an element's category (IslandType, IslandSize, add, delete,
        /// rotate, etc.) — otherwise the tree shows stale categories.
        /// </summary>
        private void RefreshAfterEdit()
        {
            if (_currentMap != null) _mapRenderer?.SetMap(_currentMap);
            RebuildElementsTree();
            RefreshIssuesPanel();
        }

        /// <summary>
        /// Re-run validation and rebuild the issue list. Called after every edit
        /// and on every map load.
        /// </summary>
        private void RefreshIssuesPanel()
        {
            if (_issuesPanel is null) return;
            _issuesPanel.Children.Clear();

            var issues = MapTemplateValidator.Validate(_currentMap);

            if (issues.Count == 0)
            {
                _issuesPanel.Children.Add(new TextBlock
                {
                    Text = Localizer.Current["main.issues_ok"],
                    Foreground = new SolidColorBrush(Color.Parse("#6FE07F")),
                    FontSize = 12
                });
                return;
            }

            // Severity-aware colors. "error" issues represent things the engine WILL
            // get wrong at runtime (red), "warn" things the user should review (orange),
            // "info" advisory only (gold). Pick the worst severity for the header.
            bool hasError = issues.Any(i => i.Severity == "error");
            bool hasWarn  = issues.Any(i => i.Severity == "warn");
            IBrush headerBrush = hasError
                ? new SolidColorBrush(Color.Parse("#FF1744"))   // red
                : hasWarn
                    ? new SolidColorBrush(Color.Parse("#FF9800"))   // orange
                    : new SolidColorBrush(Color.Parse("#F5C842"));  // gold

            _issuesPanel.Children.Add(new TextBlock
            {
                Text = Localizer.Current.Format("main.issues_count", issues.Count),
                Foreground = headerBrush,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold
            });
            foreach (var issue in issues)
            {
                IBrush lineBrush = issue.Severity switch
                {
                    "error" => new SolidColorBrush(Color.Parse("#FF1744")),
                    "warn"  => new SolidColorBrush(Color.Parse("#FF9800")),
                    _       => Brushes.LightGray
                };
                string bullet = issue.Severity switch
                {
                    "error" => "  ✖ ",
                    "warn"  => "  ⚠ ",
                    _       => "  • "
                };

                // If the issue has at least one target element, render the line as a
                // clickable button that selects the element on the canvas. Otherwise
                // (PA inverted, etc.) keep it as plain text.
                if (issue.Targets.Count > 0)
                {
                    int cursor = 0; // cycles through targets on repeated clicks
                    var btn = new Button
                    {
                        Content = bullet + issue.Message,
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(0, 1),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        FontSize = 11,
                        FontWeight = issue.Severity == "error" ? FontWeight.SemiBold : FontWeight.Normal,
                        Foreground = lineBrush,
                        Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Hand)
                    };
                    ToolTip.SetTip(btn, issue.Targets.Count > 1
                        ? $"Click to cycle through the {issue.Targets.Count} elements"
                        : "Click to focus this element");
                    btn.Click += (_, _) =>
                    {
                        var target = issue.Targets[cursor % issue.Targets.Count];
                        cursor++;
                        _mapRenderer?.SelectElement(target);
                    };
                    _issuesPanel.Children.Add(btn);
                }
                else
                {
                    _issuesPanel.Children.Add(new TextBlock
                    {
                        Text = bullet + issue.Message,
                        Foreground = lineBrush,
                        FontSize = 11,
                        FontWeight = issue.Severity == "error" ? FontWeight.SemiBold : FontWeight.Normal,
                        TextWrapping = TextWrapping.Wrap
                    });
                }
            }
        }

        private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_suppressTreeEvents) return;
            if (_elementsTree?.SelectedItem is not MapElementNode node) return;

            if (node.Element is not null)
            {
                // Forward to the existing single-selection pipeline so the canvas highlights it
                // and the property panel is rebuilt — same code path as a click on the canvas.
                _mapRenderer?.SelectElement(node.Element);
                return;
            }

            if (node.Kind == NodeKind.Zone && node.ZoneId == "PlayableArea")
            {
                // Bring the Session Properties panel into view + tick the edit toggle so the
                // resize handles appear immediately on the canvas.
                _sessionPropsPanel?.BringIntoView();
                if (_sessionEditPaToggle != null) _sessionEditPaToggle.IsChecked = true;
            }
        }

        // ------------------- Playable area editor -------------------

        /// <summary>
        /// Refresh the Session Properties side panel: region, map size, playable-area size
        /// (slider for symmetric resize) and the per-edge advanced fields.
        /// </summary>
        private void PopulatePlayableAreaSection()
        {
            if (_sessionPropsPanel is null) return;
            if (_paX1Box is null || _paY1Box is null || _paX2Box is null || _paY2Box is null) return;

            if (_currentMap is null)
            {
                _sessionPropsPanel.IsVisible = false;
                _originalPlayableArea = null;
                return;
            }

            var pa = _currentMap.PlayableArea;
            int x1 = pa.X;
            int y1 = pa.Y;
            int x2 = pa.X + pa.Width;
            int y2 = pa.Y + pa.Height;

            _originalPlayableArea = (x1, y1, x2, y2);

            int mapSize = _currentMap.Size.X;
            foreach (var box in new[] { _paX1Box, _paY1Box, _paX2Box, _paY2Box })
            {
                box.Minimum = 0;
                box.Maximum = mapSize;
            }
            _paX1Box.Value = x1;
            _paY1Box.Value = y1;
            _paX2Box.Value = x2;
            _paY2Box.Value = y2;

            // Region: display the asset's region id when present (Roman, Celtic, …).
            if (_sessionRegionLabel != null)
                _sessionRegionLabel.Text = _currentMapItem?.Asset?.TemplateRegionId
                                           ?? _currentMap.Session.Region?.Name
                                           ?? "—";

            if (_sessionMapSizeLabel != null)
                _sessionMapSizeLabel.Text = $"{_currentMap.Size.X} × {_currentMap.Size.Y}";

            // Symmetric playable-area slider — its value is the side length of the centered PA
            // square. When the original margins aren't symmetric, take the average and lock the
            // slider to that value; the user can still tweak each edge in the Advanced expander.
            if (_sessionPaSlider != null && _sessionPaSizeLabel != null)
            {
                _suppressSessionPaSlider = true;
                try
                {
                    int paSide = (pa.Width + pa.Height) / 2;
                    _sessionPaSlider.Minimum = 64;
                    _sessionPaSlider.Maximum = mapSize;
                    _sessionPaSlider.Value = paSide;
                    _sessionPaSizeLabel.Text = paSide.ToString();
                }
                finally { _suppressSessionPaSlider = false; }
            }

            _sessionPropsPanel.IsVisible = true;
        }

        private void OnSessionPaSliderChanged(object? sender,
            global::Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_suppressSessionPaSlider || _currentMap is null) return;
            if (_sessionPaSizeLabel is null || _sessionPaSlider is null) return;

            // 1-tile granularity (no snap). The wheel handler still uses ±1 / ±8 with shift,
            // and the user can fine-tune precisely with the keyboard arrows on the slider.
            int side = (int)Math.Round(e.NewValue);
            _sessionPaSizeLabel.Text = side.ToString();

            // Center the playable-area square on the map for a symmetric resize.
            int mapSize = _currentMap.Size.X;
            int margin = (mapSize - side) / 2;
            if (margin < 0) margin = 0;
            int x1 = margin, y1 = margin;
            int x2 = mapSize - margin, y2 = mapSize - margin;

            var pa = _currentMap.PlayableArea;
            var oldArea = (pa.X, pa.Y, pa.X + pa.Width, pa.Y + pa.Height);
            var newArea = (x1, y1, x2, y2);
            if (oldArea == newArea) return;

            _currentMap.ResizeAndCommitMapTemplate(mapSize, newArea);
            // Slider drags should still be undoable, but to avoid spamming the history
            // for every micro-tick we only push when the user releases the thumb. For now
            // each tick is a separate entry — good enough.
            UndoRedoStack.Instance.Do(new PlayableAreaStackEntry(_currentMap, oldArea, newArea));
            RefreshAfterEdit();
            RefreshUndoRedoButtons();
            if (_paX1Box != null) _paX1Box.Value = x1;
            if (_paY1Box != null) _paY1Box.Value = y1;
            if (_paX2Box != null) _paX2Box.Value = x2;
            if (_paY2Box != null) _paY2Box.Value = y2;
            if (_statPlayable != null)
                _statPlayable.Text = $"{_currentMap.PlayableArea.Width}×{_currentMap.PlayableArea.Height}";
        }

        private void OnSessionEditPaToggleChanged(object? sender, RoutedEventArgs e)
        {
            if (_mapRenderer == null || _sessionEditPaToggle == null) return;
            _mapRenderer.EditPlayableArea = _sessionEditPaToggle.IsChecked == true;
        }

        // Mouse wheel over the playable-area slider nudges the value 1 tile at a time.
        // Holding Shift gives ±8 (one snap step) for faster sweeps.
        private void OnSessionPaSliderWheel(object? sender, PointerWheelEventArgs e)
        {
            if (_sessionPaSlider is null) return;
            int step = (e.KeyModifiers & KeyModifiers.Shift) != 0 ? 8 : 1;
            int direction = e.Delta.Y > 0 ? +1 : -1;
            double next = _sessionPaSlider.Value + step * direction;
            next = Math.Clamp(next, _sessionPaSlider.Minimum, _sessionPaSlider.Maximum);
            _sessionPaSlider.Value = next;
            e.Handled = true;
        }

        private void OnApplyPlayableArea(object? sender, RoutedEventArgs e)
        {
            if (_currentMap is null
                || _paX1Box?.Value is not { } x1d || _paY1Box?.Value is not { } y1d
                || _paX2Box?.Value is not { } x2d || _paY2Box?.Value is not { } y2d)
                return;

            int x1 = (int)x1d, y1 = (int)y1d, x2 = (int)x2d, y2 = (int)y2d;

            // Sanity: x2 must be greater than x1 (and same for y) — otherwise the area is
            // inverted and Anno will probably refuse the map.
            if (x2 <= x1 || y2 <= y1)
            {
                if (_statusBar != null)
                    _statusBar.Text = "⚠ Playable area invalid: x2 must be > x1 and y2 > y1.";
                return;
            }

            var pa = _currentMap.PlayableArea;
            var oldArea = (pa.X, pa.Y, pa.X + pa.Width, pa.Y + pa.Height);
            var newArea = (x1, y1, x2, y2);
            if (oldArea == newArea) return;

            _currentMap.ResizeAndCommitMapTemplate(_currentMap.Size.X, newArea);
            UndoRedoStack.Instance.Do(new PlayableAreaStackEntry(_currentMap, oldArea, newArea));
            RefreshAfterEdit();
            RefreshUndoRedoButtons();
            if (_statSize != null) _statSize.Text = $"{_currentMap.Size.X}×{_currentMap.Size.Y}";
            if (_statPlayable != null)
                _statPlayable.Text = $"{_currentMap.PlayableArea.Width}×{_currentMap.PlayableArea.Height}";
            if (_statusBar != null)
                _statusBar.Text = $"Playable area: ({x1}, {y1}) → ({x2}, {y2})";
        }

        private void OnResetPlayableArea(object? sender, RoutedEventArgs e)
        {
            if (_originalPlayableArea is not { } orig
                || _paX1Box is null || _paY1Box is null || _paX2Box is null || _paY2Box is null)
                return;
            _paX1Box.Value = orig.x1;
            _paY1Box.Value = orig.y1;
            _paX2Box.Value = orig.x2;
            _paY2Box.Value = orig.y2;
        }

        // After an island finishes its drag, re-run validation + rebuild the categorized
        // tree so a fresh duplicate-position issue (or a fixed one) shows up immediately.
        private void OnElementMoveCommitted(object? sender, MapElement element)
        {
            RebuildElementsTree();
            RefreshIssuesPanel();
        }

        // Live-update the Position readout in the right pane while the user drags.
        // Fires per pointer-move frame (cheap: just rewrites a TextBlock string), so the
        // user gets the current X/Y in real time without waiting for the drag commit.
        private void OnMapElementMovedLive(object? sender, MapElement element)
        {
            if (_positionValueText is null) return;
            if (!ReferenceEquals(_positionTrackedElement, element)) return;
            _positionValueText.Text = $"({element.Position.X}, {element.Position.Y})";
        }

        /// <summary>
        /// Right-click on the canvas. Empty ocean → "Add island here". Existing island →
        /// Duplicate / Replace + (for fixed islands with the random toggle still on)
        /// "Uncheck random fertilities / slots", which flips the flag and refreshes the
        /// properties panel so the user can configure the now-fixed values manually.
        /// </summary>
        private void OnMapContextMenuRequested(object? sender, (MapElement? element, Vector2 mapPos) args)
        {
            var menu = new ContextMenu();

            if (args.element is null)
            {
                var add = new MenuItem { Header = "Ajouter une île ici" };
                add.Click += async (_, _) => await AddIslandAtPosition(args.mapPos);
                menu.Items.Add(add);

                // Anno templates expect exactly 4 starting spots (indices 0..3, one per
                // player). If any are missing, surface an "Add starting spot" entry on the
                // empty-ocean menu so the user can repair the template in one click.
                int missingIdx = FindMissingStartingSpotIndex();
                if (missingIdx >= 0)
                {
                    var addSpot = new MenuItem { Header = $"Ajouter point de départ #{missingIdx + 1} ici" };
                    addSpot.Click += (_, _) => AddStartingSpotAt(args.mapPos, missingIdx);
                    menu.Items.Add(addSpot);
                }
            }
            else
            {
                // Pin the click target as the current selection so DuplicateSelected /
                // OpenIslandPicker / property toggles all act on it.
                _selectedElementForPanel = args.element;
                _mapRenderer?.ReSelectQuiet(args.element);

                if (args.element is IslandElement isl)
                {
                    var dup = new MenuItem { Header = "Dupliquer" };
                    dup.Click += (_, _) => DuplicateSelected();
                    menu.Items.Add(dup);

                    var rep = new MenuItem { Header = "Remplacer…" };
                    rep.Click += async (_, _) => await OpenIslandPicker(isl);
                    menu.Items.Add(rep);
                }

                if (args.element is FixedIslandElement fi)
                {
                    if (fi.RandomizeFertilities)
                    {
                        var unc = new MenuItem { Header = "Décocher random fertilités" };
                        unc.Click += (_, _) =>
                        {
                            fi.RandomizeFertilities = false;
                            // Rebuild the properties pane so the now-revealed manual
                            // fertility editor is visible and ready for input.
                            OnMapElementSelected(this, fi);
                        };
                        menu.Items.Add(unc);
                    }
                    if (fi.RandomizeSlots)
                    {
                        var unc = new MenuItem { Header = "Décocher random slots" };
                        unc.Click += (_, _) =>
                        {
                            fi.RandomizeSlots = false;
                            OnMapElementSelected(this, fi);
                        };
                        menu.Items.Add(unc);
                    }
                }

                // Always offer Delete on islands and starting spots.
                if (args.element is IslandElement || args.element is StartingSpotElement)
                {
                    var del = new MenuItem { Header = "Supprimer" };
                    del.Click += (_, _) => DeleteSelected();
                    menu.Items.Add(del);
                }
            }

            if (menu.Items.Count == 0) return;
            menu.Open(this);
        }

        /// <summary>
        /// Returns the lowest player index in 0..3 that has no StartingSpotElement
        /// in the current map, or -1 if all four are already placed.
        /// </summary>
        private int FindMissingStartingSpotIndex()
        {
            if (_currentMap is null) return -1;
            var taken = new System.Collections.Generic.HashSet<int>(
                _currentMap.Elements.OfType<StartingSpotElement>().Select(s => s.Index));
            for (int i = 0; i < 4; i++)
                if (!taken.Contains(i)) return i;
            return -1;
        }

        /// <summary>
        /// Add a StartingSpotElement at <paramref name="mapPos"/> with the given player
        /// index. Pushes an undo entry so Ctrl+Z removes it. Refreshes the canvas + tree
        /// + issues panel so the new spot appears immediately.
        /// </summary>
        private void AddStartingSpotAt(Vector2 mapPos, int index)
        {
            if (_currentMap is null) return;
            var spot = new StartingSpotElement
            {
                Position = mapPos,
                Index = index
            };
            _currentMap.Elements.Add(spot);
            UndoRedoStack.Instance.Do(new MapElementAddStackEntry(spot, _currentMap));
            RefreshAfterEdit();
            RefreshUndoRedoButtons();
            _mapRenderer?.SelectElement(spot);
        }

        /// <summary>
        /// Spawn a fresh island at the given map-space position via the standard picker.
        /// Filters available choices by the current map's region. Records an undo entry so
        /// Ctrl+Z removes the new island.
        /// </summary>
        private async System.Threading.Tasks.Task AddIslandAtPosition(Vector2 mapPos)
        {
            if (_currentMap is null) return;

            string? regionId = _currentMapItem?.Asset?.TemplateRegionId
                               ?? _currentMap.Session?.Region?.RegionID;

            var choices = new List<IslandChoice>();
            // Same Continental/ExtraLarge guard as the Replace flow: those sizes are
            // only valid in very specific contexts, hide them for a from-scratch add.
            foreach (var size in IslandSize.All)
            {
                if (size == IslandSize.Continental) continue;
                if (size == IslandSize.ExtraLarge) continue;
                choices.Add(IslandChoice.ForRandom(size));
            }
            try
            {
                foreach (var asset in DataManager.Instance.IslandRepository)
                {
                    if (asset is null) continue;
                    if (regionId is not null
                        && !string.Equals(asset.Region?.RegionID, regionId,
                            System.StringComparison.OrdinalIgnoreCase))
                        continue;
                    choices.Add(IslandChoice.ForFixed(asset));
                }
            }
            catch { /* repo not initialized — random buckets only */ }

            var dialog = new IslandPickerDialog(choices, "Ajouter une île");
            var picked = await dialog.ShowDialog<IslandChoice?>(this);
            if (picked is null) return;

            IslandElement newEl;
            if (picked.IsRandom && picked.RandomSize is { } rs)
            {
                newEl = new RandomIslandElement(rs, IslandType.Normal)
                {
                    Position = mapPos
                };
            }
            else if (picked.IsFixed && picked.FixedAsset is { } fa)
            {
                var assetType = fa.IslandType?.FirstOrDefault() ?? IslandType.Normal;
                newEl = new FixedIslandElement(fa, assetType)
                {
                    Position = mapPos
                };
            }
            else
            {
                return;
            }

            _currentMap.Elements.Add(newEl);
            UndoRedoStack.Instance.Do(new IslandAddStackEntry(newEl, _currentMap));
            RefreshAfterEdit();
            RefreshUndoRedoButtons();
            _mapRenderer?.SelectElement(newEl);
        }

        // ------------------- Replace (convert Random ↔ Fixed) -------------------

        private void BuildReplaceSection(StackPanel host, IslandElement island)
        {
            host.Children.Add(SectionHeader(Localizer.Current["main.replace_section"]));

            // Single "Browse islands…" button for both Random and Fixed elements. The picker
            // shows the union of compatible options with a 3-state filter (All/Random/Fixed)
            // so the user can swap freely between kinds at the same position.
            var browse = new Button
            {
                Content = Localizer.Current["main.replace_browse"],
                Classes = { "accent" },
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            browse.Click += async (_, _) => await OpenIslandPicker(island);
            host.Children.Add(browse);

        }

        /// <summary>
        /// Open the visual island picker with both Random sizes and compatible Fixed assets,
        /// then apply the user's choice as either a same-kind tweak (e.g. resize a Random)
        /// or a kind conversion (Random↔Fixed). All paths preserve position + label and
        /// push a single grouped undo entry.
        /// </summary>
        private async System.Threading.Tasks.Task OpenIslandPicker(IslandElement island)
        {
            if (_currentMap is null) return;

            // Region filter: prefer the source MapTemplateAsset (vanilla maps) and fall
            // back to the loaded session for mods.
            string? regionId = _currentMapItem?.Asset?.TemplateRegionId
                               ?? _currentMap.Session?.Region?.RegionID;

            // Size window: compatible if the Fixed asset is roughly the same tile count
            // as the source island. ±40 % is generous enough to cover small/medium overlap
            // without proposing wildly bigger islands.
            int expected = island is RandomIslandElement r
                ? r.IslandSize.DefaultSizeInTiles
                : island.SizeInTiles;
            if (expected <= 0) expected = 192;
            int min = (int)(expected * 0.6);
            int max = (int)(expected * 1.4);

            var choices = new List<IslandChoice>();

            // Random "buckets" — Anno 117 vanilla observations:
            //   - Small/Medium/Large are used for every random pool island.
            //   - ExtraLarge (<Size>0300</Size>) is used EXCLUSIVELY for the 4 starter spots
            //     of every map (always with Type.id = 0100 / Starter).
            //   - Continental (<Size> never; emitted via <IslandSize><value><id>0600</id></value></IslandSize>)
            //     is reserved for the unique continental_01 fixed asset of DLC1 expanded.
            // We hide Continental unconditionally and ExtraLarge for non-starter elements so
            // the user can't produce templates the engine misinterprets.
            bool isStarterContext = island.IslandType == IslandType.Starter;
            foreach (var size in IslandSize.All)
            {
                if (size == IslandSize.Continental) continue;
                if (size == IslandSize.ExtraLarge && !isStarterContext) continue;
                choices.Add(IslandChoice.ForRandom(size));
            }

            // Fixed assets, filtered by region + size.
            try
            {
                foreach (var asset in DataManager.Instance.IslandRepository)
                {
                    if (asset is null) continue;
                    if (asset.SizeInTiles < min || asset.SizeInTiles > max) continue;
                    if (regionId is not null
                        && !string.Equals(asset.Region?.RegionID, regionId,
                            System.StringComparison.OrdinalIgnoreCase))
                        continue;
                    choices.Add(IslandChoice.ForFixed(asset));
                }
            }
            catch { /* repo not initialized — show only random buckets */ }

            var dialog = new IslandPickerDialog(choices,
                Localizer.Current["main.replace_pick_fixed"]);
            var picked = await dialog.ShowDialog<IslandChoice?>(this);
            if (picked is null) return;

            ApplyPickerChoice(island, picked);
        }

        private void ApplyPickerChoice(IslandElement source, IslandChoice choice)
        {
            if (_currentMap is null) return;

            // Random → Random of a different size: just mutate IslandSize. No element swap,
            // no undo group needed — the existing IslandPropertiesStackEntry covers it.
            if (source is RandomIslandElement r && choice.IsRandom && choice.RandomSize is { } rs)
            {
                if (r.IslandSize == rs) return;
                var oldSize = r.IslandSize;
                r.IslandSize = rs;
                UndoRedoStack.Instance.Do(new IslandPropertiesStackEntry(
                    r, oldIslandSize: oldSize, newIslandSize: rs));
                RefreshAfterEdit();
                RefreshUndoRedoButtons();
                _mapRenderer?.SelectElement(r);
                return;
            }

            // Random → Fixed
            if (source is RandomIslandElement rr && choice.IsFixed && choice.FixedAsset is { } fa)
            {
                ConvertRandomToFixed(rr, fa);
                return;
            }

            // Fixed → Random
            if (source is FixedIslandElement f && choice.IsRandom)
            {
                ConvertFixedToRandom(f);
                return;
            }

            // Fixed → Fixed (different asset): swap the underlying asset. We rebuild the
            // element to keep the new SizeInTiles consistent with the chosen asset.
            if (source is FixedIslandElement fSrc && choice.IsFixed && choice.FixedAsset is { } newAsset)
            {
                var replacement = new FixedIslandElement(newAsset, fSrc.IslandType ?? IslandType.Normal)
                {
                    Position = new Vector2(fSrc.Position),
                    Label = fSrc.Label
                };
                ReplaceElement(fSrc, replacement);
            }
        }

        private void ConvertFixedToRandom(FixedIslandElement source)
        {
            if (_currentMap is null) return;
            // Map the asset's tile count back to an IslandSize enum value.
            int sz = source.IslandAsset.SizeInTiles;
            var size = IslandSize.All
                .OrderBy(s => System.Math.Abs(s.DefaultSizeInTiles - sz))
                .First();
            var replacement = new RandomIslandElement(size, source.IslandType ?? IslandType.Normal)
            {
                Position = new Vector2(source.Position),
                Label = source.Label
            };
            ReplaceElement(source, replacement);
        }

        private void ConvertRandomToFixed(RandomIslandElement source, IslandAsset asset)
        {
            if (_currentMap is null) return;
            var replacement = new FixedIslandElement(asset, source.IslandType ?? IslandType.Normal)
            {
                Position = new Vector2(source.Position),
                Label = source.Label
            };
            ReplaceElement(source, replacement);
            if (_statusBar != null)
                _statusBar.Text = "⚠ Random→Fixed : la position de la random n'était qu'approximative — "
                                + "déplace l'île dans l'éditeur si elle apparaît sous l'eau en jeu.";
        }

        /// <summary>Swap one element for another in-place. Records a grouped undo entry
        /// (remove + add) so a single Ctrl+Z reverts the conversion.</summary>
        private void ReplaceElement(IslandElement oldElement, IslandElement newElement)
        {
            if (_currentMap is null) return;
            _currentMap.Elements.Remove(oldElement);
            _currentMap.Elements.Add(newElement);
            UndoRedoStack.Instance.Do(new GroupStackEntry(new List<IUndoRedoStackEntry>
            {
                new IslandRemoveStackEntry(oldElement, _currentMap),
                new IslandAddStackEntry(newElement, _currentMap)
            }));
            _selectedElementForPanel = newElement;
            RefreshAfterEdit();
            RefreshUndoRedoButtons();
            // Re-select the new element so the Properties panel reflects it.
            _mapRenderer?.SelectElement(newElement);
            if (_statusBar != null)
                _statusBar.Text = $"Remplacé : {oldElement.GetType().Name} → {newElement.GetType().Name}";
        }

        // Live drag from the canvas handles → just refresh the editor numbers.
        private void OnPlayableAreaResizing(object? sender, (int x1, int y1, int x2, int y2) live)
        {
            if (_paX1Box is null || _paY1Box is null || _paX2Box is null || _paY2Box is null) return;
            _paX1Box.Value = live.x1;
            _paY1Box.Value = live.y1;
            _paX2Box.Value = live.x2;
            _paY2Box.Value = live.y2;
        }

        // Drag finished → commit through the model and push an undo entry. Same code path
        // the Apply button uses, just with handles instead of typed numbers.
        private void OnPlayableAreaResized(object? sender,
            (int x1, int y1, int x2, int y2, int oldX1, int oldY1, int oldX2, int oldY2) e)
        {
            if (_currentMap is null) return;
            var newArea = (e.x1, e.y1, e.x2, e.y2);
            var oldArea = (e.oldX1, e.oldY1, e.oldX2, e.oldY2);
            if (newArea == oldArea) return;

            _currentMap.ResizeAndCommitMapTemplate(_currentMap.Size.X, newArea);
            UndoRedoStack.Instance.Do(new PlayableAreaStackEntry(_currentMap, oldArea, newArea));
            RefreshAfterEdit();
            RefreshUndoRedoButtons();
            if (_statPlayable != null)
                _statPlayable.Text = $"{_currentMap.PlayableArea.Width}×{_currentMap.PlayableArea.Height}";
            if (_statusBar != null)
                _statusBar.Text = $"Playable area: ({e.x1}, {e.y1}) → ({e.x2}, {e.y2})";
        }
    }
}
