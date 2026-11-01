using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Excel.Sheets;

namespace Dalamud.FindAnything.Modules;

public sealed class MountsModule : SearchModule {
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.Mounts;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        // This is nasty, should just use TerritoryIntendedUse...
        var isInNoMountDuty = GameState.IsInCombatDuty();

        // Eureka, Bozja, Occult Crescent
        if (Service.ClientState.TerritoryType is { IsValid: true, Value.ContentFinderCondition: { IsValid: true, Value.ContentType.RowId: 26 or 29 or 38} } ) {
            isInNoMountDuty = false;
        }

        if (isInNoMountDuty || GameState.IsInCombat()) return;

        var mountTable = FindAnythingPlugin.SearchDatabase.Mount;
        foreach (var mount in FindAnythingPlugin.GameStateCache.UnlockedMounts) {
            var mountEntry = mountTable[mount];
            var score = matcher.Matches(mountEntry.Searchable);
            if (score > 0) {
                ctx.AddResult(new MountResult {
                    Score = score * Weight,
                    Name = mountEntry.Display,
                    Mount = mount,
                });
            }

            if (ctx.OverLimit()) break;
        }
    }

    private class MountResult : ISearchResult {
        public string CatName => "Mount";
        public required string Name { get; init; }
        public ISharedImmediateTexture Icon => field ??= Utils.GetIcon(Mount.Icon);
        public required int Score { get; init; }
        public required Mount Mount { get; init; }

        public object Key => Mount.RowId;

        public unsafe void Selected() {
            ActionManager.Instance()->UseAction(ActionType.Mount, Mount.RowId);
        }
    }
}
