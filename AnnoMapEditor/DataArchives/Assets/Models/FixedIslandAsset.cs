using AnnoMapEditor.Utilities;
using System.Collections.Generic;
using Avalonia.Media.Imaging;

namespace AnnoMapEditor.DataArchives.Assets.Models
{
    public class FixedIslandAsset : ObservableBase
    {
        public string FilePath { get; init; }

        public int SizeInTiles { get; init; }

        public Bitmap? Thumbnail { get; init; }

        public IReadOnlyDictionary<long, Slot> Slots { get; init; }
    }
}
