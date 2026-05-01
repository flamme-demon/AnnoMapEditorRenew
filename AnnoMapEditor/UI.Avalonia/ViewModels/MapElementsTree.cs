using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AnnoMapEditor.MapTemplates.Enums;
using AnnoMapEditor.MapTemplates.Models;
using AnnoMapEditor.Utilities;

namespace AnnoMapEditor.UI.Avalonia.ViewModels
{
    /// <summary>
    /// One node in the elements TreeView. Either a category header (Element=null,
    /// Children populated) or a leaf bound to a single MapElement.
    /// </summary>
    public class MapElementNode
    {
        public string Label { get; set; }
        public MapElement? Element { get; }
        public ObservableCollection<MapElementNode> Children { get; } = new();
        public bool IsCategory => Element is null;

        public MapElementNode(string label, MapElement? element = null)
        {
            Label = label;
            Element = element;
        }
    }

    /// <summary>
    /// Builds the tree for the right-pane TreeView from a MapTemplate. Categories
    /// are translated via Localizer; leaf labels show the element's discriminating
    /// info (position, type, asset name).
    /// </summary>
    public static class MapElementsTreeBuilder
    {
        public static ObservableCollection<MapElementNode> Build(MapTemplate? template)
        {
            var root = new ObservableCollection<MapElementNode>();
            if (template is null) return root;

            var startingSpots = new MapElementNode(Localizer.Current["tree.cat.players"]);
            var starterIslands = new MapElementNode(Localizer.Current["tree.cat.starter_islands"]);
            var randomIslands  = new MapElementNode(Localizer.Current["tree.cat.random_islands"]);
            var fixedIslands   = new MapElementNode(Localizer.Current["tree.cat.fixed_islands"]);
            var npcs           = new MapElementNode(Localizer.Current["tree.cat.npcs"]);
            var decoration     = new MapElementNode(Localizer.Current["tree.cat.decoration"]);
            var randomNpcs     = new MapElementNode(Localizer.Current["tree.cat.random_npcs"]);

            foreach (var elem in template.Elements)
            {
                switch (elem)
                {
                    case StartingSpotElement spot:
                        startingSpots.Children.Add(new MapElementNode(
                            $"#{spot.Index + 1}  ({spot.Position.X}, {spot.Position.Y})", spot));
                        break;

                    case IslandElement isl when isl.IslandType == IslandType.Starter:
                        starterIslands.Children.Add(new MapElementNode(LeafLabel(isl), isl));
                        break;

                    case IslandElement isl when isl.IslandType == IslandType.ThirdParty
                                              || isl.IslandType == IslandType.PirateIsland:
                        npcs.Children.Add(new MapElementNode(LeafLabel(isl), isl));
                        break;

                    case IslandElement isl when isl.IslandType == IslandType.Decoration
                                              || isl.IslandType == IslandType.Cliff
                                              || isl.IslandType == IslandType.VolcanicIsland:
                        decoration.Children.Add(new MapElementNode(LeafLabel(isl), isl));
                        break;

                    case RandomIslandElement rnd:
                        randomIslands.Children.Add(new MapElementNode(LeafLabel(rnd), rnd));
                        break;

                    case FixedIslandElement fix:
                        fixedIslands.Children.Add(new MapElementNode(LeafLabel(fix), fix));
                        break;
                }
            }

            foreach (var npc in template.NPCPlacements)
            {
                randomNpcs.Children.Add(new MapElementNode(
                    $"GUID {npc.Guid}  ({npc.Position.X}, {npc.Position.Y})"));
            }

            // Only show non-empty categories — a sparse tree is easier to scan.
            foreach (var cat in new[] { startingSpots, starterIslands, randomIslands,
                                        fixedIslands, npcs, decoration, randomNpcs })
            {
                if (cat.Children.Count > 0)
                {
                    cat.Label = $"{cat.Label}  ({cat.Children.Count})";
                    root.Add(cat);
                }
            }

            return root;
        }

        private static string LeafLabel(IslandElement isl)
        {
            string typeTag = isl.IslandType?.Name ?? "?";
            string posTag = $"({isl.Position.X}, {isl.Position.Y})";
            return isl switch
            {
                FixedIslandElement fix => $"{typeTag} · {fix.IslandAsset?.DisplayName ?? "?"} · {posTag}",
                RandomIslandElement rnd => $"{typeTag} · random {rnd.IslandSize?.ToString() ?? "?"} · {posTag}",
                _ => $"{typeTag} · {posTag}"
            };
        }
    }

    // Small helper to make the Label setter visible (we mutate it after counting children).
    public partial class MapElementNodeMutable
    {
        // No-op placeholder kept for symmetry; Label is plain auto-prop above.
    }
}
