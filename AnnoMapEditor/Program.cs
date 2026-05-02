using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using AnnoMods.BBDom;
using AnnoRDA.Loader;
using Avalonia;
using FileDBReader;
using FileDBReader.src;
using FileDBMapTemplateDocument = AnnoMapEditor.MapTemplates.Serializing.Models.MapTemplateDocument;
using AmeFileDBSerializer = AnnoMapEditor.MapTemplates.Serializing.FileDBSerializer;

namespace AnnoMapEditor
{
    internal static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            // Headless dump mode: convert any BBDom binary (.a7tinfo / .a7t internals)
            // into a readable XML tree, with no UI, no DataManager, no game install required.
            // Useful to compare a vanilla template against a Taludas-style mod or our own
            // exports — i.e. inspect the raw `Position`, `MapFilePath`, `Rotation90`, etc.
            //   --xml <input>            → stdout (raw hex payloads)
            //   --xml <input> <output>   → write to file
            //   --xml-decoded …          → same, but bytes interpreted via the a7tinfo schema
            //                              (Position becomes "488 1608", strings become text…)
            // <input> accepts either a real path or "<archive.rda>::<internal/path.a7tinfo>"
            // to read directly from a RDA container — handy for vanilla templates.
            if (args.Length >= 2 && (args[0] == "--xml" || args[0] == "--xml-decoded"))
                return DumpBBDomToXml(args[1], args.Length >= 3 ? args[2] : null,
                    decoded: args[0] == "--xml-decoded");

            // Diagnostic round-trip: read an .a7tinfo (binary or RDA::path), pass it through
            // our typed serializer in V3, write the result. Used to verify that the expanded-
            // template tags (IsEnlargedTemplate / InitialPlayableArea / EnlargementOffset)
            // survive the read/write cycle and end up in the right slots.
            if (args.Length >= 3 && args[0] == "--roundtrip-v3")
                return RoundtripV3(args[1], args[2]).GetAwaiter().GetResult();

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }

        private static int DumpBBDomToXml(string inputPath, string? outputPath, bool decoded)
        {
            try
            {
                using Stream input = OpenInput(inputPath);
                BBDocument doc = BBDocument.LoadStream(input);

                // BBToXmlConverter is internal — reach it via reflection. The conversion is
                // version-agnostic (the parser already resolved V1/V2/V3 inside LoadStream).
                Type converterType = typeof(BBDocument).Assembly
                    .GetType("AnnoMods.BBDom.XML.BBToXmlConverter")
                    ?? throw new InvalidOperationException("BBToXmlConverter not found in AnnoMods.BBDom.dll.");
                object converter = Activator.CreateInstance(converterType, nonPublic: true)
                    ?? throw new InvalidOperationException("Failed to instantiate BBToXmlConverter.");
                MethodInfo toXml = converterType.GetMethod("ToXmlDocument", BindingFlags.Public | BindingFlags.Instance)
                    ?? throw new InvalidOperationException("ToXmlDocument method not found.");
                var xmlDoc = (XmlDocument)toXml.Invoke(converter, new object[] { doc })!;

                if (decoded)
                {
                    // Apply the a7tinfo schema (embedded in the app) so hex blobs become readable
                    // values: Position → "488 1608", MapFilePath → string, Size → "Large", etc.
                    using var schemaStream = typeof(Program).Assembly
                        .GetManifestResourceStream("AnnoMapEditor.Mods.Serialization.a7tinfo.xml")
                        ?? throw new InvalidOperationException("Embedded a7tinfo.xml schema not found.");
                    var schemaDoc = Interpreter.ToInterpreterDoc(schemaStream);
                    var interpreter = new Interpreter(schemaDoc);
                    xmlDoc = new XmlInterpreter(xmlDoc, interpreter).Run();
                }

                var settings = new XmlWriterSettings { Indent = true, IndentChars = "  ", OmitXmlDeclaration = false };
                if (outputPath is null)
                {
                    using var w = XmlWriter.Create(Console.Out, settings);
                    xmlDoc.Save(w);
                    Console.Out.Flush();
                }
                else
                {
                    using var w = XmlWriter.Create(outputPath, settings);
                    xmlDoc.Save(w);
                }
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"{(decoded ? "--xml-decoded" : "--xml")} failed: {e.GetType().Name}: {e.Message}");
                return 1;
            }
        }

        // Accepts either a regular path or "<archive.rda>::<internal/path>". For RDA inputs
        // we materialize the entry into memory so the caller can dispose it cheaply.
        private static Stream OpenInput(string spec)
        {
            int sep = spec.IndexOf("::", StringComparison.Ordinal);
            if (sep < 0) return File.OpenRead(spec);

            string archive = spec.Substring(0, sep);
            string inside  = spec.Substring(sep + 2).Replace('\\', '/');
            var loader = new RdaArchiveLoader();
            var fs = loader.Load(archive);
            using var rdaStream = fs.OpenRead(inside)
                ?? throw new FileNotFoundException($"'{inside}' not found in {archive}");
            var ms = new MemoryStream();
            rdaStream.CopyTo(ms);
            ms.Position = 0;
            return ms;
        }

        private static async Task<int> RoundtripV3(string inputSpec, string outputPath)
        {
            try
            {
                using Stream input = OpenInput(inputSpec);
                var doc = await AmeFileDBSerializer.ReadAsync<FileDBMapTemplateDocument>(input)
                    ?? throw new InvalidOperationException("Failed to deserialize as MapTemplateDocument.");

                Console.Error.WriteLine($"In:  IsEnlarged={doc.MapTemplate?.IsEnlargedTemplate}, " +
                    $"InitPA=[{string.Join(",", doc.MapTemplate?.InitialPlayableArea ?? Array.Empty<int>())}], " +
                    $"PA=[{string.Join(",", doc.MapTemplate?.PlayableArea ?? Array.Empty<int>())}], " +
                    $"Size=[{string.Join(",", doc.MapTemplate?.Size ?? Array.Empty<int>())}]");

                // Mirror MapTemplate.ToTemplateDocument's expanded-template logic so this CLI
                // command exercises the same write path the app uses on a real export. Anything
                // bigger than 2048 (or already flagged) gets the canonical 5-tag header.
                var mt = doc.MapTemplate;
                if (mt is not null)
                {
                    int size = mt.Size?.Length > 0 ? mt.Size[0] : 0;
                    bool isExpanded = size > 2048 || mt.IsEnlargedTemplate == true;
                    if (isExpanded)
                    {
                        mt.IsEnlargedTemplate = true;
                        mt.InitialPlayableArea = new[] { 20, 20, 2020, 2020 };
                        if (mt.EnlargementOffset is null)
                            mt.EnlargementOffset = new[] { 0, 0 };
                    }
                }

                using var output = File.Create(outputPath);
                await AmeFileDBSerializer.WriteAsync(doc, output, FileDBSerializing.FileDBDocumentVersion.Version3);
                Console.Error.WriteLine($"Out: {new FileInfo(outputPath).Length} bytes → {outputPath}");
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"--roundtrip-v3 failed: {e.GetType().Name}: {e.Message}");
                if (e.InnerException != null)
                    Console.Error.WriteLine($"  inner: {e.InnerException.GetType().Name}: {e.InnerException.Message}");
                return 1;
            }
        }

        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
