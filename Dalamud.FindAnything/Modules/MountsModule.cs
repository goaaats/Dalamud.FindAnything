using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using System.Globalization;

namespace Dalamud.FindAnything.Modules;

public sealed class MountsModule : SearchModule {
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.Mounts;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        // This is nasty, should just use TerritoryIntendedUse...
        var isInNoMountDuty = gameState.IsInCombatDuty();

        if (Service.ClientState.TerritoryType != 0) {
            var currentTerri = Service.Data.GetExcelSheet<TerritoryType>()?
                .GetRow(Service.ClientState.TerritoryType);

            if (currentTerri != null && currentTerri.Value.ContentFinderCondition.RowId != 0) {
                var type = currentTerri.Value.ContentFinderCondition.Value.ContentType.RowId;
                if (type is 26 or 29 or 38) // Eureka, Bozja, Occult Crescent
                    isInNoMountDuty = false;
            }
        }

        if (isInNoMountDuty || gameState.IsInCombat()) return;

        foreach (var mount in FindAnythingPlugin.GameStateCache.UnlockedMounts) {
            var score = matcher.Matches(normalizer.Searchable(mount.Singular));
            if (score > 0) {
                ctx.AddResult(new MountResult {
                    Score = score * Weight,
                    Mount = mount,
                });
            }

            if (ctx.OverLimit()) break;
        }
    }

    private class MountResult : ISearchResult {
        public string CatName => "Mount";
        public string Name => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Mount.Singular.ToText());
        public ISharedImmediateTexture Icon => FindAnythingPlugin.TexCache.GetIcon(Mount.Icon);
        public required int Score { get; init; }
        public required Mount Mount { get; init; }

        public object Key => Mount.RowId;

        public unsafe void Selected() {
            ActionManager.Instance()->UseAction(ActionType.Mount, Mount.RowId);
        }
    }
}
