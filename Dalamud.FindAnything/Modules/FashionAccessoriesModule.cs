using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using System.Linq;

namespace Dalamud.FindAnything.Modules;

public sealed class FashionAccessoriesModule : SearchModule
{
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.FashionAccessories;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        foreach (var ornament in Service.Data.GetExcelSheet<Ornament>())
        {
            if (!FindAnythingPlugin.GameStateCache.UnlockedFashionAccessoryKeys.Contains(ornament.RowId))
                continue;

            var score = matcher.Matches(normalizer.Searchable(ornament.Singular));
            if (score > 0)
            {
                ctx.AddResult(new FashionAccessoryResult
                {
                    Score = score * Weight,
                    Ornament = ornament,
                });
            }

            if (ctx.OverLimit()) break;
        }
    }

    private class FashionAccessoryResult : ISearchResult
    {
        public string CatName => "Fashion Accessory";
        public string Name => Ornament.Singular.ToText();
        public ISharedImmediateTexture Icon => FindAnythingPlugin.TexCache.GetIcon(Ornament.Icon);
        public required int Score { get; init; }
        public required Ornament Ornament { get; init; }

        public object Key => Ornament.RowId;

        public unsafe void Selected()
        {
            ActionManager.Instance()->UseAction(ActionType.Ornament, Ornament.RowId);
        }
    }
}