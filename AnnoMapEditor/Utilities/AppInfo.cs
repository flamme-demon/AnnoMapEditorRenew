using System.Reflection;

namespace AnnoMapEditor.Utilities
{
    /// <summary>
    /// Read-only metadata about this build — exposed for the UI footer / title bar so users
    /// reporting bugs or filing PRs can quote the exact version they ran.
    /// </summary>
    public static class AppInfo
    {
        /// <summary>Semantic version with optional pre-release tag (e.g. "0.7.0-fork.1").</summary>
        public static string Version { get; } = ResolveVersion();

        /// <summary>Short label suitable for a status bar: "v0.7.0-fork.1".</summary>
        public static string ShortVersionLabel => $"v{Version}";

        private static string ResolveVersion()
        {
            // InformationalVersion preserves pre-release suffixes; AssemblyVersion strips them.
            // Strip the optional "+<commit-sha>" build metadata SemVer allows — only the
            // user-facing version goes in the UI.
            var asm = Assembly.GetExecutingAssembly();
            string? info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                int plus = info.IndexOf('+');
                return plus >= 0 ? info.Substring(0, plus) : info;
            }
            return asm.GetName().Version?.ToString() ?? "0.0.0";
        }
    }
}
