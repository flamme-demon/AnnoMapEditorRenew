using AnnoMapEditor.Utilities;
using Pfim;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using IImage = Avalonia.Media.IImage;
using PfimImage = Pfim.IImage;

namespace AnnoMapEditor.DataArchives
{
    public abstract class DataArchive : ObservableBase, IDataArchive
    {
        public abstract Stream? OpenRead(string path);

        public abstract IEnumerable<string> Find(string pattern);

        private static string? AdjustDataPath(string? path)
        {
            if (path is null)
                return null;
            if (File.Exists(Path.Combine(path, "maindata/data0.rda")))
                return path;
            if (File.Exists(Path.Combine(path, "data0.rda")))
                return Path.GetDirectoryName(path);
            if (Directory.Exists(Path.Combine(path, "data/dlc01")))
                return path;
            if (Directory.Exists(Path.Combine(path, "dlc01")))
                return Path.GetDirectoryName(path);
            return null;
        }

        public IImage? TryLoadIcon(string iconPath, PixelSize? desiredSize = null)
        {
            // Icons are referenced as .png but stored as .dds.
            if (iconPath.EndsWith(".png"))
                iconPath = iconPath[0..^4] + "_0.dds";

            if (iconPath.Contains("/fhd/"))
                iconPath = iconPath.Replace("/fhd/", "/4k/");

            using Stream? stream = OpenRead(iconPath);
            if (stream == null)
                return null;

            using PfimImage iconImage = Pfimage.FromStream(stream);
            return desiredSize is { } size
                ? ConvertToAvaloniaBitmapMipmapped(iconImage, size)
                : ConvertToAvaloniaBitmap(iconImage);
        }

        public Bitmap? TryLoadPng(string pngPath)
        {
            using Stream? stream = OpenRead(pngPath);
            if (stream == null)
                return null;

            return new Bitmap(stream);
        }

        private static WriteableBitmap ConvertToAvaloniaBitmap(PfimImage image)
            => CreateBitmap(image.Data, image.DataLen, image.Width, image.Height, image.Stride, image.Format);

        private static WriteableBitmap ConvertToAvaloniaBitmapMipmapped(PfimImage image, PixelSize desiredSize)
        {
            var matchingMips = image.MipMaps
                .Where(x => x.Height >= desiredSize.Height && x.Width >= desiredSize.Width)
                .ToList();

            if (matchingMips.Count == 0)
                return ConvertToAvaloniaBitmap(image);

            MipMapOffset m = matchingMips[^1];
            var data = new byte[m.DataLen];
            Buffer.BlockCopy(image.Data, m.DataOffset, data, 0, m.DataLen);
            return CreateBitmap(data, m.DataLen, m.Width, m.Height, m.Stride, image.Format);
        }

        private static WriteableBitmap CreateBitmap(byte[] data, int dataLen, int width, int height, int stride, ImageFormat sourceFormat)
        {
            (PixelFormat avaloniaFormat, AlphaFormat alpha) = GetAvaloniaPixelFormat(sourceFormat);

            var bitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                avaloniaFormat,
                alpha);

            using ILockedFramebuffer fb = bitmap.Lock();
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                IntPtr src = handle.AddrOfPinnedObject();
                if (stride == fb.RowBytes)
                {
                    Marshal.Copy(data, 0, fb.Address, dataLen);
                }
                else
                {
                    int rowBytesToCopy = Math.Min(stride, fb.RowBytes);
                    for (int y = 0; y < height; y++)
                    {
                        IntPtr rowSrc = src + y * stride;
                        IntPtr rowDst = fb.Address + y * fb.RowBytes;
                        unsafe
                        {
                            Buffer.MemoryCopy((void*)rowSrc, (void*)rowDst, fb.RowBytes, rowBytesToCopy);
                        }
                    }
                }
            }
            finally
            {
                handle.Free();
            }

            return bitmap;
        }

        private static (PixelFormat, AlphaFormat) GetAvaloniaPixelFormat(ImageFormat format)
        {
            return format switch
            {
                ImageFormat.Rgb24 => (PixelFormat.Bgra8888, AlphaFormat.Opaque),
                ImageFormat.Rgba32 => (PixelFormat.Bgra8888, AlphaFormat.Unpremul),
                ImageFormat.Rgb8 => (PixelFormat.Bgra8888, AlphaFormat.Opaque),
                _ => throw new Exception($"Unable to convert {format} to Avalonia PixelFormat")
            };
        }
    }
}
