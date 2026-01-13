using Dalamud.FindAnything.Lookup;
using Dalamud.Interface.Textures;
using Lumina.Excel.Sheets;
using System.Linq;

namespace Dalamud.FindAnything.Modules;

public sealed class FacewearModule : SearchModule {
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.Facewear;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        foreach (var glassesStyle in Service.Data.GetExcelSheet<GlassesStyle>()) {
            if (!FindAnythingPlugin.GameStateCache.UnlockedFacewearStyleKeys.Contains(glassesStyle.RowId))
                continue;

            var score = matcher.Matches(normalizer.Searchable(glassesStyle.Name));
            if (score > 0) {
                ctx.AddResult(new FacewearStyleResult {
                    Score = score * Weight,
                    GlassesStyle = glassesStyle,
                });
            }

            if (ctx.OverLimit()) break;
        }
    }

    public class FacewearStyleResult : ISearchResult {
        public string CatName =>
            GlassesSelection is not null
                ? "Facewear"
                : "Facewear Style";
        public string Name =>
            GlassesSelection is { } glasses
                ? $"{GlassesStyle.Name.ToText()} ({glasses.Name.ToText()})"
                : GlassesStyle.Name.ToText();
        public ISharedImmediateTexture Icon =>
            GlassesSelection is { } glasses
                ? FindAnythingPlugin.TexCache.GetIcon((uint)glasses.Icon)
                : FindAnythingPlugin.TexCache.GetIcon((uint)GlassesStyle.Icon);
        public required int Score { get; init; }
        public required GlassesStyle GlassesStyle { get; init; }

        public object Key => GlassesStyle.RowId;

        public bool CloseFinder => GlassesSelection != null;

        public Glasses? GlassesSelection;

        public void Selected() {
            if (GlassesSelection is { } glasses) {
                FacewearUtils.Equip(glasses);
                return;
            }

            FacewearLookup.SetBaseResult(this);
            FindAnythingPlugin.Instance.SwitchLookupType(LookupType.Facewear);
        }
    }
}

public static class FacewearUtils {
    public static void Equip(Glasses glasses) {
        if (glasses.RowId != 0)
            Chat.ExecuteCommand($"/facewear \"{glasses.Name.ToText()}\"");
    }
}
