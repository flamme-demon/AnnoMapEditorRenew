using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace AnnoMapEditor.DataArchives
{
    public interface IDataArchive
    {
        Stream? OpenRead(string filePath);

        IEnumerable<string> Find(string pattern);

        Bitmap? TryLoadPng(string pngPath);

        IImage? TryLoadIcon(string iconPath, PixelSize? desiredSize = null);
    }
}
