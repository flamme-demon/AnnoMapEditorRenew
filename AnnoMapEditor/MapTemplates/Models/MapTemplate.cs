using AnnoMapEditor.MapTemplates.Serializing.Models;
using AnnoMapEditor.DataArchives.Assets.Models;
using AnnoMapEditor.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using AnnoMapEditor.Games;
using FileDBSerializing;

namespace AnnoMapEditor.MapTemplates.Models
{
    public class MapTemplate : ObservableBase
    {
        public ObservableCollection<MapElement> Elements { get; } = new();

        public Vector2 Size
        {
            get => _size;
            private set => SetProperty(ref _size, value, dependendProperties: new[] { nameof(MapSizeText) });
        }
        private Vector2 _size = Vector2.Zero;

        public Rect2 PlayableArea
        {
            get => _playableArea;
            private set => SetProperty(ref _playableArea, value, dependendProperties: new[] { nameof(MapSizeText) });
        }
        private Rect2 _playableArea = new();

        /// <summary>
        /// On Anno 117 DLC1 expanded templates this is the original 2048-tile playable area
        /// (e.g. <c>20 20 2020 2020</c>) preserved alongside the actual <see cref="PlayableArea"/>
        /// (e.g. <c>20 20 2440 2440</c>). Empty / zero-sized on regular non-expanded templates.
        /// Surfaced so the renderer can draw a second cadre that shows where the original 2048
        /// reference frame sits inside the expanded 2688 canvas.
        /// </summary>
        public Rect2 InitialPlayableArea =>
            _templateDocument.MapTemplate?.InitialPlayableArea is { Length: 4 } init
                ? new Rect2(init)
                : new Rect2();

        public SessionAsset Session
        {
            get => _session;
            set => SetProperty(ref _session, value);
        }
        private SessionAsset _session;

        public bool ResizingInProgress
        {
            get => _resizingInProgress;
            set => SetProperty(ref _resizingInProgress, value);
        }
        private bool _resizingInProgress = false;

        private MapTemplateDocument _templateDocument = new();

        public event EventHandler<MapTemplateResizeEventArgs>? MapSizeConfigChanged;

        public event EventHandler? MapSizeConfigCommitted;

        public string MapSizeText => $"Size: {Size.X}, Playable: {PlayableArea.Width}";

        // FileDB binary version detected when loading the .a7tinfo (V1 = Anno 1800, V3 = Anno 117).
        // Re-used at write time so the saved file stays compatible with the source game.
        public FileDBDocumentVersion SourceVersion { get; set; } = FileDBDocumentVersion.Version1;

        // NPCs the engine drops on the map at session start (campaign traders, pirate hubs…).
        // Read via reflection from MapTemplateDocument.MapTemplate.RandomlyPlacedThirdParties
        // because Anno.FileDBModels.dll is a closed-source binary we do not control.
        public IReadOnlyList<NPCPlacement> NPCPlacements { get; private set; } = Array.Empty<NPCPlacement>();

        public bool ShowLabels
        {
            get => _showLabels;
            set => SetProperty(ref _showLabels, value);
        }
        private bool _showLabels = true;

        private MapTemplate()
        {
        }

        public MapTemplate(SessionAsset session) : this()
        {
            _session = session;
            _templateDocument = new MapTemplateDocument();
        }

        public MapTemplate(MapTemplateDocument document, SessionAsset session) : this()
        {
            _session = session;
            _size = new Vector2(document.MapTemplate?.Size);
            _playableArea = new Rect2(document.MapTemplate?.PlayableArea);
            _templateDocument = document;

            // TODO: Allow empty templates?
            int startingSpotCounter = 0;
            foreach (TemplateElement elementTemplate in document.MapTemplate!.TemplateElement!)
            {
                MapElement element = MapElement.FromTemplate(elementTemplate);

                if (element is StartingSpotElement startingSpot)
                    startingSpot.Index = startingSpotCounter++;

                Elements.Add(element);
            }

            NPCPlacements = ExtractNpcPlacements(document.MapTemplate);

            // clear links in the original
            if (_templateDocument.MapTemplate is not null)
                _templateDocument.MapTemplate.TemplateElement = null;
        }

