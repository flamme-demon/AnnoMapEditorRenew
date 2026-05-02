using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Anno.FileDBModels.Anno1800.Gamedata.Models.Shared;
using AnnoMapEditor.DataArchives;
using AnnoMapEditor.DataArchives.Assets.Models;
using AnnoMapEditor.MapTemplates.Models;
using AnnoMapEditor.MapTemplates.Serializing;
using AnnoMapEditor.Utilities;
using FileDBSerializing;
using FileDBSerializing.ObjectSerializer;
using Newtonsoft.Json;
using RDAExplorer;

namespace AnnoMapEditor.Mods.Serialization
{
    /// <summary>
    /// Generates a "new map template" mod for Anno 117 — Taludas style.
    /// Instead of overriding a vanilla template, we add a brand-new MapTemplateAsset
    /// (with a custom GUID and path) that the player can pick from the menu.
    ///
    /// Output layout:
    ///   modinfo.json
    ///   data/&lt;creator&gt;/provinces/&lt;region&gt;/templates/pool/&lt;name&gt;_easy/&lt;name&gt;_easy.a7t
    ///                                                              .a7te
    ///                                                              .a7tinfo
    ///   data/base/config/export/assets.xml
    ///   data/base/config/gui/texts_english.xml (+ 11 other languages)
    /// </summary>
    public class Anno117ModWriter
    {
        // Vanilla 2048×2048 templates ship as BBDom V2 (Taludas mods do too — proven safe).
        // Vanilla DLC1 expanded templates (2688×2688) ship as V3 — V2 would not be recognized
        // for those. We pick the version per-map below.
        private const FileDBDocumentVersion StandardA7tinfoVersion = FileDBDocumentVersion.Version2;
        private const FileDBDocumentVersion ExpandedA7tinfoVersion = FileDBDocumentVersion.Version3;

        private static FileDBDocumentVersion PickA7tinfoVersion(MapTemplate t) =>
            t.Size.X > 2048 ? ExpandedA7tinfoVersion : StandardA7tinfoVersion;

        /// <summary>
        /// Names that already exist as vanilla MapTemplateTypes — using one of these would make
        /// the new template disappear into the vanilla category. The UI should reject these names.
        /// </summary>
        public static readonly IReadOnlyCollection<string> ReservedTypeNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "Random", "Default", "Donut", "Rift", "Corners", "Chain", "Gamescom"
        };

        public static bool IsReservedName(string name)
            => !string.IsNullOrWhiteSpace(name) && ReservedTypeNames.Contains(name.Trim());
        private static readonly string[] Languages =
        {
            "english", "french", "german", "italian", "spanish",
            "polish", "russian", "japanese", "korean",
            "brazilian", "simplified_chinese", "traditional_chinese"
        };

        private readonly MapTemplateWriter _mapTemplateWriter = new();

