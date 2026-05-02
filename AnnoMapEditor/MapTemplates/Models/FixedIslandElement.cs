using AnnoMapEditor.MapTemplates.Serializing.Models;
using AnnoMapEditor.DataArchives;
using AnnoMapEditor.DataArchives.Assets.Models;
using AnnoMapEditor.DataArchives.Assets.Repositories;
using AnnoMapEditor.MapTemplates.Enums;
using AnnoMapEditor.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using IslandType = AnnoMapEditor.MapTemplates.Enums.IslandType;

namespace AnnoMapEditor.MapTemplates.Models
{
    public class FixedIslandElement : IslandElement
    {
        private static readonly Logger<FixedIslandElement> _logger = new();


        // TODO: Deserialize Fertilities and MineSlotMappings instead of copying from the source template.
        // TODO: Remove _sourceTemplate alltogether
        private readonly Element? _sourceElement;

        public IslandAsset IslandAsset
        {
            get => _islandAsset;
            [MemberNotNull(nameof(_islandAsset))]
            private set
            {
                SetProperty(ref _islandAsset!, value);
                SizeInTiles = _islandAsset.SizeInTiles;
            }
        }
        private IslandAsset _islandAsset;

        // Direct path to the .a7m terrain referenced in the template (independent from the asset
        // resolution). Useful when the IslandRepository cannot resolve an asset (e.g. DLC-specific
        // continental placeholders that are not indexed by IslandType).
        public string? MapFilePath { get; private set; }


        public bool RandomizeRotation
        {
            get => _randomizeRotation;
            set {
                SetProperty(ref _randomizeRotation, value);
                if (!value && Rotation == null) Rotation = 0;
            }
        }
        private bool _randomizeRotation = true;

        public byte? Rotation
        {
            get => _rotation;
            set 
            {
                SetProperty(ref _rotation, value != null ? (byte)(value % 4) : null);
                SetProperty(ref _randomizeRotation, value == null);
            }
        }
        private byte? _rotation;

        public bool RandomizeFertilities
        {
            get => _randomizeFertilities;
            set => SetProperty(ref _randomizeFertilities, value);
        }
        private bool _randomizeFertilities = true;

        public ObservableCollection<FertilityAsset> Fertilities { get; init; } = new();

        public bool RandomizeSlots
        {
            get => _randomizeSlots;
            set => SetProperty( ref _randomizeSlots, value);
        }
        private bool _randomizeSlots = true;

        public Dictionary<long, SlotAssignment> SlotAssignments { get; init; } = new();


        public FixedIslandElement(IslandAsset islandAsset, IslandType islandType)
            : base(islandType)
        {
            IslandAsset = islandAsset;

            foreach (Slot slot in islandAsset.Slots.Values)
            {
                SlotAssignments.Add(slot.ObjectId, new()
                {
                    Slot = slot,
                    AssignedSlot = null
                });
            }
        }


        // ---- Serialization ----

        public FixedIslandElement(Element sourceElement) : base(sourceElement)
        {
            _sourceElement = sourceElement;

            string islandFilePath = sourceElement.MapFilePath
                ?? throw new ArgumentException($"Missing property '{nameof(Element.MapFilePath)}'.");
            MapFilePath = islandFilePath;

            _randomizeRotation = sourceElement.Rotation90 == null;
            _rotation = sourceElement.Rotation90;

            System.Diagnostics.Debug.WriteLineIf(sourceElement.Rotation90 != null, $"Reading source data {sourceElement.Rotation90} to rotation value of {_rotation} on path {islandFilePath}.");

            _randomizeFertilities = sourceElement.RandomizeFertilities != false;
            _randomizeSlots = sourceElement.MineSlotMapping == null || sourceElement.MineSlotMapping.Count == 0;

            LoadIslandDataFromRepository(sourceElement);
        }