        public MapTemplate(int mapSize, int playableSize, SessionAsset session) : this()
        {
            int margin = (mapSize - playableSize) / 2;

            _templateDocument = new()
            {
                MapTemplate = new()
                {
                    Size = new int[] { mapSize, mapSize },
                    PlayableArea = new int[] { margin, margin, playableSize + margin, playableSize + margin },
                    ElementCount = 4
                }
            };

            _session = session;
            _size = new Vector2(_templateDocument.MapTemplate.Size);
            _playableArea = new Rect2(_templateDocument.MapTemplate.PlayableArea);

            // create starting spots in the default location
            Elements.AddRange(CreateNewStartingSpots(mapSize));
        }


        public static List<StartingSpotElement> CreateNewStartingSpots(int mapSize)
        {
            const int SPACING = 32;
            int halfSize = mapSize / 2;

            List<StartingSpotElement> starts = new()
            {
                new() { Position = new(halfSize + SPACING, halfSize + SPACING) },
                new() { Position = new(halfSize + SPACING, halfSize - SPACING) },
                new() { Position = new(halfSize - SPACING, halfSize - SPACING) },
                new() { Position = new(halfSize - SPACING, halfSize + SPACING) }
            };

            for (int i = 0; i < starts.Count; ++i)
                starts[i].Index = i;

            return starts;
        }

        public void ResizeMapTemplate(int mapSize, (int x1, int y1, int x2, int y2) playableAreaMargins)
        {
            ResizingInProgress = true;

            Vector2 oldMapSize = new(Size);
            Size = new(mapSize, mapSize);

            Vector2 oldPlayableSize = new(PlayableArea.Width, PlayableArea.Height);
            PlayableArea = new(new int[] { playableAreaMargins.x1, playableAreaMargins.y1, playableAreaMargins.x2, playableAreaMargins.y2 });

            MapSizeConfigChanged?.Invoke(this, new MapTemplateResizeEventArgs(oldMapSize, oldPlayableSize));
        }

        public void RestoreMapSizeConfig(int mapSize, Rect2 playableArea)
        {
            // TODO: Implement Map Size Config restoring
        }

        public void ResizeAndCommitMapTemplate(int mapSize, (int x1, int y1, int x2, int y2) playableAreaMargins)
        {
            if (_templateDocument.MapTemplate == null)
                throw new InvalidDataException();

            _templateDocument.MapTemplate.Size = new int[] { mapSize, mapSize };
            _templateDocument.MapTemplate.PlayableArea = new int[] {
                playableAreaMargins.x1,
                playableAreaMargins.y1,
                playableAreaMargins.x2,
                playableAreaMargins.y2
            };

            Vector2 oldMapSize = new(Size);
            Size = new(_templateDocument.MapTemplate.Size);

            Vector2 oldPlayableSize = new(PlayableArea.Width, PlayableArea.Height);
            PlayableArea = new(_templateDocument.MapTemplate.PlayableArea);

            ResizingInProgress = false;

            MapSizeConfigChanged?.Invoke(this, new MapTemplateResizeEventArgs(oldMapSize, oldPlayableSize));
            MapSizeConfigCommitted?.Invoke(this, new EventArgs());
        }

        // Set during ToTemplateDocument so child elements (FixedIslandElement, ...) can decide
        // whether to emit the DLC1-expanded fixed-island idiom (FertilitiesPerAreaIndex /
        // MineSlotActivation / FertilitySetGUIDs / IslandSize / Locked / RandomIslandConfig
        // wrapper). Vanilla DLC1 emits these on EVERY fixed island in expanded templates,
        // not just continentals — without them the FertilitySet binding fails.
        [ThreadStatic]
        internal static bool IsExportingExpandedMap;

