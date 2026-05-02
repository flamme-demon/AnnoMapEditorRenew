using AnnoMapEditor.Utilities;
using System.Collections.Generic;
using System.Linq;

namespace AnnoMapEditor.MapTemplates.Enums
{
    public class IslandSize
    {
        private static readonly Logger<IslandSize> _logger = new();

        // ElementValue = the byte value Anno 117 stores in <Size>0X00</Size> for random islands,
        // observed across every vanilla pool template (base + dlc01_expanded). Empirical mapping:
        //   0 = Small      1 = Medium     2 = Large      3 = ExtraLarge   6 = Continental
        // Note: 0300 in vanilla is only ever used by the 4 starter spots of each map (random
        // Large with Type.id = 0100). Continental never appears as <Size> — it's emitted via
        // a separate <IslandSize><value><id>0600</id></value></IslandSize> sub-tag on a fixed
        // island (and only on the unique continental_01 asset of DLC1 expanded). We still keep
        // Continental.ElementValue = 6 so the legacy label-bucket and FromElementValue lookups
        // stay consistent.
        public static readonly IslandSize Default     = new("Small",       null, 192);
        public static readonly IslandSize Small       = new("Small",       0,    192);
        public static readonly IslandSize Medium      = new("Medium",      1,    320);
        public static readonly IslandSize Large       = new("Large",       2,    384);
        public static readonly IslandSize ExtraLarge  = new("ExtraLarge",  3,    400);
        public static readonly IslandSize Continental = new("Continental", 6,    int.MaxValue);

        // ExtraLarge MUST come before Continental so the bucket selector in IslandRepository
        // (first size where SizeInTiles ≤ DefaultSizeInTiles) classifies a 400-tile extralarge_*
        // asset as ExtraLarge rather than falling through to Continental (int.MaxValue).
        public static readonly IEnumerable<IslandSize> All = new[] { Small, Medium, Large, ExtraLarge, Continental };


        public string Name { get; init; }

        public short? ElementValue { get; init; }

        public int DefaultSizeInTiles { get; init; }


        private IslandSize(string name, short? elementValue, int defaultSizeInTiles)
        {
            Name = name;
            ElementValue = elementValue;
            DefaultSizeInTiles = defaultSizeInTiles;
        }


        public static IslandSize FromElementValue(short? elementValue)
        {
            IslandSize? size = All.FirstOrDefault(t => t.ElementValue == elementValue);

            if (size is null)
            {
                _logger.LogWarning($"{elementValue} is not a valid element value for {nameof(IslandSize)}. Defaulting to {nameof(Default)}/{Default.ElementValue}.");
                size = Default;
            }

            return size;
        }

        /// <summary>
        /// Reads the asset file name (e.g. "roman_island_extralarge_03") and maps it to the
        /// matching <see cref="IslandSize"/>. Returns null when the name doesn't carry a
        /// recognizable size token, so callers can fall back to a tile-count heuristic.
        ///
        /// Order matters: "extralarge" must be checked before "large", and "continental"
        /// is more specific than the generic suffixes.
        /// </summary>
        public static IslandSize? FromAssetFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            if (fileName.Contains("continental", System.StringComparison.OrdinalIgnoreCase)) return Continental;
            if (fileName.Contains("extralarge",  System.StringComparison.OrdinalIgnoreCase)) return ExtraLarge;
            if (fileName.Contains("_large_",     System.StringComparison.OrdinalIgnoreCase)) return Large;
            if (fileName.Contains("_medium_",    System.StringComparison.OrdinalIgnoreCase)) return Medium;
            if (fileName.Contains("_small_",     System.StringComparison.OrdinalIgnoreCase)) return Small;
            return null;
        }


        public override string ToString() => Name;
    }
}