        public async Task<string> WriteAsync(
            MapTemplate mapTemplate,
            MapTemplateAsset templateAsset,
            string sourceTemplateFilename,
            string modName,
            string modsRoot)
        {
            string safeName = SafeName(modName);
            string modFolder = Path.Combine(modsRoot, $"AME_{safeName}");
            // Don't delete: re-saving with the same name updates the existing mod in place
            // (same ModID, same custom GUID, same files overwritten). Anno will see it as the
            // same template — just refreshed content. No need to re-activate it in the mod manager.
            Directory.CreateDirectory(modFolder);
            bool isUpdate = Directory.Exists(modFolder) && File.Exists(Path.Combine(modFolder, "modinfo.json"));

            // Stable custom GUIDs derived from safeName. We allocate 4 slots per mod:
            //   +0 : MapTemplateType (the "category")
            //   +1 : MapTemplate Easy
            //   +2 : MapTemplate Medium
            //   +3 : MapTemplate Hard
            // Anno 117 only displays a MapTemplateType in the menu when several MapTemplates are
            // attached to it (one per difficulty). A single template would be silently ignored.
            int seedHash = Math.Abs(safeName.GetHashCode()) % 100_000;
            long mapTypeGuid       = 1_999_100_000L + seedHash * 10;
            long templateEasyGuid  = mapTypeGuid + 1;
            long templateMediumGuid = mapTypeGuid + 2;
            long templateHardGuid  = mapTypeGuid + 3;

            // TemplateRegionId is the short identifier ("Roman", "Celtic") used in assets.xml.
            // TemplateRegion.Name is the asset display name ("Region Roman") which has spaces and
            // breaks file paths.
            string region = !string.IsNullOrEmpty(templateAsset.TemplateRegionId)
                ? templateAsset.TemplateRegionId
                : "Roman";
            string regionLower = region.ToLowerInvariant();

            // We only write one variant for now (the one being edited).
            // Full coverage would generate Easy/Medium/Hard from the same edits.
            string templateBaseName = $"{regionLower}_province_{safeName}";
            string templatePathBase = $"data/ame/{safeName}/provinces/{regionLower}/templates/pool/{templateBaseName}";

            string a7tRel     = $"{templatePathBase}/{templateBaseName}.a7t";
            string a7teRel    = $"{templatePathBase}/{templateBaseName}.a7te";
            string a7tinfoRel = $"{templatePathBase}/{templateBaseName}.a7tinfo";

            // 1. modinfo.json (rich format Anno 117 expects) — only on first export
            if (!isUpdate)
                await WriteModInfoAsync(modFolder, modName, safeName, templateAsset);

            // 2. .a7tinfo — V2 for standard 2048 templates (Taludas-compatible), V3 for the
            // 2688 "expanded" DLC1 layout (engine refuses V2 there).
            string a7tinfoPath = Path.Combine(modFolder, a7tinfoRel.Replace('/', Path.DirectorySeparatorChar));
            await _mapTemplateWriter.WriteA7tinfoAsync(mapTemplate, a7tinfoPath, PickA7tinfoVersion(mapTemplate));

            // 3. .a7te (XML AnnoEditorLevel, FileVersion 4 for Anno 117)
            string a7tePath = Path.Combine(modFolder, a7teRel.Replace('/', Path.DirectorySeparatorChar));
            WriteA7te(mapTemplate.Size.X, a7tePath);

            // 4. .a7t (RDA v2.2 container with gamedata.data inside as FileDB V2)
            string a7tPath = Path.Combine(modFolder, a7tRel.Replace('/', Path.DirectorySeparatorChar));
            WriteA7t(mapTemplate, a7tPath);

            // 5. assets.xml — always rewrite (3 MapTemplates: Easy/Medium/Hard so Anno shows
            //    the category in the menu).
            await WriteAssetsXmlAsync(modFolder, modName, region, templateAsset, a7tRel,
                mapTypeGuid, templateEasyGuid, templateMediumGuid, templateHardGuid);

            // 6. Localized text — always rewrite. Includes the MapTemplateType label
            //    (the visible category name in the menu) AND each MapTemplate's name.
            await WriteLanguageTextsAsync(modFolder, modName, mapTypeGuid,
                templateEasyGuid, templateMediumGuid, templateHardGuid);

            return modFolder;
        }

        /// <summary>
        /// Updates the .a7t/.a7te/.a7tinfo of an existing mod in place. Keeps modinfo, assets.xml
        /// and texts untouched. Use when re-saving an already-exported mod after further edits.
        /// </summary>
        public async Task<string> UpdateExistingModAsync(MapTemplate mapTemplate, string a7tinfoAbsolutePath)
        {
            // Walk up from the .a7tinfo to find the mod root (where modinfo.json should live).
            string? modRoot = FindModRoot(a7tinfoAbsolutePath);
            if (modRoot is null || !File.Exists(Path.Combine(modRoot, "modinfo.json")))
            {
                throw new InvalidOperationException(
                    "This mod is incomplete (modinfo.json is missing). " +
                    "Reload the app and re-export from a vanilla map.");
            }

            string dir = Path.GetDirectoryName(a7tinfoAbsolutePath)!;
            string baseName = Path.GetFileNameWithoutExtension(a7tinfoAbsolutePath);
            string a7tinfo = Path.Combine(dir, baseName + ".a7tinfo");
            string a7te = Path.Combine(dir, baseName + ".a7te");
            string a7t = Path.Combine(dir, baseName + ".a7t");

            await _mapTemplateWriter.WriteA7tinfoAsync(mapTemplate, a7tinfo, PickA7tinfoVersion(mapTemplate));
            WriteA7te(mapTemplate.Size.X, a7te);
            WriteA7t(mapTemplate, a7t);
            return dir;
        }

