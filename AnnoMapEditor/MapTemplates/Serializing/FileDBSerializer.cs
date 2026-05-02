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
using AnnoMods.BBDom;
using AnnoMods.BBDom.ObjectSerializer;

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
                // BBDocument.LoadStream auto-detects V1/V2/V3 internally. We then deserialize
                // from the parsed document into the typed model — this path uses AnnoMods.BBDom
                // exclusively (no FileDBSerializer fallback), so our new BBDom-native model
                // types (with AnnoMods.BBDom.EncodingAwareStrings.Unicode/UTF8String) work.
                byte[] buffer;
                using (var ms = new MemoryStream())
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    stream.CopyTo(ms);
                    buffer = ms.ToArray();
                }
                try
                {
                    using var versionStream = new MemoryStream(buffer);
                    var bbVersion = AnnoMods.BBDom.IO.VersionDetector.GetCompressionVersion(versionStream);
                    LastReadVersion = BBToFileDBVersion(bbVersion);
                }
                catch { /* leave LastReadVersion at last value */ }

                try
                {
                    using var docStream = new MemoryStream(buffer);
                    var doc = BBDocument.LoadStream(docStream);
                    return BBConvert.DeserializeObjectFromDocument<T>(doc,
                        new BBSerializerOptions { IgnoreMissingProperties = true });
                }
                catch (Exception e)
                {
                    int peekLen = Math.Min(32, buffer.Length);
                    string hex = string.Join(" ", buffer.Take(peekLen).Select(b => b.ToString("X2")));
                    _logger.LogError($"BBDom read failed: {e.Message}");
                    _logger.LogError($"  bytes (hex):   {hex}");
                    _logger.LogError($"  total size:    {buffer.Length} bytes");
                    return null;
                }
            });
        }

        private static FileDBDocumentVersion BBToFileDBVersion(BBDocumentVersion v) => v switch
        {
            BBDocumentVersion.V1 => FileDBDocumentVersion.Version1,
            BBDocumentVersion.V2 => FileDBDocumentVersion.Version2,
            BBDocumentVersion.V3 => FileDBDocumentVersion.Version3,
            _ => FileDBDocumentVersion.Version1
        };

        private static BBDocumentVersion FileDBToBBVersion(FileDBDocumentVersion v) => v switch
        {
            FileDBDocumentVersion.Version1 => BBDocumentVersion.V1,
            FileDBDocumentVersion.Version2 => BBDocumentVersion.V2,
            FileDBDocumentVersion.Version3 => BBDocumentVersion.V3,
            _ => BBDocumentVersion.V1
        };

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
                try
                {
                    // BBConvert serializes our BBDom-native model directly. EnlargementOffset
                    // is a regular property on MapTemplate now, so no post-processing needed.
                    BBConvert.SerializeObject(data,
                        new BBSerializerOptions { Version = FileDBToBBVersion(version) },
                        stream);
                }
                catch (Exception e)
                {
                    _logger.LogError($"{e.Message} \n {e.StackTrace}");
                }
            });
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
