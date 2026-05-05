using AnnoMapEditor.DataArchives.Assets.Models;
using AnnoMapEditor.MapTemplates.Enums;
using AnnoMapEditor.MapTemplates.Models;

namespace AnnoMapEditor.MapTemplates.Validation
{
    public class SmallPoolSizeValidator : IMapTemplateValidator
    {
        public MapTemplateValidatorResult Validate(MapTemplate mapTemplate)
        {
            int smallIslandCount = 0;
            int thirdPartyCount = 0;
            int pirateCount = 0;

            foreach (var element in mapTemplate.Elements)
            {
                if (element is RandomIslandElement randomIsland)
                {
                    if (randomIsland.IslandType == IslandType.ThirdParty)
                        ++thirdPartyCount;

                    else if (randomIsland.IslandType == IslandType.PirateIsland)
                        ++pirateCount;

                    else if (randomIsland.IslandSize == IslandSize.Small)
                        ++smallIslandCount;
                }
            }

            int maxPoolSize = Pool.GetPool(mapTemplate.Session.Region, IslandSize.Small).Size;
            if (smallIslandCount <= maxPoolSize)
                return MapTemplateValidatorResult.Ok;
            else
                return new(MapTemplateValidatorStatus.Warning, $"Too many {IslandSize.Small.Name} random islands", $"Only the first {maxPoolSize} will be used.");
        }
    }
}