        private static string? FindModRoot(string anyFileInsideMod)
        {
            DirectoryInfo? d = new FileInfo(anyFileInsideMod).Directory;
            while (d != null)
            {
                if (File.Exists(Path.Combine(d.FullName, "modinfo.json"))
                    || d.Name.StartsWith("AME_", StringComparison.OrdinalIgnoreCase))
                {
                    return d.FullName;
                }
                d = d.Parent;
            }
            return null;
        }

        // ---------------------- modinfo.json ----------------------

        private static async Task WriteModInfoAsync(string modFolder, string modName, string safeName,
                                                     MapTemplateAsset templateAsset)
        {
            var modinfo = new
            {
                Version = "1.0.0",
                Anno = 8,
                ModID = $"ame-{safeName.Replace('_', '-')}", // mod-loader requires lowercase letters + dashes only (no digits/underscores per current Anno 117 strict check)
                Difficulty = "unchanged",
                GameSetup = new
                {
                    RequiresNewGame = true,
                    SafeToRemove = false,
                    Multiplayer = true,
                    Campaign = true
                },
                Dependencies = new
                {
                    Require = Array.Empty<string>(),
                    Optional = Array.Empty<string>(),
                    LoadAfter = Array.Empty<string>(),
                    Deprecate = Array.Empty<string>(),
                    Incompatible = Array.Empty<string>()
                },
                Category = LangPack("Map"),
                ModName = LangPack(modName),
                Description = LangPack(
                    $"Custom map template based on '{templateAsset.Name ?? "?"}'.\n\n" +
                    "Created with AnnoMapEditor (native Linux build)."),
                CreatorName = "AnnoMapEditor"
            };

            string json = JsonConvert.SerializeObject(modinfo, Newtonsoft.Json.Formatting.Indented);
            await File.WriteAllTextAsync(Path.Combine(modFolder, "modinfo.json"), json);
        }

        private static Dictionary<string, string> LangPack(string text)
        {
            var d = new Dictionary<string, string>();
            foreach (string lang in new[] {
                "Chinese", "English", "French", "German", "Italian",
                "Japanese", "Korean", "Polish", "Russian", "Spanish", "Taiwanese" })
            {
                d[lang] = text;
            }
            return d;
        }

        // ---------------------- .a7te ----------------------

        private static void WriteA7te(int dimensions, string a7tePath)
        {
            string? dir = Path.GetDirectoryName(a7tePath);
            if (dir != null) Directory.CreateDirectory(dir);

            AnnoEditorLevel level = AnnoEditorLevel.CreateForAnno117(dimensions);

            XmlSerializer serializer = new(typeof(AnnoEditorLevel));
            XmlWriterSettings settings = new()
            {
                Indent = true,
                IndentChars = "  ",
                OmitXmlDeclaration = true
            };
            XmlSerializerNamespaces ns = new(new[] { XmlQualifiedName.Empty });

            using StreamWriter sw = new(a7tePath, false, Encoding.UTF8);
            using XmlWriter xw = XmlWriter.Create(sw, settings);
            serializer.Serialize(xw, level, ns);
        }

        // ---------------------- .a7t (RDA + FileDB) ----------------------

