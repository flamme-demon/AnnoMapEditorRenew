using System;
using System.Collections.Generic;
using AnnoMods.BBDom.EncodingAwareStrings;
using AnnoMods.BBDom.ObjectSerializer;

// In-repo replacements for the legacy Anno.FileDBModels.Anno1800.MapTemplate.* types. Two
// reasons we own these now:
//   1. The legacy DLL types reference FileDBSerializing.EncodingAwareStrings.{UnicodeString,
//      UTF8String}. AnnoMods.BBDom's typed serializer only recognizes its OWN namespace
//      (AnnoMods.BBDom.EncodingAwareStrings) — anything else ends up serialized as a raw
//      .NET string and fails with "PropertyType Char could not be resolved".
//   2. The legacy MapTemplate type is missing EnlargementOffset, which Anno 117 DLC1
//      "expanded" 2688×2688 templates require. With it absent, the engine falls back to
//      a 2048-tile generator and any island placed beyond that lands underwater.
//
// Property order matches BBDom's declaration-order serialization. EnlargementOffset and the
// DLC1-specific Element tags (FertilitiesPerAreaIndex / MineSlotActivation / FertilitySetGUIDs
// / IslandSize / TypePerConstructionArea) are placed where vanilla emits them so the engine
// receives bit-equivalent layouts.
//
// The 5 DLC1 Element tags above were missing in the legacy model. Without them, the Vesuvius
// (continental_01) loses its FertilitySet binding and the engine falls back to a single
// fertility (Obsidian) at runtime — they MUST be modelled and round-tripped.
namespace AnnoMapEditor.MapTemplates.Serializing.Models
{
    public class MapTemplateDocument
    {
        public MapTemplate? MapTemplate { get; set; }
    }

    public class MapTemplate
    {
        public int[]? Size { get; set; }
        public int[]? EnlargementOffset { get; set; }
        public int[]? PlayableArea { get; set; }
        public int[]? InitialPlayableArea { get; set; }
        public bool? IsEnlargedTemplate { get; set; }
        // Tag wrapper holding 0..N <None>... children (default list shape).
        public List<RandomlyPlacedThirdParty>? RandomlyPlacedThirdParties { get; set; }
        public int? ElementCount { get; set; }
        // <TemplateElement> tags are emitted as N flat siblings (no wrapper) — needs [FlatArray].
        [FlatArray]
        public List<TemplateElement>? TemplateElement { get; set; }
    }

    public class TemplateElement
    {
        public int? ElementType { get; set; }
        public Element? Element { get; set; }
    }

    public class Element
    {
        // Property order matches vanilla emission across both Anno 1800 (Taludas-style) and
        // Anno 117 (DLC1 expanded) layouts. BBDom serializes in declaration order; non-null
        // properties slot in regardless of which "format" the island uses.
        //
        // Vanilla Taludas fixed extralarge:
        //   Position, MapFilePath, Rotation90, FertilityGuids, RandomizeFertilities, Difficulty, Config
        // Vanilla DLC1 fixed (continental + campaign):
        //   Position, Locked, MapFilePath, Rotation90, IslandLabel, FertilityGuids,
        //   FertilitiesPerAreaIndex, RandomizeFertilities, MineSlotActivation,
        //   RandomIslandConfig, FertilitySetGUIDs, IslandSize
        // Vanilla random:
        //   Position, [Locked], Size, Difficulty, Config
        //
        // The ordering below produces all three layouts when the irrelevant slots stay null.
        public int[]? Position { get; set; }
        public bool? Locked { get; set; }
        public UnicodeString? MapFilePath { get; set; }
        public byte? Rotation90 { get; set; }
        public UTF8String? IslandLabel { get; set; }
        public int[]? FertilityGuids { get; set; }
        public FertilitiesPerAreaIndex? FertilitiesPerAreaIndex { get; set; }
        public bool? RandomizeFertilities { get; set; }
        public MineSlotActivation? MineSlotActivation { get; set; }
        public List<Tuple<long, int>>? MineSlotMapping { get; set; }
        public RandomIslandConfig? RandomIslandConfig { get; set; }
        public FertilitySetGUIDs? FertilitySetGUIDs { get; set; }
        public IslandSizeRef? IslandSize { get; set; }
        // Trailing block — only used by Anno 1800 / Taludas-style fixed islands and by random
        // islands. DLC1 fixed islands embed Type+Difficulty inside RandomIslandConfig.value,
        // so these stay null on that path.
        public short? Size { get; set; }
        public Difficulty? Difficulty { get; set; }
        public Config? Config { get; set; }
    }

    public class Config
    {
        public IslandType? Type { get; set; }
        public Difficulty? Difficulty { get; set; }
        // Tag wrapper holding pairs of (areaIndex: short, config: IslandTypeRef). Default
        // list shape — six <None> children on continental_01, empty on other fixed islands.
        // Same pattern as MapTemplate.RandomlyPlacedThirdParties (no [FlatArray]).
        public List<Tuple<short, IslandTypeRef>>? TypePerConstructionArea { get; set; }
    }

    /// <summary>
    /// Wrapper for the <c>&lt;value&gt;&lt;id&gt;X&lt;/id&gt;&lt;/value&gt;</c> idiom used by
    /// the second item of each TypePerConstructionArea pair. Distinct from the bare
    /// <see cref="IslandType"/> which has only a flat <c>id</c>.
    /// </summary>
    public class IslandTypeRef
    {
        public IslandTypeRefValue? value { get; set; }
    }
    public class IslandTypeRefValue
    {
        public short? id { get; set; }
    }

    /// <summary>
    /// Empty container observed in vanilla DLC1 templates as <c>&lt;FertilitiesPerAreaIndex /&gt;</c>.
    /// We don't interpret its content yet; presence/absence is what matters for round-trip parity.
    /// </summary>
    public class FertilitiesPerAreaIndex { }
    public class MineSlotActivation { }
    public class FertilitySetGUIDs { }


    /// <summary>
    /// Wrapper for the <c>&lt;IslandSize&gt;&lt;value&gt;&lt;id&gt;0600&lt;/id&gt;&lt;/value&gt;&lt;/IslandSize&gt;</c>
    /// sub-tag emitted on the unique continental_01 fixed asset of every DLC1 expanded template.
    /// Distinct from the <see cref="Element.Size"/> attribute used by random islands.
    /// </summary>
    public class IslandSizeRef
    {
        public IslandSizeValue? value { get; set; }
    }
    public class IslandSizeValue
    {
        public short? id { get; set; }
    }

    public class IslandType
    {
        public short? id { get; set; }
    }

    public class Difficulty
    {
        public short? id { get; set; }
    }

    public class RandomIslandConfig
    {
        public Config? value { get; set; }
    }

    public class RandomlyPlacedThirdParty
    {
        public RandomlyPlacedThirdPartyValue? value { get; set; }
    }

    public class RandomlyPlacedThirdPartyValue
    {
        public short? id { get; set; }
    }
}
