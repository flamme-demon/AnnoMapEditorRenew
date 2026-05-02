using AnnoMapEditor.MapTemplates.Enums;
using System.Collections.Generic;
using Avalonia.Media.Imaging;

namespace AnnoMapEditor.DataArchives.Assets.Models
{
    /// <summary>
    /// Common base class for RandomIslandAsset and FixedIslandAsset. If an island does not belong
    /// to a pool and does not require non-default properties, it may be ommitted from assets.xml.
    /// </summary>
    public class IslandAsset
    {
        public string DisplayName { get; init; }

        public string FilePath { get; init; }

        public Bitmap? Thumbnail { get; init; }

        public RegionAsset Region { get; init; }

        public IEnumerable<IslandDifficulty> IslandDifficulty { get; init; }

        public IEnumerable<IslandSize> IslandSize { get; init; }

        public IEnumerable<IslandType> IslandType { get; init; }

        public int SizeInTiles { get; init; }

        /// <summary>
        /// [x1, y1, x2, y2] of the actual habitable terrain inside the SizeInTiles square,
        /// pulled from the .a7m's <c>ActiveMapRect</c>. Used by the canvas to render the
        /// island at the size you'd see on the in-game minimap (the surrounding buffer is
        /// just out-of-bounds ocean). Null on assets that don't ship the field.
        /// </summary>
        public int[]? ActiveMapRect { get; init; }

        public IReadOnlyDictionary<long, Slot> Slots { get; init; }
    }
}
