using AnnoMapEditor.Utilities;
using System.Collections.Generic;
using System.Linq;

namespace AnnoMapEditor.MapTemplates.Enums
{
    public class IslandType
    {
        private static readonly Logger<IslandType> _logger = new();

        public static readonly IslandType Normal         = new("Normal",         null);
        public static readonly IslandType Starter        = new("Starter",        1);
        public static readonly IslandType Decoration     = new("Decoration",     2);
        public static readonly IslandType ThirdParty     = new("ThirdParty",     3);
        public static readonly IslandType PirateIsland   = new("PirateIsland",   4);
        public static readonly IslandType Cliff          = new("Cliff",          5);
        // Anno 117: volcanic islands (DLC1 "Pillars of Fire" / Vesuvius)
        public static readonly IslandType VolcanicIsland = new("VolcanicIsland", 6);

        public static readonly IEnumerable<IslandType> All = new[]
        {
            Normal, Starter, Decoration, ThirdParty, PirateIsland, Cliff, VolcanicIsland
        };

        public readonly string Name;

        public readonly short? ElementValue;

        public bool IsNormalOrStarter => this == Starter || this == Normal;

        public bool IsSameWithoutOil(IslandType that) => ElementValue == that.ElementValue;


        private IslandType(string name, short? elementValue)
        {
            ElementValue = elementValue;
            Name = name;
        }


        public static IslandType FromName(string name)
        {
            IslandType? type = All.FirstOrDefault(d => d.Name == name);

            if (type is null)
            {
                _logger.LogWarning($"{name} is not a valid name for {nameof(IslandType)}. Defaulting to {nameof(Normal)}.");
                type = Normal;
            }

            return type;
        }

        public static IslandType FromElementValue(short? elementValue)
        {
            IslandType? type = All.FirstOrDefault(t => t.ElementValue == elementValue);

            if (type is null)
            {
                _logger.LogWarning($"{elementValue} is not a valid element value for {nameof(IslandType)}. Defaulting to {nameof(Normal)}/{Normal.ElementValue}.");
                type = Normal;
            }

            return type;
        }
        // Anno 117 conventions de nommage (à compléter au fil des découvertes
        // sur les .a7m vanilla DLC1 et les mods Taludas-style) :
        //   - décoratives  → fragments  "_d_", "_dst_", "_battlesite_", "_encounter_"
        //   - volcaniques  → "volcanic" / "vesuv" / l'asset Vesuvius DLC1
        //                    "roman_dlc01_island_continental_01"
        //   - 3rd party / pirates : conventions à identifier (pas de mapping
        //                            hardcodé pour l'instant — anciens noms 1800
        //                            "moderate_3rdparty*" / "colony01_3rdparty*" virés).
        public static IslandType FromIslandFileName(string fileName)
        {
            if (fileName == "roman_dlc01_island_continental_01")
                return VolcanicIsland;

            if (fileName.Contains("_d_") || fileName.Contains("_dst_")
                || fileName.Contains("_battlesite_") || fileName.Contains("_encounter_"))
                return Decoration;

            if (fileName.Contains("volcanic", System.StringComparison.OrdinalIgnoreCase)
                || fileName.Contains("vesuv", System.StringComparison.OrdinalIgnoreCase))
                return VolcanicIsland;

            return Normal;
        }

        // Plus de labels hardcodés (les noms NPCs 1800 ont été retirés).
        // Le label vient de l'asset XML du jeu lui-même.
        public static string? DefaultIslandLabelFromFileName(string fileName) => null;


        public override string ToString() => Name;
    }
}
