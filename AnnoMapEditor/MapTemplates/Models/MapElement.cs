using AnnoMapEditor.MapTemplates.Serializing.Models;
using AnnoMapEditor.MapTemplates.Enums;
using AnnoMapEditor.Utilities;
using System;

namespace AnnoMapEditor.MapTemplates.Models
{
    public abstract class MapElement : ObservableBase
    {
        public Vector2 Position
        {
            get => _position;
            set => SetProperty(ref _position, value);
        }
        private Vector2 _position = Vector2.Zero;


        public MapElement()
        {

        }


        // ---- Serialization ----

        public MapElement(Element element)
        {
            // Direct 1:1 mapping with the binary: Position[0] = X (Anno East axis),
            // Position[1] = Y (Anno North axis). The user-visible (X, Y) in the editor
            // matches what the in-game minimap and the .a7tinfo file say.
            _position = new Vector2(element.Position![0], element.Position![1]);
        }

        public static MapElement FromTemplate(TemplateElement templateElement)
        {
            Element element = templateElement.Element!;
            MapElementType elementType = MapElementType.FromElementValue(templateElement.ElementType);

            if (elementType == MapElementType.FixedIsland)
                return new FixedIslandElement(element);

            else if (elementType == MapElementType.PoolIsland)
                return new RandomIslandElement(element);

            else if (elementType == MapElementType.StartingSpot)
                return new StartingSpotElement(element);

            else
                throw new NotImplementedException();
        }

        public TemplateElement ToTemplate()
        {
            MapElementType elementType = this switch
            {
                FixedIslandElement  _ => MapElementType.FixedIsland,
                RandomIslandElement _ => MapElementType.PoolIsland,
                StartingSpotElement _ => MapElementType.StartingSpot,
                _ => throw new NotImplementedException()
            };

            // Locked=true tells the engine to honour Position verbatim instead of nudging it
            // during map generation. Vanilla DLC1 emits Locked=01 on most elements, but with
            // two exceptions:
            //   1. The unique continental_01 (Vesuvius) NEVER carries Locked.
            //   2. Elements placed in the expanded NE quadrant (X > 2020 or Y > 2020) NEVER
            //      carry Locked either — the engine repositions them to fit the new terrain.
            // Following the same rule keeps mods bit-equivalent to vanilla.
            bool isContinental = (this is FixedIslandElement fix
                && fix.IslandAsset?.FilePath?.Contains("continental",
                    StringComparison.OrdinalIgnoreCase) == true);
            bool outsideOriginalFrame = Position.X > 2020 || Position.Y > 2020;
            bool emitLocked = !isContinental && !outsideOriginalFrame;

            TemplateElement templateElement = new()
            {
                ElementType = elementType.ElementValue,
                Element = new()
                {
                    // Direct 1:1 mapping: editor (X, Y) → binary [X, Y]. Same convention as
                    // vanilla DLC1 .a7tinfo, so the user can read coords from the editor and
                    // know exactly what's stored in the file.
                    Position = new int[] { Position.X, Position.Y },
                    Locked = emitLocked ? true : null
                }
            };

            ToTemplate(templateElement.Element);

            return templateElement;
        }

        protected abstract void ToTemplate(Element resultElement);
    }
}
