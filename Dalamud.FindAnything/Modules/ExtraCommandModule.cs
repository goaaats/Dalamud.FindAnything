using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;

namespace Dalamud.FindAnything.Modules;

public sealed class ExtraCommandModule : SearchModule
{
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.ExtraCommand;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        if (gameState.IsInEvent()) return;

        foreach (var extraCommand in FindAnythingPlugin.SearchDatabase.GetAll<ExtraCommand>()) {
            var score = matcher.Matches(extraCommand.Value.Searchable);
            if (score > 0) {
                ctx.AddResult(new ExtraCommandSearchResult {
                    Score = score * Weight,
                    CommandId = extraCommand.Key,
                    Name = extraCommand.Value.Display,
                    Icon = FindAnythingPlugin.TexCache.ExtraCommandIcons[extraCommand.Key]
                });

                if (ctx.OverLimit()) break;
            }
        }
    }

    private class ExtraCommandSearchResult : ISearchResult
    {
        public string CatName => "Extra Commands";
        public required string Name { get; init; }
        public required ISharedImmediateTexture? Icon { get; init; }
        public required int Score { get; init; }
        public required uint CommandId { get; init; }

        public object Key => CommandId;

        public unsafe void Selected() {
            UIModule.Instance()->GetExtraCommandHelper()->ExecuteExtraCommand((int)CommandId);
        }
    }
}