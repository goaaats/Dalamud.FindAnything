using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace Dalamud.FindAnything.Modules;

public sealed class MinionsModule : SearchModule {
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.Minions;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        if (gameState.IsInCombatDuty() || gameState.IsInCombat()) return;

        var minionDict = FindAnythingPlugin.SearchDatabase.GetAll<Companion>();
        foreach (var minion in FindAnythingPlugin.GameStateCache.UnlockedMinions) {
            var minionEntry = minionDict[minion.RowId];
            var score = matcher.Matches(minionEntry.Searchable);
            if (score > 0) {
                ctx.AddResult(new MinionResult {
                    Score = score * Weight,
                    Name = minionEntry.Display,
                    Minion = minion,
                });
            }
        }
    }

    private class MinionResult : ISearchResult {
        public string CatName => "Minion";
        public required string Name { get; init; }
        public ISharedImmediateTexture Icon => FindAnythingPlugin.TexCache.GetIcon(Minion.Icon);
        public required int Score { get; init; }
        public required Companion Minion { get; init; }

        public object Key => Minion.RowId;

        public unsafe void Selected() {
            ActionManager.Instance()->UseAction(ActionType.Companion, Minion.RowId);
        }
    }
}
