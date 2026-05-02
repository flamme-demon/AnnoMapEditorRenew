using System.Collections.Generic;
using System.Linq;
using AnnoMapEditor.MapTemplates.Models;
using AnnoMapEditor.Utilities;

namespace AnnoMapEditor.MapTemplates
{
    /// <summary>
    /// Surface-level checks that flag the most common breakages in a map template
    /// — the kind of issues that make the game silently refuse the mod or that the
    /// player notices in-game (continental island sticking out, starter outside the
    /// playable area, etc.).
    /// </summary>
    public static class MapTemplateValidator
    {
        /// <summary>
        /// One detected problem. <see cref="Targets"/> lists the elements at fault so the UI
        /// can let the user click the issue line to select them on the canvas.
        /// </summary>
        public record Issue(string Severity, string Message, IReadOnlyList<MapElement> Targets);

        private const int ContinentalSizeThreshold = 1024;

        public static IReadOnlyList<Issue> Validate(MapTemplate? map)
        {
            var issues = new List<Issue>();
            if (map is null) return issues;

            var pa = map.PlayableArea;
            int paX1 = pa.X, paY1 = pa.Y;
            int paX2 = pa.X + pa.Width, paY2 = pa.Y + pa.Height;

            // 1. Inverted PA (would make Anno reject the map outright).
            if (pa.Width <= 0 || pa.Height <= 0 || paX2 <= paX1 || paY2 <= paY1)
                issues.Add(new Issue("error",
                    Localizer.Current["main.issue.pa_inverted"],
                    System.Array.Empty<MapElement>()));

            // 2. Starter spots outside the PA — the game places the player there, so it
            //    has to be inside.
            foreach (var elem in map.Elements.OfType<StartingSpotElement>())
            {
                int x = elem.Position.X, y = elem.Position.Y;
                if (x < paX1 || x > paX2 || y < paY1 || y > paY2)
                    issues.Add(new Issue("warn",
                        Localizer.Current.Format("main.issue.starter_outside_pa", elem.Index + 1),
                        new MapElement[] { elem }));
            }

            // 3. Overlapping islands — flag whenever two non-continental island bboxes
            //    intersect. Catches "Small on top of Large", duplicate-position copies,
            //    everything in between. Continental islands are skipped because their
            //    bbox is the whole map quadrant and they routinely sit under randoms by
            //    design (the engine resolves spawn collisions at runtime).
            var islands = map.Elements.OfType<IslandElement>()
                .Where(i => i.SizeInTiles > 0 && i.SizeInTiles <= ContinentalSizeThreshold)
                .ToList();
            for (int i = 0; i < islands.Count; i++)
            {
                for (int j = i + 1; j < islands.Count; j++)
                {
                    if (BboxOverlap(islands[i], islands[j]))
                    {
                        var pair = new MapElement[] { islands[i], islands[j] };
                        issues.Add(new Issue("info",
                            Localizer.Current.Format("main.issue.duplicate_position",
                                2, $"≈ ({islands[i].Position.X}, {islands[i].Position.Y})")
                            + " — " + string.Join(", ", pair.Select(DescribeElement)),
                            pair));
                    }
                }
            }

            // NOTE: we used to flag continental islands overlapping the PA, but that's
            // by design on every vanilla template — the continentals occupy the corners
            // *outside* the PA but their bbox always spills inside. Keeping the check
            // here would yield 6 false positives on every official map. The user can
            // still tighten the PA via the side panel handles when a continental's
            // outline actually leaves room for ships to sail around.

            return issues;
        }

        /// <summary>True when the two islands' axis-aligned bboxes intersect.</summary>
        private static bool BboxOverlap(IslandElement a, IslandElement b)
        {
            int ax1 = a.Position.X, ay1 = a.Position.Y;
            int ax2 = ax1 + a.SizeInTiles, ay2 = ay1 + a.SizeInTiles;
            int bx1 = b.Position.X, by1 = b.Position.Y;
            int bx2 = bx1 + b.SizeInTiles, by2 = by1 + b.SizeInTiles;
            return !(ax2 <= bx1 || bx2 <= ax1 || ay2 <= by1 || by2 <= ay1);
        }

        /// <summary>Short, human-readable identifier for an element to surface in issues.</summary>
        private static string DescribeElement(MapElement element) => element switch
        {
            FixedIslandElement fix =>
                $"Fixed {fix.IslandAsset?.DisplayName ?? "?"}",
            RandomIslandElement rnd =>
                $"Random {rnd.IslandSize?.Name ?? "?"}",
            StartingSpotElement spot => $"Starter #{spot.Index + 1}",
            _ => element.GetType().Name
        };
    }
}
