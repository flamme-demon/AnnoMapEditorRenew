using AnnoMapEditor.MapTemplates.Models;

namespace AnnoMapEditor.Utilities.UndoRedo
{
    public record MapElementAddStackEntry : IUndoRedoStackEntry
    {
        public MapElementAddStackEntry(MapElement element, MapTemplate mapTemplate)
        {
            _element = element;
            _mapTemplate = mapTemplate;
        }

        private readonly MapElement _element;
        private readonly MapTemplate _mapTemplate;

        public ActionType ActionType => ActionType.IslandAdd;

        public void Redo() => _mapTemplate.Elements.Add(_element);
        public void Undo() => _mapTemplate.Elements.Remove(_element);
    }
}