        /// <summary>
        /// Loads the Asset data from the IslandRepository. 
        /// Should only be called when the IslandRepository is actually loaded, errors otherwise.
        /// </summary>
        /// <param name="sourceElement">The element from the template.</param>
        /// <exception cref="ArgumentException">The sourceElement does not have a MapFilePath. This is forbidden on FixedIslands.</exception>
        /// <exception cref="NullReferenceException">The given MapFilePath does not match any islands in the IslandRepository.</exception>
        [MemberNotNull(nameof(_islandAsset))]
        private void LoadIslandDataFromRepository(Element sourceElement)
        {
            string islandFilePath = sourceElement.MapFilePath
                ?? throw new ArgumentException($"Missing property '{nameof(Element.MapFilePath)}'.");

            IslandRepository islandRepository = DataManager.Instance.IslandRepository;

            if (!islandRepository.TryGetByFilePath(islandFilePath, out var islandAsset))
                throw new NullReferenceException($"Unknown island '{islandFilePath}'.");

            IslandAsset = islandAsset;
            //Rotation is not asset bound, thus loaded in constructor

            AssetRepository assetRepository = DataManager.Instance.AssetRepository;

            // fertilities
            //  _randomizeFertilities is loaded in constructor.
            if (sourceElement.FertilityGuids != null)
            {
                foreach (int guid in sourceElement.FertilityGuids)
                {
                    if (assetRepository.TryGet(guid, out FertilityAsset? fertility) && fertility != null)
                        Fertilities.Add(fertility);
                    else
                        throw new Exception($"Unrecognized {nameof(FertilityAsset)} for GUID {guid}.");
                }
            }

            // fixed slots
            // _randomizeSlots is loaded in constructor.
            if (sourceElement.MineSlotMapping != null)
            {
                foreach ((long objectId, int slotGuid) in sourceElement.MineSlotMapping)
                {
                    // skip unsupported slots
                    if (!islandAsset.Slots.TryGetValue(objectId, out Slot? slot))
                    {
                        _logger.LogWarning($"Unrecognized {nameof(Slot)} id {objectId} on instance of '{islandFilePath}'. The slot will be skipped and the map may be corrupted.");
                        continue;
                    }

                    SlotAsset? slotAsset;

                    // 0 denotes an empty slot
                    if (slotGuid == 0)
                        slotAsset = null;

                    else if (!assetRepository.TryGet(slotGuid, out slotAsset))
                    {
                        _logger.LogWarning($"Unrecognized {nameof(SlotAsset)} GUID {slotGuid} on instance of '{islandFilePath}'. The slot will be skipped and the map may be corrupted.");
                        continue;
                    }

                    SlotAssignments.Add(objectId, new()
                    {
                        Slot = slot,
                        AssignedSlot = slotAsset
                    });
                }
            }

            // add remaining assignable slots
            foreach (Slot slot in islandAsset.Slots.Values)
            {
                if (!SlotAssignments.ContainsKey(slot.ObjectId) && slot.SlotAsset != null)
                {
                    SlotAssignments.Add(slot.ObjectId, new()
                    {
                        Slot = slot,
                        AssignedSlot = null
                    });
                }
            }
        }

        /// <summary>
        /// Creates an empty IslandAsset that gets replaced with the correct one as soon as the Repository is loaded.
        /// </summary>
        /// <param name="sourceElement">The element from the template.</param>
        /// <exception cref="ArgumentException">The sourceElement does not have a MapFilePath. This is forbidden on FixedIslands.</exception>
        [MemberNotNull(nameof(_islandAsset))]
        private void SetDummyAsset(Element sourceElement)
        {
            var gameDefaults = DataManager.Instance.DetectedGame?.GameDefaults;
            
            if (gameDefaults ==  null)
                throw new NullReferenceException("GameDefaults have not been initialized!.");
            
            string islandFilePath = sourceElement.MapFilePath
                ?? throw new ArgumentException($"Missing property '{nameof(Element.MapFilePath)}'.");

            IslandSize islandSize = IslandRepository.DetectDefaultIslandSizeFromPath(islandFilePath);
            IslandDifficulty? islandDifficulty = IslandDifficulty.FromElementValue(sourceElement.Difficulty?.id);


            IslandAsset dummyAsset = new IslandAsset()
            {
                FilePath = islandFilePath,
                DisplayName = System.IO.Path.GetFileNameWithoutExtension(islandFilePath),
                Thumbnail = null,
                Region = RegionAsset.DetectFromPath(islandFilePath, gameDefaults),
                IslandDifficulty =  new[] { islandDifficulty },
                IslandType = new[] { IslandRepository.DetectIslandTypeFromPath(islandFilePath) },
                IslandSize = new[] { islandSize },
                SizeInTiles = Math.Min(islandSize.DefaultSizeInTiles, 768),
                Slots = new Dictionary<long, Slot>()
            };

            IslandAsset = dummyAsset;
        }

