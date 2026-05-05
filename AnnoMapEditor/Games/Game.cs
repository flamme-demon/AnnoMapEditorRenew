using System;
using System.Collections.Generic;
using Avalonia.Media;
using AnnoMapEditor.DataArchives.Assets.Models;
using AnnoMapEditor.MapTemplates;

namespace AnnoMapEditor.Games
{
    /*
     * Anno117 — Pillar pictogram from https://pictogrammers.com/library/mdi/.
     * On garde la classe `Game` (abstraite) plutôt que l'inliner dans
     * Anno117Game pour ne pas toucher à toutes les signatures qui prennent
     * un `Game` (DataManager, AssetRepository, GameDefaults, etc.).
     */

    public abstract class Game
    {
        public static Game Anno117 => new Anno117Game();

        public abstract string Title { get; }

        /**
         * This is not the entire Game Path, but the part that is searched for to identify the game. Defaults to the
         * games Title.
         */
        public virtual string Path => Title;

        /**
         * The Icon representation of each game uses a canvas XAML object. The IconGeometry is the vector data to be
         * used for that Canvas.
         */
        public abstract string IconGeometry { get; }
        public virtual string? AssetsXmlPath => null;
        public virtual GameDefaults? GameDefaults => null;
        public virtual StaticGameAssets? StaticAssets => null;
        public virtual IEnumerable<Pool> IslandPools => new List<Pool>();
    }

    /*
     * TODO: For more flexibility and possible future modded data support, using static assets could be a problem.
     * Ideally, all assets should be loaded dynamically. But this would probably require quite a lot of work.
     * This might be an idea for a future version after compatibility with 117 is archived. 
     */
    
    // TODO: Add all remaining kinds of static assets for each game, then remove static assets from the respective asset classes.
    
    public abstract class StaticGameAssets
    {
        public abstract IEnumerable<RegionAsset?> SupportedRegions { get; }
        public abstract IEnumerable<SessionAsset?> SupportedSessions { get; }
        public abstract IEnumerable<SlotAsset?> SupportedSlots { get; }
        public abstract IEnumerable<Type> SupportedAssetTypes { get; }
    }

    public abstract class GameDefaults
    {
        public abstract string DefaultRegionId { get; }
        public abstract long DefaultRegionGuid { get; }
        public abstract SessionAsset? DefaultSessionAsset { get; }
        public virtual MinimapSceneAsset? MinimapSceneInstance => null;
        public virtual bool UsesSlots => false;
        public virtual bool UsesFertilities => false;

        /*
         * Anno 1800:
         *     The session assets for The Old World and The New World to not properly reference their
         *     respective regions in assets.xml.
         */
        public abstract Dictionary<long, long> SessionToRegionGuidDictionary { get; }

        /*
         * Anno 1800:
         *     Each region has its own AmbientName, which is needed when creating the .a7t. These values are missing in
         *     assets.xml. The values seen here were reverse engineered from existing a7t files within the game.
         *
         *     Note: Region assets do contain an attribute "Ambiente". However its value is always "Region_map_global"
         *     and does not match the expected value for a7ts.
         */
        public abstract Dictionary<long, string> RegionAmbienteDictionary { get; }

        public abstract RegionAsset GetRegionAssetFromFilePath(string path);
        public abstract SessionAsset GetSessionAssetFromFilePath(string path);
        public abstract SessionAsset GetSessionAssetFromGuid(long guid);
        public virtual void PostProcess(StandardAsset asset) { }

        public virtual IBrush PinBrushFromSlot(long slotGuid)
        {
            return Brushes.LightGray;
        }
        
        public virtual string ShortenAssetDisplayName<TAsset>(string displayName) where TAsset : StandardAsset
        {
            if (typeof(TAsset) == typeof(FertilityAsset))
            {
                displayName = displayName
                    .Replace("Fertility", "")
                    .Trim();
            }
            return displayName;
        }
    }
    
    
}