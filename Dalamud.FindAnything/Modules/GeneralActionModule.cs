using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace Dalamud.FindAnything.Modules;

public sealed class GeneralActionModule : SearchModule
{
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.GeneralAction;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        if (gameState.IsInEvent()) return;

        unsafe {
            var hasMelding = UIState.Instance()->IsUnlockLinkUnlockedOrQuestCompleted(66175); // Waking the Spirit
            var hasAdvancedMelding = UIState.Instance()->IsUnlockLinkUnlockedOrQuestCompleted(66176); // Melding Materia Muchly

            foreach (var generalAction in FindAnythingPlugin.SearchDatabase.GetAll<GeneralAction>()) {
                // Skip invalid entries, jump, etc
                if (generalAction.Key is 2 or 3 or 1 or 0 or 11 or 26 or 27 or 16 or 17)
                    continue;

                // Skip Materia Melding/Advanced Material Melding, based on what is unlocked
                if ((!hasMelding || hasAdvancedMelding) && generalAction.Key is 12)
                    continue;
                if (!hasAdvancedMelding && generalAction.Key is 13)
                    continue;

                var score = matcher.Matches(generalAction.Value.Searchable);
                if (score > 0)
                    ctx.AddResult(new GeneralActionSearchResult {
                        Id = generalAction.Key,
                        Score = score * Weight,
                        Name = generalAction.Value.Display,
                        Icon = FindAnythingPlugin.TexCache.GeneralActionIcons[generalAction.Key]
                    });
            }
        }
    }

    public class GeneralActionSearchResult : ISearchResult
    {
        public string CatName => "General Actions";
        public required string Name { get; set; }
        public required ISharedImmediateTexture? Icon { get; init; }
        public required int Score { get; init; }
        public required uint Id { get; set; }

        public object Key => Id;

        public unsafe void Selected() {
            ActionManager.Instance()->UseAction(ActionType.GeneralAction, Id);
        }
    }
}