        protected override void ToTemplate(Element resultElement)
        {
            base.ToTemplate(resultElement);

            resultElement.MapFilePath = _islandAsset.FilePath;
            // Vanilla DLC1 always emits <Rotation90> on every fixed island (even =0). Emitting
            // null would drop the tag entirely and the engine reads "no rotation locked" as
            // "randomize" — different behaviour from vanilla and source of subtle bugs at
            // load time. Always serialize a concrete byte: the user-set Rotation if any,
            // otherwise 0 (= no rotation).
            resultElement.Rotation90  = (byte)(Rotation ?? 0);

            // The DLC1 continental asset is the ONLY one that should be tagged as continental.
            // Looking only at the filename avoids false positives from inherited tags on
            // non-continental fixed islands previously exported by buggy editor versions.
            bool isContinentalAsset =
                _islandAsset?.FilePath?.Contains("continental", System.StringComparison.OrdinalIgnoreCase) == true;
            bool isExpandedMap = MapTemplate.IsExportingExpandedMap;

            //
            // Anno 117 RandomizeFertilities convention:
            //   - Expanded map (DLC1, 2688): NO fixed island emits RandomizeFertilities, the
            //     FertilitySet binding handles fertility selection instead.
            //   - Non-expanded map (vanilla 2048, Taludas-style): emit RandomizeFertilities=true
            //     explicitly so the engine randomizes from the regional pool.
            //
            resultElement.RandomizeFertilities = isExpandedMap ? null : (bool?)_randomizeFertilities;

            // Round-trip the empty container tags (presence is what matters for the engine
            // — content is always empty in vanilla anyway).
            if (_sourceElement is not null)
            {
                resultElement.FertilitiesPerAreaIndex = _sourceElement.FertilitiesPerAreaIndex;
                resultElement.MineSlotActivation     = _sourceElement.MineSlotActivation;
                resultElement.FertilitySetGUIDs      = _sourceElement.FertilitySetGUIDs;
            }
            // Vanilla DLC1 expanded templates emit the 5 tags on EVERY fixed island, not just
            // continentals. Auto-generate them when the parent map is expanded so freshly-
            // created/converted fixed islands stay vanilla-compatible.
            // (isExpandedMap is computed earlier in the method.)
            if (isContinentalAsset || isExpandedMap)
            {
                resultElement.FertilitiesPerAreaIndex ??= new();
                resultElement.MineSlotActivation     ??= new();
                resultElement.FertilitySetGUIDs      ??= new();
                // IslandSize.value.id encodes the asset's physical size class so the engine
                // reserves a terrain footprint of the right dimensions. Wrong size → terrain
                // generated for a smaller bucket than the .a7m needs → island spills into
                // the ocean (the "fixed island underwater" bug).
                //   Small/Default = 0   Medium = 1   Large = 2   ExtraLarge = 3   Continental = 6
                //
                // We DON'T trust _sourceElement.IslandSize here: a mod that was exported with a
                // buggy older version of the editor may carry id=0000 on a Large asset, and we
                // want to overwrite it with the correct value computed from the asset itself.
                short sizeId;
                if (isContinentalAsset)
                {
                    sizeId = 6;
                }
                else
                {
                    var assetSize = _islandAsset?.IslandSize?.FirstOrDefault();
                    sizeId = (short)(assetSize?.ElementValue ?? 0);
                }
                resultElement.IslandSize = new() { value = new() { id = sizeId } };
            }
            if (_randomizeFertilities)
                resultElement.FertilityGuids = Array.Empty<int>();
            else
                resultElement.FertilityGuids = Fertilities.Select(f => (int)f.GUID).ToArray();

            //
            // There exists no additional "RandomizeSlots" flag in the templates. Instead slots
            // will be randomized if MineSlotMapping is an empty list. If it contains any elements,
            // all slots will be fixed.
            //
            // 0 is used to set empty slots.
            //
            // Randomized slots:
            //   MineSlotMapping = []
            // Fixed slots
            //   MineSlotMapping = [(8252351, 1000063), (162612, 0), ...]
            //
            // Vanilla DLC1 (continental + campaign fixed) NEVER emits MineSlotMapping. Including
            // it forces the engine onto a slot-randomisation path that interferes with the
            // FertilitySet binding. Only Anno 1800 / Taludas-style 2048 maps emit this tag.
            if (isContinentalAsset || isExpandedMap)
            {
                resultElement.MineSlotMapping = null;
            }
            else if (_randomizeSlots)
                resultElement.MineSlotMapping = new();
            else
                resultElement.MineSlotMapping = IslandAsset.Slots.Values
                    .Select(s =>
                    {
                        int slotGuid = 0;
                        if (SlotAssignments.TryGetValue(s.ObjectId, out SlotAssignment? assignment))
                            slotGuid = (int)(assignment.AssignedSlot?.GUID ?? 0);

                        return new Tuple<long, int>(s.ObjectId, slotGuid);
                    })
                    .ToList();

            // despite its name, all fixed islands must have a RandomIslandConfig.
            // TypePerConstructionArea is always regenerated from the asset shape — we don't
            // trust _sourceElement here because a buggy older export may have stored 3 pairs
            // on a non-continental island, which makes the engine reject the construction
            // wiring (= the fixed island lands in the water).
            //   - Continental (3 ConstructionAreas): 3 pairs (areaIndex 0/1/2 → id 0).
            //   - Other fixed in expanded map (1 CA): 1 pair (areaIndex 1 → id 1) — observed
            //     on vanilla DLC1 campaign roman_island_large_02 and similar starters.
            //   - Non-expanded map (Taludas 2048): no TypePerConstructionArea at all.
            List<Tuple<short, IslandTypeRef>>? inheritedTpca = null;
            if (isContinentalAsset)
            {
                inheritedTpca = new()
                {
                    new(0, new IslandTypeRef { value = new() { id = 0 } }),
                    new(1, new IslandTypeRef { value = new() { id = 0 } }),
                    new(2, new IslandTypeRef { value = new() { id = 0 } })
                };
            }
            else if (isExpandedMap)
            {
                inheritedTpca = new()
                {
                    new(1, new IslandTypeRef { value = new() { id = 1 } })
                };
            }

            // Continentals MUST emit <Type /> empty — putting Type=Starter (or anything else)
            // breaks the FertilitySet wiring, leaving the island with Obsidian only and no
            // valid spawn binding. We override the user's choice silently here. The UI will
            // also disable the Type combo on continental assets (see MainWindow Properties).
            //
            // For NON-continentals, the inverse rule applies: vanilla DLC1 expanded ALWAYS
            // emits a non-empty <Type> (Starter/ThirdParty/PirateIsland depending on role).
            // A bare <Type /> tells the engine "treat this as continental class", routing
            // the island through the Obsidian-only fertility branch — that's the root
            // cause of "all my fixed islands have obsidian" reported on user mods. When
            // the user hasn't picked a specific role, fall back to id=7 (= Normal explicit),
            // the same code vanilla emits on sized random islands outside the 2020 frame.
            short? configTypeId;
            if (isContinentalAsset)
                configTypeId = null;
            else
                configTypeId = IslandType.ElementValue ?? 7;
            resultElement.RandomIslandConfig = new()
            {
                value = new()
                {
                    Type = new() { id = configTypeId },
                    Difficulty = new() { id = IslandDifficulty?.ElementValue },
                    TypePerConstructionArea = inheritedTpca
                }
            };
        }
    }
}
