using AnnoMapEditor.MapTemplates.Serializing.Models;
using AnnoMapEditor.MapTemplates.Enums;
using IslandType = AnnoMapEditor.MapTemplates.Enums.IslandType;

namespace AnnoMapEditor.MapTemplates.Models
{
    public class RandomIslandElement : IslandElement
    {
        public IslandSize IslandSize
        {
            get => _islandSize;
            set
            {
                SetProperty(ref _islandSize, value);
                SizeInTiles = _islandSize.DefaultSizeInTiles;
            }
        }
        private IslandSize _islandSize;

        /// <summary>
        /// True when the source template did not include a <c>&lt;Size&gt;</c> tag for this
        /// element. Vanilla DLC1 corners templates use those "no-size" entries as generic
        /// placement zones — the engine fills them at runtime with any compatible random
        /// island. We surface the flag so the editor can render them with a distinct
        /// "zone" cadre instead of pretending they're a small random island.
        /// </summary>
        public bool HasExplicitSize { get; private set; } = true;


        public RandomIslandElement(IslandSize islandSize, IslandType islandType)
            : base(islandType)
        {
            IslandSize = islandSize;
        }


        // ---- Serialization ----

        public RandomIslandElement(Element sourceElement)
            : base(sourceElement)
        {
            HasExplicitSize = sourceElement.Size is not null;
            IslandSize = IslandSize.FromElementValue(sourceElement.Size);
        }

        protected override void ToTemplate(Element resultElement)
        {
            base.ToTemplate(resultElement);

            // Preserve the original "no Size tag" form for placeholder zones — re-emitting
            // <Size>0000</Size> would change the engine's behaviour from "any random island"
            // to "force a Small one".
            resultElement.Size = HasExplicitSize ? IslandSize.ElementValue : null;
            resultElement.Difficulty = new();

            // Vanilla DLC1 expanded marks SIZED random islands placed OUTSIDE the 2020
            // inner frame with <Type><id>0700</id></Type> (= Normal type explicit). A bare
            // <Type /> on a sized random hors cadre tells the engine "any type" → it ends
            // up picking the volcanic/continental pool that yields Obsidian. Inside the
            // 2020 frame, vanilla keeps <Type /> empty even for sized random islands.
            // Mirroring that exact rule keeps newly-placed random islands visually and
            // economically equivalent to vanilla DLC1.
            short? typeId = IslandType.ElementValue;
            bool outsideFrame = Position.X > 2020 || Position.Y > 2020;
            if (typeId == null && HasExplicitSize && outsideFrame
                && MapTemplate.IsExportingExpandedMap)
            {
                typeId = 7;
            }

            resultElement.Config = new()
            {
                Type = new() { id = typeId },
                Difficulty = new(),
                // Vanilla DLC1 always emits <TypePerConstructionArea /> (empty) inside the
                // random island Config — both inside and outside the 2020 frame. Omitting it
                // changes the binary structure (extra trailing fields detected by the engine
                // as a different schema), so we always emit an empty list to stay byte-compatible.
                TypePerConstructionArea = new System.Collections.Generic.List<System.Tuple<short, Serializing.Models.IslandTypeRef>>()
            };
        }
    }
}
