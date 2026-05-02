using System.Collections.Generic;
using System.Text.RegularExpressions;
using AnnoMapEditor.DataArchives.Assets.Models;
using AnnoMapEditor.MapTemplates.Models;
using AnnoMapEditor.Utilities;

namespace AnnoMapEditor.UI.Avalonia.ViewModels
{
    public class MapListItem
    {
        public MapTemplateAsset? Asset { get; }
        public string DisplayName { get; }
        public string SubLabel { get; }
        public string TemplatePath { get; }
        public bool IsExpanded { get; }
        public string DlcId { get; }

        // For mod items: absolute path on disk (read directly with FromBinaryFileAsync)
        // For vanilla items: null (read via DataArchive)
        public string? AbsoluteFilePath { get; }
        public string? ModFolderName { get; }
        public bool IsMod => AbsoluteFilePath != null;

        private MapListItem(MapTemplateAsset? asset, string displayName, string subLabel,
                            string templatePath, bool isExpanded, string dlcId,
                            string? absoluteFilePath = null, string? modFolderName = null)
        {
            Asset = asset;
            DisplayName = displayName;
            SubLabel = subLabel;
            TemplatePath = templatePath;
            IsExpanded = isExpanded;
            DlcId = dlcId;
            AbsoluteFilePath = absoluteFilePath;
            ModFolderName = modFolderName;
        }

        public static MapListItem Mod(string absolutePath, string modFolderName, string templatePath)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(absolutePath);
            return new MapListItem(
                null,
                $"⚙ {fileName}",
                $"Mod · {modFolderName}",
                templatePath,
                false,
                "Mod",
                absoluteFilePath: absolutePath,
                modFolderName: modFolderName);
        }

        public static IEnumerable<MapListItem> AllVariants(MapTemplateAsset asset)
        {
            string region = asset.TemplateRegion?.Name ?? "?";
            string type = asset.TemplateMapType?.Name ?? "?";
            string display = asset.Name ?? "(unnamed)";

            string baseDlc = DetectDlcFromPath(asset.TemplateFilename);
            string baseSub = baseDlc == "Base"
                ? $"{region} · {type}"
                : $"{region} · {type} · {baseDlc}";

            yield return new MapListItem(asset, display, baseSub, asset.TemplateFilename, false, baseDlc);

            if (!string.IsNullOrWhiteSpace(asset.EnlargedTemplateFilename))
            {
                string expDlc = DetectDlcFromPath(asset.EnlargedTemplateFilename!);
                yield return new MapListItem(
                    asset,
                    $"{display}  ★ {expDlc}",
                    $"{region} · {type} · enlarged {expDlc}",
                    asset.EnlargedTemplateFilename!,
                    true,
                    expDlc);
            }
        }

        public static IEnumerable<MapListItem> ScanModsFolder(string? modsRoot)
        {
            if (string.IsNullOrEmpty(modsRoot) || !System.IO.Directory.Exists(modsRoot))
                yield break;

            // Scan every mod that contains at least one .a7tinfo (not just AME_*).
            // Folder name "[Map] XL Maptemplate (Taludas)" is a valid mod and we want to
            // be able to re-edit it.
            foreach (string modDir in System.IO.Directory.EnumerateDirectories(modsRoot))
            {
                string folderName = System.IO.Path.GetFileName(modDir);
                if (folderName.StartsWith(".")) continue; // skip hidden like .ubi

                string dataDir = System.IO.Path.Combine(modDir, "data");
                if (!System.IO.Directory.Exists(dataDir)) continue;

                foreach (string a7tinfo in System.IO.Directory.EnumerateFiles(dataDir, "*.a7tinfo",
                    System.IO.SearchOption.AllDirectories))
                {
                    string rel = System.IO.Path.GetRelativePath(modDir, a7tinfo).Replace('\\', '/');
                    yield return Mod(a7tinfo, folderName, rel);
                }
            }
        }

        // data/base/...           -> "Base"
        // data/dlc01/...          -> "DLC01"
        // data/dlc02/...          -> "DLC02"
        // anywhere "_dlc01..."    -> "DLC01" (fallback)
        private static readonly Regex DlcRegex = new(
            @"(?:^|[/\\])(?:data[/\\])?(dlc\d+)(?:[/\\_]|$)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string DetectDlcFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "Base";
            Match m = DlcRegex.Match(path);
            if (m.Success)
                return m.Groups[1].Value.ToUpperInvariant();
            return "Base";
        }
    }

    /// <summary>
    /// Either a single map (leaf) or a mod group containing multiple maps. Used by the
    /// left-pane TreeView so a mod with 6 difficulty variants doesn't pollute the list
    /// with 6 sibling rows.
    /// </summary>
    public class MapListEntry
    {
        public string Header { get; set; }
        public string SubHeader { get; set; }
        public MapListItem? Item { get; }
        public System.Collections.ObjectModel.ObservableCollection<MapListEntry> Children { get; } = new();
        public bool IsGroup => Item is null;

        public MapListEntry(MapListItem item)
        {
            Item = item;
            Header = item.DisplayName;
            SubHeader = item.SubLabel;
        }

        public MapListEntry(string header, string subHeader)
        {
            Item = null;
            Header = header;
            SubHeader = subHeader;
        }
    }

    public class DlcFilter : ObservableBase
    {
        public string Id { get; }
        public string Label { get; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }
        private bool _isEnabled = true;

        public DlcFilter(string id)
        {
            Id = id;
            Label = id == "Base" ? "Base" : id;
        }
    }

    public class PropertyItem
    {
        public string Key { get; }
        public string Value { get; }
        public PropertyItem(string key, string value) { Key = key; Value = value; }
    }

    public class MapElementItem
    {
        public string Kind { get; }
        public string Position { get; }
        public string Description { get; }

        private MapElementItem(string kind, string position, string description)
        {
            Kind = kind;
            Position = position;
            Description = description;
        }

        public static MapElementItem From(MapElement element)
        {
            string kind = element.GetType().Name;
            string position = $"{element.Position.X},{element.Position.Y}";
            string description = element switch
            {
                FixedIslandElement fixedIsland => fixedIsland.IslandAsset?.DisplayName ?? fixedIsland.IslandAsset?.FilePath ?? "(fixed island)",
                RandomIslandElement random =>
                    $"random {random.IslandType?.ToString() ?? "?"} {random.IslandSize?.ToString() ?? "?"}",
                StartingSpotElement spot => $"starting spot #{spot.Index}",
                _ => element.GetType().Name
            };
            return new MapElementItem(kind, position, description);
        }

        public static MapElementItem Diag(string kind, string position, string description)
            => new(kind, position, description);
    }
}
