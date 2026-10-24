using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;

namespace Dalamud.FindAnything.Modules;

public sealed class MainCommandModule : SearchModule {
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.MainCommand;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        if (gameState.IsInEvent()) return;

        foreach (var (commandId, searchEntry) in FindAnythingPlugin.SearchDatabase.GetAll<MainCommand>()) {
            var searchable = searchEntry.Searchable;
            if (searchable == "log out")
                searchable = "logout";

            var score = matcher.Matches(searchable);
            if (score > 0) {
                ctx.AddResult(new MainCommandSearchResult {
                    Score = score * Weight,
                    MainCommandId = commandId,
                    Name = searchEntry.Display,
                    Icon = FindAnythingPlugin.TexCache.MainCommandIcons[commandId]
                });
            }

            if (ctx.OverLimit()) break;
        }
    }

    private record MainCommandSearchResult : ISearchResult {
        public string CatName => "Commands";
        public required string Name { get; init; }
        public required ISharedImmediateTexture? Icon { get; init; }
        public required int Score { get; init; }
        public required uint MainCommandId { get; init; }

        public object Key => MainCommandId;

        public unsafe void Selected() {
            UIModule.Instance()->ExecuteMainCommand(MainCommandId);
        }
    }
}
