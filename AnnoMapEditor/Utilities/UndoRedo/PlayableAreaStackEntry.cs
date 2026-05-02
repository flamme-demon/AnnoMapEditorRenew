using AnnoMapEditor.MapTemplates.Models;

namespace AnnoMapEditor.Utilities.UndoRedo
{
    /// <summary>
    /// Undo/Redo for a PlayableArea edit. Stores the four BBDom margins
    /// (x1, y1, x2, y2) before and after the change and calls
    /// <see cref="MapTemplate.ResizeAndCommitMapTemplate"/> on Undo / Redo.
    /// Map size never changes here — only the playable area rectangle.
    /// </summary>
    public record PlayableAreaStackEntry : IUndoRedoStackEntry
    {
        public PlayableAreaStackEntry(
            MapTemplate mapTemplate,
            (int x1, int y1, int x2, int y2) oldArea,
            (int x1, int y1, int x2, int y2) newArea)
        {
            _mapTemplate = mapTemplate;
            _oldArea = oldArea;
            _newArea = newArea;
            _mapSize = mapTemplate.Size.X;
        }

        public ActionType ActionType => ActionType.MapProperties;

        private readonly MapTemplate _mapTemplate;
        private readonly (int x1, int y1, int x2, int y2) _oldArea;
        private readonly (int x1, int y1, int x2, int y2) _newArea;
        private readonly int _mapSize;

        public void Undo() => _mapTemplate.ResizeAndCommitMapTemplate(_mapSize, _oldArea);
        public void Redo() => _mapTemplate.ResizeAndCommitMapTemplate(_mapSize, _newArea);
    }
}
