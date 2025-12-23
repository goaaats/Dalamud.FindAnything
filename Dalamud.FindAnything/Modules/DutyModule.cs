using Dalamud.Interface.Textures;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using System.Linq;

namespace Dalamud.FindAnything.Modules;

public sealed class DutyModule : SearchModule
{
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.Duty;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        if (gameState.IsInDuty()) return;

        foreach (var cfc in FindAnythingPlugin.SearchDatabase.GetAll<ContentFinderCondition>()) {
            if (!FindAnythingPlugin.GameStateCache.UnlockedDutyKeys.Contains(cfc.Key))
                continue;

            if (Service.Data.GetExcelSheet<ContentFinderCondition>().GetRowOrDefault(cfc.Key) is not
                { ContentType.ValueNullable: { } contentType } row)
                continue;

            // Only include dungeon, trials, raids, ultimates
            if (contentType.RowId is not (2 or 4 or 5 or 28))
                continue;

            var score = matcher.Matches(cfc.Value.Searchable);
            if (score > 0) {
                ctx.AddResult(new DutySearchResult {
                    Score = score * Weight,
                    CatName = row.Name.ToText(),
                    DataKey = cfc.Key,
                    Name = cfc.Value.Display,
                    Icon = FindAnythingPlugin.TexCache.GetIcon(contentType.Icon),
                });
            }

            if (ctx.OverLimit()) break;
        }

        foreach (var contentRoulette in Service.Data.GetExcelSheet<ContentRoulette>()!.Where(x => x.IsInDutyFinder)) // Also filter !row 7 + 10 here, but not in Lumina schemas yet
        {
            var text = FindAnythingPlugin.SearchDatabase.GetString<ContentRoulette>(contentRoulette.RowId);

            var score = matcher.Matches(text.Searchable);
            if (score > 0) {
                var name = contentRoulette.Category.ToDalamudString().TextValue;
                if (name.IsNullOrWhitespace())
                    name = text.Display;

                ctx.AddResult(new ContentRouletteSearchResult {
                    Score = score * Weight,
                    DataKey = (byte)contentRoulette.RowId,
                    Name = name,
                });
            }

            if (ctx.OverLimit()) break;
        }
    }

    private record DutySearchResult : ISearchResult
    {
        public required string CatName { get; init; }
        public required string Name { get; init; }
        public required ISharedImmediateTexture Icon { get; init; }
        public required int Score { get; init; }
        public required uint DataKey { get; init; }

        public object Key => DataKey;

        public unsafe void Selected() {
            AgentContentsFinder.Instance()->OpenRegularDuty(DataKey);
        }
    }

    private record ContentRouletteSearchResult : ISearchResult
    {
        public string CatName => "Duty Roulette";
        public required string Name { get; init; }
        public ISharedImmediateTexture Icon => FindAnythingPlugin.TexCache.RoulettesIcon;
        public required int Score { get; init; }
        public required byte DataKey { get; init; }

        public object Key => DataKey;

        public unsafe void Selected() {
            AgentContentsFinder.Instance()->OpenRouletteDuty(DataKey);
        }
    }
}