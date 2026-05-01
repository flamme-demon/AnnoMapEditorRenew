using System;
using Avalonia;
using Avalonia.Controls;

namespace AnnoMapEditor.UI.Avalonia
{
    public enum WindowKind { Main, Start }

    public static class WindowStateService
    {
        public static void Attach(Window window, WindowKind kind)
        {
            UserSettings s = UserSettings.Default;

            (double? w, double? h, int? x, int? y) = kind switch
            {
                WindowKind.Main => (s.MainWindowWidth, s.MainWindowHeight, s.MainWindowX, s.MainWindowY),
                _              => (s.StartWindowWidth, s.StartWindowHeight, s.StartWindowX, s.StartWindowY)
            };

            if (w is double ww and > 200 && h is double hh and > 200)
            {
                window.Width = ww;
                window.Height = hh;
            }

            if (x is int xx && y is int yy)
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Position = new PixelPoint(xx, yy);
            }

            if (kind == WindowKind.Main && s.MainWindowMaximized)
                window.WindowState = WindowState.Maximized;

            window.Closing += (_, _) => Persist(window, kind);
        }

        private static void Persist(Window window, WindowKind kind)
        {
            try
            {
                UserSettings s = UserSettings.Default;
                bool isNormal = window.WindowState == WindowState.Normal;

                if (kind == WindowKind.Main)
                {
                    s.MainWindowMaximized = window.WindowState == WindowState.Maximized;
                    if (isNormal)
                    {
                        s.MainWindowWidth = window.Width;
                        s.MainWindowHeight = window.Height;
                        s.MainWindowX = window.Position.X;
                        s.MainWindowY = window.Position.Y;
                    }
                }
                else
                {
                    if (isNormal)
                    {
                        s.StartWindowWidth = window.Width;
                        s.StartWindowHeight = window.Height;
                        s.StartWindowX = window.Position.X;
                        s.StartWindowY = window.Position.Y;
                    }
                }
                s.Save();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"WindowStateService.Persist failed: {ex.Message}");
            }
        }
    }
}
