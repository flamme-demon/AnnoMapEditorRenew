using Anno.FileDBModels.Anno1800.IslandTemplate;
using AnnoMapEditor.DataArchives.Assets.Models;
using AnnoMapEditor.MapTemplates.Enums;
using AnnoMapEditor.Utilities;
using FileDBSerializing;
using FileDBSerializing.ObjectSerializer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace AnnoMapEditor.DataArchives.Assets.Repositories
{
    public class FixedIslandRepository : Repository, INotifyCollectionChanged, IEnumerable<FixedIslandAsset>
    {
        private static readonly Logger<FixedIslandRepository> _logger = new();

        public event NotifyCollectionChangedEventHandler? CollectionChanged;


        private readonly List<FixedIslandAsset> _islands = new();

        private readonly IDataArchive _dataArchive;


        public FixedIslandRepository(IDataArchive dataArchive)
        {
            _dataArchive = dataArchive;
        }


        public override async Task InitializeAsync()
        {
            _logger.LogInformation($"Begin loading fixed islands.");
            Stopwatch watch = Stopwatch.StartNew();

            var mapFilePaths = _dataArchive.Find("*.a7m");

            var tasks = mapFilePaths.Select(mapFilePath =>
            {
                return LoadIslandAssetAsync(mapFilePath);
            });

            var islandAssets = await Task.WhenAll(tasks);

            foreach (var islandAsset in islandAssets)
            {
                _islands.Add(islandAsset);
            }

            watch.Stop();
            _logger.LogInformation($"Finished loading fixed islands. Loaded {_islands.Count} islands in {watch.Elapsed.TotalMilliseconds} ms.");
        }

        public async Task<FixedIslandAsset> LoadIslandAssetAsync(String mapFilePath)
        {
            // thumbnail — gamemapimage = visuel "in-game" (île seule sur fond noir),
            // fallback sur les autres variantes si manquante.
            string baseDir = Path.Combine(
                Path.GetDirectoryName(mapFilePath)!,
                "_gamedata",
                Path.GetFileNameWithoutExtension(mapFilePath));

            string[] thumbnailCandidates =
            {
                Path.Combine(baseDir, "gamemapimage.png"),
                Path.Combine(baseDir, "mapimage.png"),
                Path.Combine(baseDir, "provincemapimage.png"),
                Path.Combine(baseDir, "activemapimage.png"),
            };

            Bitmap? thumbnail = null;
            foreach (string candidate in thumbnailCandidates)
            {
                thumbnail = await Task.Run(() => _dataArchive.TryLoadPng(candidate));
                if (thumbnail is not null)
                    break;
            }
            if (thumbnail is null)
                _logger.LogWarning($"No thumbnail found for island '{mapFilePath}'. Continuing without it.");

            // open a7minfo
            string infoFilePath = mapFilePath + "info";

            IslandTemplateDocument? islandTemplate;
            try
            {
                using (var datastream = _dataArchive.OpenRead(infoFilePath))
                using (var memoryAccStream = new MemoryStream())
                {
                    if (datastream is null)
                        throw new Exception($"Could not load a7minfo '{infoFilePath}'.");
                    datastream.CopyTo(memoryAccStream);
                    memoryAccStream.Seek(0, SeekOrigin.Begin);
                    islandTemplate = FileDBConvert.DeserializeObject<IslandTemplateDocument>(memoryAccStream, new FileDBSerializerOptions() { IgnoreMissingProperties = true });
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load a7minfo '{infoFilePath}'.", e);
            }

            // get the island's size in tiles
            int sizeInTiles = islandTemplate?.MapSize?[0] ?? IslandSize.Default.DefaultSizeInTiles;

            // get slots
            // The list of slots is found at <ObjectMetaInfo><SlotObjects> within the
            // a7mfile. It has two kinds of elements.
            //
            // First there are lists with a single item of the following form. They denote the
            // slot group the following slots belong to.
            //   <None>
            //     <value>
            //       <id>{byte[2]}</id>
            //     </value>
            //   </None>.
            //
            // The actual lists of slots look like this:
            //   <None>
            //     <None>
            //       <ObjectId>{long}</ObjectId>
            //       <ObjectGuid>{long}</ObjectGuid>
            //       <Position>{float[3]}</Position>
            //     <None>
            //   <None>
            Dictionary<long, Slot> mineSlots = new();
            if (islandTemplate.ObjectMetaInfo?.SlotObjects is not null)
            {
                foreach ((ShortIdValueWrapper id, List<ObjectItem> items) in islandTemplate.ObjectMetaInfo.SlotObjects)
                {
                    short slotGroupId = id?.value?.id ?? 0;
                    foreach (ObjectItem item in items)
                    {
                        if (item?.ObjectId is null || item?.ObjectGuid is null ||
                            item?.Position is null || item?.Position.Length != 3)
                            continue;

                        long index = item.ObjectId.Value;

                        Slot mineSlot = new()
                        {
                            GroupId = slotGroupId,
                            ObjectId = index,
                            ObjectGuid = item.ObjectGuid.Value,
                            //Position is stored as xyz, and we need x and z
                            Position = new((int)item.Position[0], (int)item.Position[2])
                        };

                        mineSlots.Add(index, mineSlot);
                    }
                }
            }

            return new()
            {
                FilePath = mapFilePath,
                SizeInTiles = sizeInTiles,
                // The .a7m file ships an explicit "active map rect" = the bbox of the
                // inhabitable terrain inside the SizeInTiles square. Pull it through so
                // the editor can render the island at its real visual size (matching the
                // in-game minimap) instead of the larger reserved-terrain square.
                ActiveMapRect = islandTemplate?.ActiveMapRect,
                Thumbnail = thumbnail,
                Slots = mineSlots
            };
        }

        public void Add(FixedIslandAsset fixedIsland)
        {
            _islands.Add(fixedIsland);
            CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Add, fixedIsland));
            _logger.LogInformation($"Added '{fixedIsland.FilePath}'.");
        }

        public FixedIslandAsset GetByFilePath(string filePath)
        {
            return _islands.FirstOrDefault(i => i.FilePath == filePath)
                ?? throw new Exception();
        }

        public bool TryGetByFilePath(string filePath, [NotNullWhen(false)] out FixedIslandAsset? fixedIslandAsset)
        {
            fixedIslandAsset = _islands.FirstOrDefault(i => i.FilePath == filePath);
#pragma warning disable CS8762 // Parameter must have a non-null value when exiting in some condition.
            return fixedIslandAsset != null;
#pragma warning restore CS8762 // Parameter must have a non-null value when exiting in some condition.
        }

        public IFileDBDocument? ReadFileDB(string mapFilePath)
        {
            using Stream? stream = _dataArchive?.OpenRead(mapFilePath);
            if (stream == null)
            {
                _logger.LogWarning($"Could not read FileDB from '{mapFilePath}'. The file could not be found.");
                return null;
            }

            try
            {
                var Version = VersionDetector.GetCompressionVersion(stream);
                var parser = new DocumentParser(Version);
                IFileDBDocument? doc = parser.LoadFileDBDocument(stream);

                return doc;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not read FileDB from '{mapFilePath}'.", ex);
                return null;
            }
        }


        public IEnumerator<FixedIslandAsset> GetEnumerator() => _islands.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _islands.GetEnumerator();
    }
}
