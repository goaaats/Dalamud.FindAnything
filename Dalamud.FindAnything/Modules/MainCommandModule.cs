using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;

namespace Dalamud.FindAnything.Modules;

public sealed class MainCommandModule : SearchModule
{
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.MainCommand;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        if (gameState.IsInEvent()) return;

        foreach (var mainCommand in FindAnythingPlugin.SearchDatabase.GetAll<MainCommand>()) {
            // Record ready check, internal ones
            if (mainCommand.Key is 79 or 38 or 39 or 40 or 43 or 26)
                continue;

            var searchable = mainCommand.Value.Searchable;
            if (searchable == "log out")
                searchable = "logout";

            var score = matcher.Matches(searchable);
            if (score > 0) {
                ctx.AddResult(new MainCommandSearchResult {
                    Score = score * Weight,
                    CommandId = mainCommand.Key,
                    Name = mainCommand.Value.Display,
                    Icon = FindAnythingPlugin.TexCache.MainCommandIcons[mainCommand.Key]
                });
            }

            if (ctx.OverLimit()) break;
        }
    }

    private record MainCommandSearchResult : ISearchResult
    {
        public string CatName => "Commands";
        public required string Name { get; init; }
        public required ISharedImmediateTexture? Icon { get; init; }
        public required int Score { get; init; }
        public required uint CommandId { get; init; }

        public object Key => CommandId;

        public unsafe void Selected() {
            UIModule.Instance()->ExecuteMainCommand(CommandId);
        }
    }
}