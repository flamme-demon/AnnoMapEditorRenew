using AnnoMapEditor.Utilities;
using System.Collections.Generic;
using Avalonia.Media.Imaging;

namespace AnnoMapEditor.DataArchives.Assets.Models
{
    public class FixedIslandAsset : ObservableBase
    {
        public string FilePath { get; init; }

        public int SizeInTiles { get; init; }

        // [x1, y1, x2, y2] of the inhabitable / "active" region inside the terrain map.
        // The terrain map is a SizeInTiles × SizeInTiles square, but only this rect is the
        // actual island; everything else is the surrounding ocean buffer the engine draws
        // out of the playable area. The editor uses this to render fixed islands at their
        // real visual size instead of the inflated terrain bbox. Null when the asset
        // didn't ship with the field (older Anno 1800 .a7m files).
        public int[]? ActiveMapRect { get; init; }

        public Bitmap? Thumbnail { get; init; }

        public IReadOnlyDictionary<long, Slot> Slots { get; init; }
    }
}