        private static void WriteA7t(MapTemplate mapTemplate, string a7tPath)
        {
            string? dir = Path.GetDirectoryName(a7tPath);
            if (dir != null) Directory.CreateDirectory(dir);

            // Build the gamedata FileDB document.
            (int x, int y, int size) playableArea =
                (mapTemplate.PlayableArea.X, mapTemplate.PlayableArea.Y, mapTemplate.PlayableArea.Width);
            string ambiente = mapTemplate.Session.Region?.Ambiente ?? "Region_map_global";
            Gamedata gameDataItem = new(mapTemplate.Size.X, playableArea, ambiente, true);

            FileDBDocumentSerializer fdbSerializer = new(new() { Version = PickA7tinfoVersion(mapTemplate) });
            IFileDBDocument fileDb = fdbSerializer.WriteObjectStructureToFileDBDocument(gameDataItem);

            using MemoryStream fileDbStream = new();
            DocumentWriter docWriter = new();
            docWriter.WriteFileDBToStream(fileDb, fileDbStream);
            fileDbStream.Position = 0;

            // Wrap the FileDB into a RDA v2.2 container (.a7t = RDA with a single entry "gamedata.data").
            RDABlockCreator.FileType_CompressedExtensions.Add(".data");
            try
            {
                using RDAReader rdaReader = new();
                using BinaryReader reader = new(fileDbStream);

                RDAFolder rdaFolder = new(FileHeader.Version.Version_2_2);
                rdaReader.rdaFolder = rdaFolder;

                DirEntry entry = new()
                {
                    filename = RDAFile.FileNameToRDAFileName("gamedata.data", ""),
                    offset = 0,
                    compressed = (ulong)fileDbStream.Length,
                    filesize = (ulong)fileDbStream.Length,
                    timestamp = RDAExplorer.Misc.DateTimeExtension.ToTimeStamp(DateTime.Now)
                };
                BlockInfo blockInfo = new()
                {
                    flags = 0,
                    fileCount = 1,
                    directorySize = (ulong)fileDbStream.Length,
                    decompressedSize = (ulong)fileDbStream.Length,
                    nextBlock = 0
                };

                RDAFile rdaFile = RDAFile.FromUnmanaged(FileHeader.Version.Version_2_2, entry, blockInfo, reader, null);
                rdaFolder.AddFiles(new List<RDAFile> { rdaFile });

                RDAWriter writer = new(rdaFolder);
                writer.Write(a7tPath, FileHeader.Version.Version_2_2, compress: true, rdaReader, null);
            }
            finally
            {
                RDABlockCreator.FileType_CompressedExtensions.Remove(".data");
            }
        }

        // ---------------------- assets.xml ----------------------

        private static async Task WriteAssetsXmlAsync(string modFolder, string modName, string region,
            MapTemplateAsset sourceAsset, string a7tRelPath,
            long mapTypeGuid,
            long templateEasyGuid, long templateMediumGuid, long templateHardGuid)
        {
            string assetsXmlPath = Path.Combine(modFolder, "data", "base", "config", "export", "assets.xml");
            string? dir = Path.GetDirectoryName(assetsXmlPath);
            if (dir != null) Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.AppendLine("<ModOps>");
            sb.AppendLine();

            // 1. Custom MapTemplateType — appears as a new category alongside vanilla
            //    Default/Donut/Corners… in the New Game menu.
            sb.AppendLine("\t<Asset>");
            sb.AppendLine("\t\t<Template>MapTemplateType</Template>");
            sb.AppendLine("\t\t<Values>");
            sb.AppendLine("\t\t\t<Standard>");
            sb.AppendLine($"\t\t\t\t<GUID>{mapTypeGuid}</GUID>");
            sb.AppendLine($"\t\t\t\t<Name>MapType {modName}</Name>");
            sb.AppendLine("\t\t\t</Standard>");
            sb.AppendLine("\t\t\t<MapTemplateType>");
            sb.AppendLine($"\t\t\t\t<Name>{mapTypeGuid}</Name>");
            sb.AppendLine("\t\t\t</MapTemplateType>");
            sb.AppendLine("\t\t</Values>");
            sb.AppendLine("\t</Asset>");

            // 2-4. Three MapTemplate variants pointing to the same .a7t — Anno's New Game menu
            //      requires multiple difficulty entries per MapTemplateType for the category
            //      to be visible (Taludas mods follow this pattern).
            AppendMapTemplate(sb, templateEasyGuid,   modName + " (Easy)",   "Large",  a7tRelPath, region, mapTypeGuid);
            AppendMapTemplate(sb, templateMediumGuid, modName + " (Medium)", "Medium", a7tRelPath, region, mapTypeGuid);
            AppendMapTemplate(sb, templateHardGuid,   modName + " (Hard)",   "Small",  a7tRelPath, region, mapTypeGuid);

            sb.AppendLine();
            // 5. Register the new MapTemplateType in the global //MapTemplateTypes list — without
            //    this ModOp the asset exists but the category never shows up in the New Game menu.
            //    (Taludas mods always end with this exact ModOp.)
            sb.AppendLine("\t<ModOp Add=\"//MapTemplateTypes\">");
            sb.AppendLine("\t\t<Item>");
            sb.AppendLine($"\t\t\t<MapType>{mapTypeGuid}</MapType>");
            sb.AppendLine("\t\t</Item>");
            sb.AppendLine("\t</ModOp>");
            sb.AppendLine("</ModOps>");

            await File.WriteAllTextAsync(assetsXmlPath, sb.ToString());
        }

