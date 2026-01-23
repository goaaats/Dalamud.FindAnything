using Dalamud.FindAnything.Modules;
using Dalamud.Interface.Textures;
using Lumina.Excel.Sheets;

namespace Dalamud.FindAnything.Lookup;

public class FacewearLookup : ILookup {
    private static FacewearModule.FacewearStyleResult? _baseResult;

    public static void SetBaseResult(FacewearModule.FacewearStyleResult result) {
        _baseResult = result;
    }

    public string GetPlaceholder() {
        if (_baseResult is { } result) {
            return $"Choose facewear for \"{result.Name}\"...";
        }

        return "";
    }

    public LookupResult Lookup(SearchCriteria criteria) {
        var ctx = new SearchContext(criteria);
        var matcher = new FuzzyMatcher(criteria.MatchString, criteria.MatchMode);

        if (_baseResult is not { } baseResult) {
            return LookupResult.Empty;
        }

        foreach (var glassesRef in baseResult.GlassesStyle.Glasses) {
            if (glassesRef.ValueNullable is not { } glasses)
                continue;

            var score = criteria.HasMatchString() ? matcher.Matches(FindAnythingPlugin.Normalizer.Searchable(glasses.Name)) : 1;
            if (score > 0) {
                ctx.AddResult(new FacewearResult {
                    Score = score,
                    Glasses = glasses,
                });
            }
        }

        return LookupResult.Bag(ctx.Results);
    }

    public class FacewearResult : ISearchResult {
        public string CatName => "Facewear";
        public string Name => Glasses.Name.ToText();
        public ISharedImmediateTexture Icon => FindAnythingPlugin.TexCache.GetIcon((uint)Glasses.Icon);
        public required int Score { get; init; }
        public required Glasses Glasses { get; init; }

        public object Key => Glasses.RowId;

        public void Selected() {
            _baseResult?.GlassesSelection = Glasses;
            FacewearUtils.Equip(Glasses);
        }
    }
}
