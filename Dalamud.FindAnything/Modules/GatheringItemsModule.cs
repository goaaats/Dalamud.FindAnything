using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace Dalamud.FindAnything.Modules;

public sealed class GatheringItemsModule : SearchModule {
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.GatheringItems;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        foreach (var (gatheringItemId, searchEntry) in FindAnythingPlugin.SearchDatabase.GetAll<GatheringItem>()) {
            var score = matcher.Matches(searchEntry.Searchable);
            if (score > 0) {
                ctx.AddResult(new GatheringItemResult {
                    Score = score * Weight,
                    GatheringItemId = gatheringItemId,
                    Name = searchEntry.Display,
                });
            }

            if (ctx.OverLimit()) break;
        }
    }

    private class GatheringItemResult : ISearchResult {
        public string CatName => "Gathering Item";
        public required string Name { get; init; }
        private uint? IconId => field ??= GetGatheredItem(GatheringItemId)?.Icon ?? 0u;
        public ISharedImmediateTexture? Icon => IconId is { } validIcon and > 0 ? FindAnythingPlugin.TexCache.GetIcon(validIcon) : null;
        public required int Score { get; init; }
        public required uint GatheringItemId { get; init; }

        public object Key => GatheringItemId;

        public unsafe void Selected() {
            if (GetGatheredItem(GatheringItemId) is { } item) {
                AgentGatheringNote.Instance()->OpenGatherableByItemId((ushort)item.RowId);
            }
        }

        private static Item? GetGatheredItem(uint gatheringItemId) {
            if (Service.Data.GetExcelSheet<GatheringItem>().GetRowOrDefault(gatheringItemId) is { } gatheringItem) {
                var item = gatheringItem.Item;
                if (item.RowId > 0) {
                    return item.GetValueOrDefault<Item>();
                }
            }
            return null;
        }
    }
}
