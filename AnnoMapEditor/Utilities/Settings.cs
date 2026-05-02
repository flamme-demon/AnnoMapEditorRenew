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
                GamePath = GetInstallDirFromRegistry();
        }


        public static string? GetInstallDirFromRegistry()
        {
            if (!System.OperatingSystem.IsWindows())
                return null;

            return ReadAnnoInstallDirWindows();
        }

        [SupportedOSPlatform("windows")]
        private static string? ReadAnnoInstallDirWindows()
        {
            string installDirKey = @"SOFTWARE\WOW6432Node\Ubisoft\Anno 1800";
            using Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(installDirKey);

            string? installDir = key?.GetValue("InstallDir") as string;
            if (installDir == null)
                return null;

            if (!installDir.Contains(Path.DirectorySeparatorChar))
            {
                char wrongSeparator = Path.DirectorySeparatorChar == '/' ? '\\' : '/';
                return installDir.Replace(wrongSeparator, Path.DirectorySeparatorChar);
            }
            return installDir;
        }
    }
}
