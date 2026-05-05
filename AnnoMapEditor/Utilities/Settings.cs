using System.IO;
using System.Runtime.Versioning;

namespace AnnoMapEditor.Utilities
{
    public class Settings : ObservableBase
    {
        public static Settings Instance { get; } = new();

        public bool Quickstart
        {
            get => UserSettings.Default.Quickstart;
            set
            {
                if (value != Quickstart)
                {
                    UserSettings.Default.Quickstart = value;
                    UserSettings.Default.Save();
                    OnPropertyChanged(nameof(Quickstart));
                }
            }
        }

        public string? GamePath
        {
            get => UserSettings.Default.GamePath;
            set
            {
                if (value != GamePath)
                {
                    UserSettings.Default.GamePath = value;
                    UserSettings.Default.Save();

                    if (value != null)
                    {
                        if (DataPath == null || !EnableExpertMode)
                            DataPath = Path.Combine(value, "maindata");

                        if (ModsPath == null || !EnableExpertMode)
                            ModsPath = Path.Combine(value, "mods");
                    }

                    OnPropertyChanged(nameof(GamePath));
                }
            }
        }

        public string? DataPath
        {
            get => UserSettings.Default.DataPath;
            set
            {
                if (value != DataPath)
                {
                    UserSettings.Default.DataPath = value;
                    UserSettings.Default.Save();
                    OnPropertyChanged(nameof(DataPath));
                }
            }
        }

        public string? ModsPath
        {
            get => UserSettings.Default.ModsPath;
            set
            {
                if (value != ModsPath)
                {
                    UserSettings.Default.ModsPath = value;
                    UserSettings.Default.Save();
                    OnPropertyChanged(nameof(ModsPath));
                }
            }
        }

        public bool EnableExpertMode
        {
            get => UserSettings.Default.EnableExpertMode;
            set
            {
                if (value != EnableExpertMode)
                {
                    UserSettings.Default.EnableExpertMode = value;
                    UserSettings.Default.Save();
                    OnPropertyChanged(nameof(EnableExpertMode));
                }
            }
        }

        /// <summary>
        /// CSV-encoded set of DLC filter ids the user has explicitly toggled OFF in the map list.
        /// Stored as a single string for compatibility with the legacy settings backing store.
        /// </summary>
        public System.Collections.Generic.HashSet<string> DisabledDlcFilters
        {
            get
            {
                string raw = UserSettings.Default.DisabledDlcFilters ?? string.Empty;
                return new System.Collections.Generic.HashSet<string>(
                    raw.Split(',', System.StringSplitOptions.RemoveEmptyEntries),
                    System.StringComparer.OrdinalIgnoreCase);
            }
            set
            {
                string serialized = string.Join(",", value);
                if (serialized != (UserSettings.Default.DisabledDlcFilters ?? ""))
                {
                    UserSettings.Default.DisabledDlcFilters = serialized;
                    UserSettings.Default.Save();
                    OnPropertyChanged(nameof(DisabledDlcFilters));
                }
            }
        }


        private Settings()
        {
            if (GamePath == null)
                GamePath = AutoDetectGamePath();
        }


        /// <summary>
        /// Tente de localiser le dossier d'install d'Anno 117 — Pax Romana.
        /// Ordre des candidats :
        ///   1. Registre Ubisoft Connect (Windows)
        ///   2. Chemins Steam Proton classiques (Linux)
        ///   3. Chemins Ubisoft Connect par défaut (Windows)
        ///   4. Chemins Steam classiques (Linux natif)
        /// Retourne le premier chemin existant qui contient le sous-dossier `maindata`.
        /// </summary>
        public static string? AutoDetectGamePath()
        {
            foreach (string candidate in EnumerateGamePathCandidates())
            {
                if (string.IsNullOrEmpty(candidate)) continue;
                if (Directory.Exists(Path.Combine(candidate, "maindata")))
                    return NormalizeSeparators(candidate);
            }
            return null;
        }

        private static System.Collections.Generic.IEnumerable<string> EnumerateGamePathCandidates()
        {
            // 1. Windows : registre Ubisoft Connect (clés Anno 117 puis fallback Anno 1800
            //    pour les utilisateurs qui ont les deux jeux et veulent juste l'editor)
            if (System.OperatingSystem.IsWindows())
            {
                string? registryPath = ReadUbisoftRegistry(@"SOFTWARE\WOW6432Node\Ubisoft\Anno 117 - Pax Romana");
                if (registryPath != null) yield return registryPath;
            }

            // 2. Linux : Steam + Proton (Anno 117 AppID = 2980876963)
            string? home = System.Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
            {
                yield return Path.Combine(home,
                    ".local/share/Steam/steamapps/compatdata/2980876963/pfx/drive_c/Program Files (x86)/Ubisoft/Ubisoft Game Launcher/games/Anno 117 - Pax Romana");
                yield return Path.Combine(home,
                    ".steam/steam/steamapps/compatdata/2980876963/pfx/drive_c/Program Files (x86)/Ubisoft/Ubisoft Game Launcher/games/Anno 117 - Pax Romana");
            }

            // 3. Windows : chemins par défaut Ubisoft Connect
            if (System.OperatingSystem.IsWindows())
            {
                yield return @"C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games\Anno 117 - Pax Romana";
                yield return @"C:\Program Files\Ubisoft\Ubisoft Game Launcher\games\Anno 117 - Pax Romana";
            }
        }

        [SupportedOSPlatform("windows")]
        private static string? ReadUbisoftRegistry(string installDirKey)
        {
            using Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(installDirKey);
            return key?.GetValue("InstallDir") as string;
        }

        private static string NormalizeSeparators(string path)
        {
            if (path.Contains(Path.DirectorySeparatorChar))
                return path;
            char wrongSeparator = Path.DirectorySeparatorChar == '/' ? '\\' : '/';
            return path.Replace(wrongSeparator, Path.DirectorySeparatorChar);
        }
    }
}
