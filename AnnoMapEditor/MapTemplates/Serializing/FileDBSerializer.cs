using System;
using FileDBReader;
using FileDBReader.src.XmlRepresentation;
using FileDBSerializing;
using FileDBSerializing.ObjectSerializer;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using AnnoMapEditor.Utilities;

namespace AnnoMapEditor.MapTemplates.Serializing
{
    internal class FileDBSerializer
    {
        private static readonly Logger<FileDBSerializer> _logger = new();
        
        // Side-channel for the caller to know which version was actually used.
        public static FileDBDocumentVersion LastReadVersion { get; private set; } = FileDBDocumentVersion.Version1;

        public static async Task<T?> ReadAsync<T>(Stream stream) where T : class, new()
        {
            return await Task.Run(() =>
            {
                // Buffer the stream so we can rewind and retry with multiple versions.
                // Anno 1800 uses V1; Anno 117 uses V2/V3.
                byte[] buffer;
                using (var ms = new MemoryStream())
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    stream.CopyTo(ms);
                    buffer = ms.ToArray();
                }

                FileDBDocumentVersion[] versionsToTry;
                try
                {
                    using var detect = new MemoryStream(buffer);
                    var detected = VersionDetector.GetCompressionVersion(detect);
                    versionsToTry = OrderedVersionsStartingWith(detected);
                }
                catch
                {
                    versionsToTry = new[]
                    {
                        FileDBDocumentVersion.Version1,
                        FileDBDocumentVersion.Version2,
                        FileDBDocumentVersion.Version3
                    };
                }

                Exception? lastError = null;
                foreach (var version in versionsToTry)
                {
                    try
                    {
                        using var attemptStream = new MemoryStream(buffer);
                        var result = FileDBConvert.DeserializeObject<T>(attemptStream,
                            new FileDBSerializerOptions
                            {
                                Version = version,
                                IgnoreMissingProperties = true
                            });
                        if (result != null)
                        {
                            LastReadVersion = version;
                            return result;
                        }
                    }
                    catch (Exception e)
                    {
                        lastError = e;
                    }
                }

                if (lastError != null)
                {
                    int peekLen = Math.Min(32, buffer.Length);
                    string hex = string.Join(" ", buffer.Take(peekLen).Select(b => b.ToString("X2")));
                    string ascii = new string(buffer.Take(peekLen).Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray());
                    _logger.LogError($"All FileDB versions failed. Last: {lastError.Message}");
                    _logger.LogError($"  bytes (hex):   {hex}");
                    _logger.LogError($"  bytes (ascii): {ascii}");
                    _logger.LogError($"  total size:    {buffer.Length} bytes");
                }
                return null;
            });
        }

        private static FileDBDocumentVersion[] OrderedVersionsStartingWith(FileDBDocumentVersion first)
        {
            var all = new[]
            {
                FileDBDocumentVersion.Version1,
                FileDBDocumentVersion.Version2,
                FileDBDocumentVersion.Version3
            };
            return all.OrderBy(v => v == first ? 0 : 1).ToArray();
        }

        public static async Task<T?> ReadFromXmlAsync<T>(Stream stream) where T : class, new()
        {
            return await Task.Run(() =>
            {
                try
                { 
                    // load xml
                    XmlDocument xmlDocument = new();
                    xmlDocument.Load(stream);

                    // convert to bytes
                    XmlDocument? interpreterDocument = GetEmbeddedXmlDocument("AnnoMapEditor.Mods.Serialization.a7tinfo.xml");
                    if (interpreterDocument is null)
                        return null;
                    XmlDocument xmlWithBytes = new XmlExporter(xmlDocument, new(interpreterDocument)).Run();

                    // convert to FileDB
                    XmlFileDbConverter converter = new(FileDBDocumentVersion.Version1);
                    IFileDBDocument doc = converter.ToFileDb(xmlWithBytes);

                    // construct deserialize into objects
                    FileDBDocumentDeserializer<T> deserializer = new(new FileDBSerializerOptions() { IgnoreMissingProperties = true });
                    return deserializer.GetObjectStructureFromFileDBDocument(doc);
                }
                catch (Exception e)
                {
                    _logger.LogError($"{e.Message} \n {e.StackTrace}");
                    return null;
                }
            });
        }

