using Dalamud.Interface.Textures;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace Dalamud.FindAnything.Modules;

public sealed class GatheringItemsModule : SearchModule
{
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.GatheringItems;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        var items = Service.Data.GetExcelSheet<Item>()!;
        var gatheringItem = Service.Data.GetExcelSheet<GatheringItem>()!;

        foreach (var gatherSearch in FindAnythingPlugin.SearchDatabase.GetAll<GatheringItem>())
        {
            var gather = gatheringItem.GetRow(gatherSearch.Key)!;
            var item = items.GetRowOrDefault(gather.Item.RowId);

            if (item == null || item.Value.RowId == 0) {
                continue;
            }

            var score = matcher.Matches(gatherSearch.Value.Searchable);
            if (score > 0) {
                ctx.AddResult(new GatheringItemResult
                {
                    Score = score * Weight,
                    Item = gather,
                    Name = gatherSearch.Value.Display,
                    Icon = FindAnythingPlugin.TexCache.GetIcon(item.Value.Icon),
                });
            }

            if (ctx.OverLimit()) break;
        }
    }

    private class GatheringItemResult : ISearchResult {
        public string CatName => "Gathering Item";
        public required string Name { get; init; }
        public required ISharedImmediateTexture? Icon { get; init; }
        public required int Score { get; init; }
        public required GatheringItem Item { get; init; }

        public object Key => Item.RowId;

        public unsafe void Selected() {
            var id = ItemUtil.GetBaseId(Item.Item.RowId).ItemId;
            if (id > 0) {
                AgentGatheringNote.Instance()->OpenGatherableByItemId((ushort)id);
            }
        }
    }
}