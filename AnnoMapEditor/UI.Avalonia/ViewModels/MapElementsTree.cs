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
    public enum NodeKind { Category, Element, Zone }

    public class MapElementNode
    {
        public string Label { get; set; }
        public MapElement? Element { get; }
        public NodeKind Kind { get; }
        /// <summary>
        /// Tag identifying which non-element thing this node represents. For zones,
        /// it's the zone id (e.g. "PlayableArea"). Null for categories and elements.
        /// </summary>
        public string? ZoneId { get; }
        public ObservableCollection<MapElementNode> Children { get; } = new();
        public bool IsCategory => Kind == NodeKind.Category;

        // Element / category constructor (kept compatible with existing callsites).
        public MapElementNode(string label, MapElement? element = null)
        {
            Label = label;
            Element = element;
            Kind = element is null ? NodeKind.Category : NodeKind.Element;
        }

        // Zone-leaf constructor — clicking it should focus the zone in the editor.
        public MapElementNode(string label, string zoneId)
        {
            Label = label;
            Element = null;
            ZoneId = zoneId;
            Kind = NodeKind.Zone;
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

            // Zones (non-element editable areas). The current PlayableArea already lives in
            // the left-hand "Session properties" panel — no need to duplicate it here. We
            // only surface secondary zones that don't have their own dedicated UI:
            //   - InitialPlayableArea: the DLC1 reference 2048 frame, only present on
            //     expanded templates.
            var zones = new MapElementNode(Localizer.Current["tree.cat.zones"]);
            var initPa = template.InitialPlayableArea;
            if (initPa.Width > 0 && initPa.Height > 0)
            {
                zones.Children.Add(new MapElementNode(
                    $"{Localizer.Current["tree.zone.initial_playable"]}  ({initPa.X},{initPa.Y}) → ({initPa.X + initPa.Width},{initPa.Y + initPa.Height})",
                    zoneId: "InitialPlayableArea"));
            }

            var startingSpots   = new MapElementNode(Localizer.Current["tree.cat.players"]);
            var continental     = new MapElementNode(Localizer.Current["tree.cat.continental"]);
            var starterIslands  = new MapElementNode(Localizer.Current["tree.cat.starter_islands"]);
            var randomIslands   = new MapElementNode(Localizer.Current["tree.cat.random_islands"]);
            var fixedIslands    = new MapElementNode(Localizer.Current["tree.cat.fixed_islands"]);
            var npcs            = new MapElementNode(Localizer.Current["tree.cat.npcs"]);
            var decoration      = new MapElementNode(Localizer.Current["tree.cat.decoration"]);
            var randomNpcs      = new MapElementNode(Localizer.Current["tree.cat.random_npcs"]);

            // "Frontière" category groups every island large enough to act as a corner
            // border on a vanilla DLC1 corners-style map: random Large (384), random
            // ExtraLarge (400), and the unique fixed continental (int.MaxValue). This
            // matches the visual the user expects — four big cornered cadres listed
            // together — even when no actual continental is present in the template.
            const int ContinentalSizeThreshold = 320;

            foreach (var elem in template.Elements)
            {
                switch (elem)
                {
                    case StartingSpotElement spot:
                        startingSpots.Children.Add(new MapElementNode(
                            $"#{spot.Index + 1}  ({spot.Position.X}, {spot.Position.Y})", spot));
                        break;

                    case IslandElement isl when isl.SizeInTiles > ContinentalSizeThreshold:
                        continental.Children.Add(new MapElementNode(LeafLabel(isl), isl));
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

            // Zones is only shown when it actually has secondary zones to display
            // (currently: InitialPlayableArea on DLC1 expanded templates). On regular
            // 2048 maps the section stays hidden so the tree starts directly with
            // "Joueurs" / "Îles starter" etc.
            foreach (var cat in new[] { zones, continental, startingSpots, starterIslands, randomIslands,
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