        public MapTemplateDocument? ToTemplateDocument(bool writeInitialArea = false)
        {
            if (_templateDocument.MapTemplate?.Size is null || _templateDocument.MapTemplate?.PlayableArea is null)
                return null;

            int sizeForCheck = _templateDocument.MapTemplate.Size?.Length > 0
                ? _templateDocument.MapTemplate.Size[0] : 0;
            bool isExpandedForElements = sizeForCheck > 2048
                                         || _templateDocument.MapTemplate.IsEnlargedTemplate == true;

            try
            {
                IsExportingExpandedMap = isExpandedForElements;
                _templateDocument.MapTemplate.TemplateElement = new List<TemplateElement>(
                    Elements.Select(x => x.ToTemplate()).Where(x => x is not null)!);
            }
            finally
            {
                IsExportingExpandedMap = false;
            }
            _templateDocument.MapTemplate.ElementCount = _templateDocument.MapTemplate.TemplateElement.Count;

            // Anno 117 DLC1 "expanded" templates are 2688×2688 and require a fixed 5-tag header
            // (Size 2688, IsEnlargedTemplate=true, InitialPlayableArea=20,20,2020,2020,
            //  EnlargementOffset=0,0,0,0, PlayableArea=20,20,2440,2440). Vanilla DLC1 emits all
            // 6 templates with these *exact* values — they are NOT customizable. Without them
            // the engine silently falls back to a 2048-tile generator and any island placed
            // beyond 2048 (continentals at the corners, etc.) lands underwater.
            //
            // EnlargementOffset is missing from Anno.FileDBModels.dll's MapTemplate type, so we
            // patch it into the BBDocument post-serialization (see FileDBSerializer.WriteAsync).
            int currentSize = _templateDocument.MapTemplate.Size?.Length > 0
                ? _templateDocument.MapTemplate.Size[0] : 0;
            bool isExpanded = currentSize > 2048
                              || _templateDocument.MapTemplate.IsEnlargedTemplate == true;

            if (isExpanded)
            {
                _templateDocument.MapTemplate.IsEnlargedTemplate = true;
                _templateDocument.MapTemplate.InitialPlayableArea = new[] { 20, 20, 2020, 2020 };
                if (_templateDocument.MapTemplate.EnlargementOffset is null)
                    _templateDocument.MapTemplate.EnlargementOffset = new[] { 0, 0 };
            }
            else if (Session == Anno1800StaticAssets.NewWorldSession)
            {
                _templateDocument.MapTemplate.InitialPlayableArea =
                    _templateDocument.MapTemplate.PlayableArea;
                _templateDocument.MapTemplate.IsEnlargedTemplate = null;
                _templateDocument.MapTemplate.EnlargementOffset = null;
            }
            else
            {
                _templateDocument.MapTemplate.InitialPlayableArea = null;
                _templateDocument.MapTemplate.IsEnlargedTemplate = null;
                _templateDocument.MapTemplate.EnlargementOffset = null;
            }

            return _templateDocument;
        }

        private static IReadOnlyList<NPCPlacement> ExtractNpcPlacements(object? mapTemplateNode)
        {
            if (mapTemplateNode is null) return Array.Empty<NPCPlacement>();
            var result = new List<NPCPlacement>();

            try
            {
                PropertyInfo? listProp = mapTemplateNode.GetType()
                    .GetProperty("RandomlyPlacedThirdParties", BindingFlags.Public | BindingFlags.Instance);
                if (listProp?.GetValue(mapTemplateNode) is not IEnumerable list) return result;

                foreach (object? wrapper in list)
                {
                    if (wrapper is null) continue;

                    object? inner = wrapper.GetType().GetField("value")?.GetValue(wrapper)
                                  ?? wrapper.GetType().GetProperty("value")?.GetValue(wrapper);
                    if (inner is null) continue;

                    Type t = inner.GetType();
                    object? guidObj = ReadField(t, inner, "Guid", "GUID", "id", "ProfileGuid");
                    object? posObj = ReadField(t, inner, "Position", "BasePosition");

                    long guid = guidObj switch
                    {
                        long l => l,
                        int i => i,
                        short s => s,
                        _ => 0
                    };

                    int x = 0, y = 0;
                    if (posObj is float[] fa && fa.Length >= 3)
                    {
                        x = (int)fa[0];
                        y = (int)fa[2];
                    }
                    else if (posObj is int[] ia && ia.Length >= 3)
                    {
                        x = ia[0];
                        y = ia[2];
                    }

                    result.Add(new NPCPlacement(new Vector2(x, y), guid));
                }
            }
            catch
            {
                // Unknown shape: ignore.
            }

            return result;
        }

        private static object? ReadField(Type t, object instance, params string[] names)
        {
            foreach (string name in names)
            {
                FieldInfo? f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (f != null) return f.GetValue(instance);
                PropertyInfo? p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (p != null) return p.GetValue(instance);
            }
            return null;
        }

        public class MapTemplateResizeEventArgs : EventArgs
        {
            public MapTemplateResizeEventArgs(Vector2 oldMapSize, Vector2 oldPlayableSize)
            {
                OldMapSize = new Vector2(oldMapSize);
                OldPlayableSize = new Vector2(oldPlayableSize);
            }

            public Vector2 OldMapSize { get; }
            public Vector2 OldPlayableSize { get; }
        }
    }

    public record NPCPlacement(Vector2 Position, long Guid);
}
