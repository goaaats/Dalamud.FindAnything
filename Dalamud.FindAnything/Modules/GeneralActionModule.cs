using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace Dalamud.FindAnything.Modules;

public sealed class GeneralActionModule : SearchModule {
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.GeneralAction;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        if (gameState.IsInEvent()) return;

        var hasMelding = Service.UnlockState.IsUnlockLinkUnlocked(66175, 0);         // Waking the Spirit
        var hasAdvancedMelding = Service.UnlockState.IsUnlockLinkUnlocked(66176, 0); // Melding Materia Muchly

        foreach (var (generalActionId, searchEntry) in FindAnythingPlugin.SearchDatabase.GeneralAction) {
            // Skip invalid entries, jump, etc
            if (generalActionId is 2 or 3 or 1 or 0 or 11 or 26 or 27 or 16 or 17)
                continue;

            // Skip Materia Melding/Advanced Material Melding, based on what is unlocked
            if ((!hasMelding || hasAdvancedMelding) && generalActionId is 12)
                continue;
            if (!hasAdvancedMelding && generalActionId is 13)
                continue;

            var score = matcher.Matches(searchEntry.Searchable);
            if (score > 0)
                ctx.AddResult(new GeneralActionSearchResult {
                    GeneralActionId = generalActionId,
                    Score = score * Weight,
                    Name = searchEntry.Display,
                });
        }
    }

    private static int GetIconId(uint extraCommandId)
        => Service.Data.GetExcelSheet<GeneralAction>().GetRow(extraCommandId).Icon;

    private class GeneralActionSearchResult : ISearchResult {
        public string CatName => "General Actions";
        public required string Name { get; init; }
        public ISharedImmediateTexture Icon => field ??= Utils.GetIcon(GetIconId(GeneralActionId));
        public required int Score { get; init; }
        public required uint GeneralActionId { get; init; }

        public object Key => GeneralActionId;

        public unsafe void Selected() {
            ActionManager.Instance()->UseAction(ActionType.GeneralAction, GeneralActionId);
        }
    }
}