        public static async Task WriteAsync(object data, Stream stream,
            FileDBDocumentVersion version = FileDBDocumentVersion.Version1)
        {
            await Task.Run(() =>
            {
                // For V3 (Anno 117), try the rewritten AnnoMods.BBDom library first — it has a
                // dedicated V3 writer (BBStructureWriter_V3). If it fails (incompatible attribute
                // schema with the legacy Anno.FileDBModels.dll), we fall back to the legacy
                // serializer so at least a non-empty file is written.
                if (version == FileDBDocumentVersion.Version3)
                {
                    try
                    {
                        WriteAsBBDomV3(data, stream);
                        return;
                    }
                    catch (Exception bbEx)
                    {
                        Exception unwrapped = bbEx is System.Reflection.TargetInvocationException tie && tie.InnerException != null
                            ? tie.InnerException
                            : bbEx;
                        _logger.LogWarning($"BBDom V3 writer failed ({unwrapped.GetType().Name}: {unwrapped.Message}) — falling back to legacy V3.");
                        try { stream.Seek(0, SeekOrigin.Begin); stream.SetLength(0); } catch { }
                    }
                }

                try
                {
                    FileDBConvert.SerializeObject(data, new() { Version = version }, stream);
                }
                catch (Exception e)
                {
                    _logger.LogError($"{e.Message} \n {e.StackTrace}");
                }
            });
        }

        private static void WriteAsBBDomV3(object data, Stream stream)
        {
            var bbConvertType = Type.GetType("AnnoMods.BBDom.ObjectSerializer.BBConvert, AnnoMods.BBDom");
            var optionsType   = Type.GetType("AnnoMods.BBDom.ObjectSerializer.BBSerializerOptions, AnnoMods.BBDom");
            var versionEnum   = Type.GetType("AnnoMods.BBDom.BBDocumentVersion, AnnoMods.BBDom");

            if (bbConvertType is null || optionsType is null || versionEnum is null)
                throw new InvalidOperationException("AnnoMods.BBDom not loaded.");

            object opts = Activator.CreateInstance(optionsType)!;
            optionsType.GetProperty("Version")?.SetValue(opts, Enum.Parse(versionEnum, "V3"));
            var ignoreProp = optionsType.GetProperty("IgnoreMissingProperties");
            if (ignoreProp != null) ignoreProp.SetValue(opts, true);

            var openMethod = bbConvertType.GetMethods()
                .First(m => m.Name == "SerializeObject"
                            && m.IsGenericMethodDefinition
                            && m.GetParameters().Length == 3);
            var closedMethod = openMethod.MakeGenericMethod(data.GetType());
            closedMethod.Invoke(null, new object[] { data, opts, stream });
        }

        public static async Task WriteToXmlAsync(object data, Stream stream)
        {
            await Task.Run(() =>
            {
                try
                {
                    FileDBDocumentSerializer serializer = new(new FileDBSerializerOptions());
                    IFileDBDocument doc = serializer.WriteObjectStructureToFileDBDocument(data);

                    // convert to xml with bytes
                    FileDbXmlConverter converter = new();
                    XmlDocument xmlWithBytes = converter.ToXml(doc);

                    // interpret bytes
                    XmlDocument? interpreterDocument = GetEmbeddedXmlDocument("AnnoMapEditor.Mods.Serialization.a7tinfo.xml");
                    if (interpreterDocument is null)
                        return;
                    XmlDocument xmlDocument = new XmlInterpreter(xmlWithBytes, new(interpreterDocument)).Run();

                    xmlDocument.Save(stream);
                }
                catch (Exception e)
                {
                    _logger.LogError($"{e.Message} \n {e.StackTrace}");
                }
            });
        }

        private static XmlDocument? GetEmbeddedXmlDocument(string resourceName)
        {
            Assembly me = Assembly.GetExecutingAssembly();
            using (var resource = me.GetManifestResourceStream(resourceName))
            {
                if (resource is not null)
                {
                    XmlDocument doc = new();
                    doc.Load(resource);
                    return doc;
                }
            }
            return null;
        }
    }
}
