using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using System.Linq;

namespace Dalamud.FindAnything.Modules;

public sealed class CollectionModule : SearchModule
{
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.Collection;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        foreach (var mcGuffin in Service.Data.GetExcelSheet<McGuffin>()) {
            if (!FindAnythingPlugin.GameStateCache.UnlockedCollectionKeys.Contains(mcGuffin.RowId))
                continue;

            var uiData = mcGuffin.UIData.Value; // Already checked validity in UnlockedCollectionKeys
            var score = matcher.Matches(normalizer.Searchable(uiData.Name));
            if (score > 0) {
                ctx.AddResult(new CollectionResult {
                    Score = score * Weight,
                    McGuffin = mcGuffin,
                    McGuffinUiData = uiData,
                });
            }

            if (ctx.OverLimit()) break;
        }
    }

    private class CollectionResult : ISearchResult
    {
        public string CatName => "Collection";
        public string Name => McGuffinUiData.Name.ToText();
        public ISharedImmediateTexture Icon => FindAnythingPlugin.TexCache.GetIcon(McGuffinUiData.Icon);
        public required int Score { get; init; }
        public required McGuffin McGuffin { get; init; }
        public required McGuffinUIData McGuffinUiData { get; init; }

        public object Key => McGuffin.RowId;

        public unsafe void Selected() {
            var agent = AgentMcGuffin.Instance();
            if (agent != null) {
                agent->OpenMcGuffin(McGuffin.RowId);
            }
        }
    }
}