using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AnnoMapEditor
{
    public sealed class UserSettings
    {
        public static UserSettings Default { get; } = Load();

        public string? GamePath { get; set; }
        public string? DataPath { get; set; }
        public string? ModsPath { get; set; }
        public bool EnableExpertMode { get; set; }
        public string? AssetsHash { get; set; }
        public string? Xpath { get; set; }
        public bool Quickstart { get; set; }

        /// <summary>CSV of DLC filter ids the user has switched OFF in the maps panel.</summary>
        public string? DisabledDlcFilters { get; set; }

        /// <summary>Global MapView canvas rotation in degrees — applied to every map the user
        /// opens so the editor stays aligned with the in-game view. Default -45° = the in-game
        /// isometric diamond orientation (north pointing up). 0° = flat top-down view.</summary>
        public int MapViewRotationDeg { get; set; } = -45;

        /// <summary>"Light" (parchment) or "Dark" (navy) — picked by the toggle
        /// FAB in the bottom bar, persisted across sessions.</summary>
        public string ThemeVariant { get; set; } = "Light";

        // Window state persistence
        public double? MainWindowWidth { get; set; }
        public double? MainWindowHeight { get; set; }
        public int? MainWindowX { get; set; }
        public int? MainWindowY { get; set; }
        public bool MainWindowMaximized { get; set; }

        public double? StartWindowWidth { get; set; }
        public double? StartWindowHeight { get; set; }
        public int? StartWindowX { get; set; }
        public int? StartWindowY { get; set; }

        [JsonIgnore]
        public static string ConfigPath
        {
            get
            {
                string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (string.IsNullOrEmpty(baseDir))
                    baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                return Path.Combine(baseDir, "AnnoMapEditor", "settings.json");
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, SerializerOptions));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"UserSettings.Save failed: {ex.Message}");
            }
        }

        private static UserSettings Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    UserSettings? loaded = JsonSerializer.Deserialize<UserSettings>(json, SerializerOptions);
                    if (loaded is not null)
                        return loaded;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"UserSettings.Load failed: {ex.Message}");
            }
            return new UserSettings();
        }

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }
}
