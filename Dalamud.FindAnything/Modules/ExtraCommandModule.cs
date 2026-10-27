using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;

namespace Dalamud.FindAnything.Modules;

public sealed class ExtraCommandModule : SearchModule {
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.ExtraCommand;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        if (gameState.IsInEvent()) return;

        foreach (var (extraCommandId, searchEntry) in FindAnythingPlugin.SearchDatabase.ExtraCommand) {
            var score = matcher.Matches(searchEntry.Searchable);
            if (score > 0) {
                ctx.AddResult(new ExtraCommandSearchResult {
                    Score = score * Weight,
                    ExtraCommandId = extraCommandId,
                    Name = searchEntry.Display,
                });

                if (ctx.OverLimit()) break;
            }
        }
    }

    private static int GetIconId(uint extraCommandId)
        => Service.Data.GetExcelSheet<ExtraCommand>().GetRow(extraCommandId).Icon;

    private class ExtraCommandSearchResult : ISearchResult {
        public string CatName => "Extra Commands";
        public required string Name { get; init; }
        public ISharedImmediateTexture Icon => field ??= Utils.GetIcon(GetIconId(ExtraCommandId));
        public required int Score { get; init; }
        public required uint ExtraCommandId { get; init; }

        public object Key => ExtraCommandId;

        public unsafe void Selected() {
            UIModule.Instance()->GetExtraCommandHelper()->ExecuteExtraCommand((int)ExtraCommandId);
        }
    }
}
