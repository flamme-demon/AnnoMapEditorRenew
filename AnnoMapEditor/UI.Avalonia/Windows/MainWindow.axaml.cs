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
using AnnoMapEditor.DataArchives;
using AnnoMapEditor.DataArchives.Assets.Models;
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
        private ListBox? _mapsList;
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
        private bool _suppressPropertyEvents;
        private ListBox? _historyList;
        private Button? _undoButton;
        private Button? _redoButton;
        private TreeView? _elementsTree;
        private bool _suppressTreeEvents;
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
            _mapsList = this.FindControl<ListBox>("MapsList");
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
            _actionButtons = this.FindControl<StackPanel>("ActionButtons");
            _rotateButton = this.FindControl<Button>("RotateButton");
            _saveModButton = this.FindControl<Button>("SaveModButton");
            if (_dlcFilters != null) _dlcFilters.ItemsSource = _filters;
            if (_mapRenderer != null) _mapRenderer.ElementSelected += OnMapElementSelected;
            if (_historyList != null)
                _historyList.ItemsSource = UndoRedoStack.Instance.UndoHistory;
            UndoRedoStack.Instance.PropertyChanged += (_, __) => RefreshUndoRedoButtons();
            RefreshUndoRedoButtons();
            KeyDown += OnWindowKeyDown;
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

            // "Base" first, then sorted DLCs
            var distinct = _allMaps
                .Select(m => m.DlcId)
                .Distinct()
                .OrderBy(id => id == "Base" ? 0 : 1)
                .ThenBy(id => id);

            foreach (string id in distinct)
            {
                var f = new DlcFilter(id) { IsEnabled = true };
                f.PropertyChanged += OnFilterChanged;
                _filters.Add(f);
            }
        }

        private void OnFilterChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DlcFilter.IsEnabled))
                ApplyDlcFilters();
        }

        private void ApplyDlcFilters()
        {
            if (_mapsList is null) return;
            HashSet<string> enabled = new(_filters.Where(f => f.IsEnabled).Select(f => f.Id));
            var filtered = _allMaps.Where(m => enabled.Contains(m.DlcId)).ToList();
            _mapsList.ItemsSource = filtered;
            if (_mapsCountLabel != null)
                _mapsCountLabel.Text = $"{filtered.Count} / {_allMaps.Count} cartes";
        }

        private async void OnMapSelected(object? sender, SelectionChangedEventArgs e)
        {
            if (_mapsList?.SelectedItem is not MapListItem item)
                return;

            await LoadMapDetailAsync(item);
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

                // Position (read-only display, modifiable via drag)
                host.Children.Add(ReadOnlyRow("Position", $"({element.Position.X}, {element.Position.Y})"));

                // IslandType — editable for IslandElement
                if (element is IslandElement island)
                {
                    var typeCombo = new ComboBox { Width = 160 };
                    foreach (var t in IslandType.All) typeCombo.Items.Add(t);
                    typeCombo.SelectedItem = island.IslandType ?? IslandType.Normal;
                    ToolTip.SetTip(typeCombo,
                        "Rôle de l'île dans la session (jouable / NPC / déco). " +
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
                        RefreshAfterEdit();
                        RefreshUndoRedoButtons();
                    };
                    host.Children.Add(LabeledRow("Type (rôle)", typeCombo));
                }

                // IslandSize — editable for RandomIslandElement
                if (element is RandomIslandElement random)
                {
                    var sizeCombo = new ComboBox { Width = 160 };
                    foreach (var s in IslandSize.All) sizeCombo.Items.Add(s);
                    sizeCombo.SelectedItem = random.IslandSize;
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
                    AddBoolCheckbox(host, "Random fertilities",
                        fixedIsland.RandomizeFertilities,
                        v => SetFixedFlag(fixedIsland, randomizeFertilities: v));
                    AddBoolCheckbox(host, "Random slots",
                        fixedIsland.RandomizeSlots,
                        v => SetFixedFlag(fixedIsland, randomizeSlots: v));
                }

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
        }

        private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_suppressTreeEvents) return;
            if (_elementsTree?.SelectedItem is MapElementNode node && node.Element is not null)
            {
                // Forward to the existing single-selection pipeline so the canvas highlights it
                // and the property panel is rebuilt — same code path as a click on the canvas.
                _mapRenderer?.SelectElement(node.Element);
            }
        }
    }
}
