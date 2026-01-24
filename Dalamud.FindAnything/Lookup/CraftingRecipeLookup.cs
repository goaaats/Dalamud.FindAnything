using Dalamud.FindAnything.Modules;
using Dalamud.Interface.Textures;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Dalamud.FindAnything.Lookup;

public class CraftingRecipeLookup : ILookup {
    private static CraftingRecipesModule.CraftingItemMergedResult? _baseResult;

    public static void SetBaseResult(CraftingRecipesModule.CraftingItemMergedResult result) {
        _baseResult = result;
    }

    public string GetPlaceholder() {
        if (_baseResult is { } result) {
            return $"Choose recipe for \"{result.Name}\"...";
        }

        return "";
    }

    public LookupResult Lookup(SearchCriteria criteria) {
        var ctx = new SearchContext(criteria);
        var matcher = new FuzzyMatcher(criteria.MatchString, criteria.MatchMode);

        if (_baseResult is not { } baseResult) {
            return LookupResult.Empty;
        }

        var recipeDict = FindAnythingPlugin.SearchDatabase.GetAll<Recipe>();

        var group = baseResult.RecipeGroup;
        foreach (var recipe in group.Recipes) {
            var recipeEntry = recipeDict[recipe.RowId];

            var score = criteria.HasMatchString() ? matcher.Matches(GetSearchableCraftType(recipe.CraftType)) : 1;
            if (score > 0) {
                ctx.AddResult(new CraftingLookupResult {
                    Score = score,
                    Name = recipeEntry.Display,
                    Icon = FindAnythingPlugin.TexCache.GetIcon(group.Item.Icon),
                    Recipe = recipe,
                });
            }
        }

        return LookupResult.Bag(ctx.Results);
    }

    private string GetSearchableCraftType(RowRef<CraftType> craftType) {
        if (craftType.ValueNullable is { } ct) {
            return FindAnythingPlugin.Normalizer.Searchable(ct.Name);
        }
        return "";
    }

    private class CraftingLookupResult : ISearchResult {
        public string CatName => Recipe.CraftType.Value.Name.ToString();
        public required string Name { get; init; }
        public required ISharedImmediateTexture? Icon { get; init; }
        public required int Score { get; init; }
        public required Recipe Recipe { get; init; }

        public object Key => Recipe.RowId;

        public void Selected() {
            if (_baseResult is null)
                return;

            _baseResult.RecipeSelection = Recipe;
            _baseResult.Selected();
        }
    }
}