        private static void AppendMapTemplate(StringBuilder sb, long guid, string name,
            string islandSize, string a7tRelPath, string region, long mapTypeGuid)
        {
            sb.AppendLine("\t<Asset>");
            sb.AppendLine("\t\t<Template>MapTemplate</Template>");
            sb.AppendLine("\t\t<Values>");
            sb.AppendLine("\t\t\t<Standard>");
            sb.AppendLine($"\t\t\t\t<GUID>{guid}</GUID>");
            sb.AppendLine($"\t\t\t\t<Name>{name}</Name>");
            sb.AppendLine("\t\t\t</Standard>");
            sb.AppendLine("\t\t\t<MapTemplate>");
            sb.AppendLine($"\t\t\t\t<IslandSize>{islandSize}</IslandSize>");
            sb.AppendLine($"\t\t\t\t<TemplateFilename>{a7tRelPath}</TemplateFilename>");
            sb.AppendLine("\t\t\t\t<TemplateSize>Large</TemplateSize>");
            sb.AppendLine("\t\t\t\t<IsUsedByMapGenerator>1</IsUsedByMapGenerator>");
            sb.AppendLine($"\t\t\t\t<TemplateRegion>{region}</TemplateRegion>");
            sb.AppendLine($"\t\t\t\t<MapTemplateType>{mapTypeGuid}</MapTemplateType>");
            sb.AppendLine("\t\t\t</MapTemplate>");
            sb.AppendLine("\t\t</Values>");
            sb.AppendLine("\t</Asset>");
        }

        // ---------------------- localized texts ----------------------

        private static async Task WriteLanguageTextsAsync(string modFolder, string modName,
            long mapTypeGuid, long easyGuid, long mediumGuid, long hardGuid)
        {
            // Anno 117 reads the localized labels via <LineId> (NOT <GUID>) and the ModOp
            // attribute is `Add="..."` (NOT Type="add" Path="..."). Without this exact form,
            // the engine silently ignores the texts and the category shows up nameless.
            //
            // The MapTemplateType label is what the user sees as the category title in the
            // New Game menu — this is the most important entry. The MapTemplate labels are
            // shown for each difficulty.
            var sb = new StringBuilder();
            sb.AppendLine("<ModOps>");
            sb.AppendLine("    <ModOp Add=\"//TextExport/Texts\">");
            AppendText(sb, mapTypeGuid, modName);
            AppendText(sb, easyGuid,    $"{modName} (Easy)");
            AppendText(sb, mediumGuid,  $"{modName} (Medium)");
            AppendText(sb, hardGuid,    $"{modName} (Hard)");
            sb.AppendLine("    </ModOp>");
            sb.AppendLine("</ModOps>");
            string templateText = sb.ToString();

            foreach (string lang in Languages)
            {
                string path = Path.Combine(modFolder, "data", "base", "config", "gui", $"texts_{lang}.xml");
                string? dir = Path.GetDirectoryName(path);
                if (dir != null) Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(path, templateText);
            }
        }

        private static void AppendText(StringBuilder sb, long guid, string text)
        {
            sb.AppendLine("        <Text>");
            sb.AppendLine($"            <LineId>{guid}</LineId>");
            sb.AppendLine($"            <Text>{text}</Text>");
            sb.AppendLine("        </Text>");
        }

        // ---------------------- helpers ----------------------

        private static string SafeName(string raw)
        {
            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (c == ' ' || c == '-' || c == '_') sb.Append('_');
            }
            string s = sb.ToString().Trim('_');
            return string.IsNullOrEmpty(s) ? "custom_map" : s.ToLowerInvariant();
        }
    }
}
