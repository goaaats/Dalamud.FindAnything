using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace Dalamud.FindAnything.Modules;

public sealed class DutyModule : SearchModule {
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.Duty;

    private readonly uint contentTypeDutyRouletteIconId = Service.Data.GetExcelSheet<ContentType>().GetRow(1).Icon;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        if (gameState.IsInDuty()) return;

        foreach (var cfc in FindAnythingPlugin.GameStateCache.UnlockedDuties) {
            if (cfc is not { ContentType.ValueNullable: { } contentType })
                continue;

            // Only include dungeon, trials, raids, ultimates
            if (contentType.RowId is not (2 or 4 or 5 or 28))
                continue;

            var searchEntry = FindAnythingPlugin.SearchDatabase.ContentFinderCondition[cfc];
            var score = matcher.Matches(searchEntry.Searchable);
            if (score > 0) {
                ctx.AddResult(new DutySearchResult {
                    Score = score * Weight,
                    ContentFinderCondition = cfc,
                    ContentType = contentType,
                    Name = searchEntry.Display,
                });
            }

            if (ctx.OverLimit()) break;
        }

        foreach (var (rouletteId, searchEntry) in FindAnythingPlugin.SearchDatabase.ContentRoulette) {
            var score = matcher.MatchesAny(searchEntry.Searchables);
            if (score > 0) {
                ctx.AddResult(new ContentRouletteSearchResult {
                    Score = score * Weight,
                    IconId = contentTypeDutyRouletteIconId,
                    ContentRouletteId = rouletteId,
                    Name = searchEntry.Display,
                });
            }

            if (ctx.OverLimit()) break;
        }
    }

    private record DutySearchResult : ISearchResult {
        public string CatName => field ??= ContentFinderCondition.Name.ToText();
        public required string Name { get; init; }
        public ISharedImmediateTexture Icon => field ??= Utils.GetIcon(ContentType.Icon);
        public required int Score { get; init; }
        public required ContentFinderCondition ContentFinderCondition { get; init; }
        public required ContentType ContentType { get; init; }

        public object Key => ContentFinderCondition.RowId;

        public unsafe void Selected() {
            AgentContentsFinder.Instance()->OpenRegularDuty(ContentFinderCondition.RowId);
        }
    }

    private record ContentRouletteSearchResult : ISearchResult {
        public string CatName => "Duty Roulette";
        public required string Name { get; init; }
        public required uint IconId { get; init; }
        public ISharedImmediateTexture Icon => field ??= Utils.GetIcon(IconId);
        public required int Score { get; init; }
        public required uint ContentRouletteId { get; init; }

        public object Key => ContentRouletteId;

        public unsafe void Selected() {
            AgentContentsFinder.Instance()->OpenRouletteDuty((byte)ContentRouletteId);
        }
    }
}
