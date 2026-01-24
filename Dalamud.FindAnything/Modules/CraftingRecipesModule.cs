using Dalamud.FindAnything.Lookup;
using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace Dalamud.FindAnything.Modules;

public sealed class CraftingRecipesModule : SearchModule {
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.CraftingRecipes;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        if (FindAnythingPlugin.Configuration.CraftingMergeItems) {
            SearchItems(ctx, matcher);
        } else {
            SearchRecipes(ctx, matcher);
        }
    }

    private void SearchRecipes(SearchContext ctx, FuzzyMatcher matcher) {
        var recipeDict = FindAnythingPlugin.SearchDatabase.GetAll<Recipe>();
        foreach (var recipe in FindAnythingPlugin.GameStateCache.UnlockedRecipes) {
            var recipeEntry = recipeDict[recipe.RowId];
            var score = matcher.Matches(recipeEntry.Searchable);
            if (score > 0) {
                if (recipe.ItemResult.ValueNullable is { } itemResult) {
                    ctx.AddResult(new CraftingRecipeResult {
                        Score = score * Weight,
                        Recipe = recipe,
                        Name = recipeEntry.Display,
                        Icon = FindAnythingPlugin.TexCache.GetIcon(itemResult.Icon),
                    });
                }
            }

            if (ctx.OverLimit()) break;
        }
    }

    private void SearchItems(SearchContext ctx, FuzzyMatcher matcher) {
        var itemDict = FindAnythingPlugin.SearchDatabase.GetAll<Item>();
        foreach (var group in FindAnythingPlugin.GameStateCache.UnlockedRecipeGroups) {
            var itemEntry = itemDict[group.Item.RowId];
            var score = matcher.Matches(itemEntry.Searchable);
            if (score > 0) {
                if (group.Recipes.Length == 1) {
                    ctx.AddResult(new CraftingItemSingleResult {
                        Score = score * Weight,
                        Recipe = group.Recipes[0],
                        Name = itemEntry.Display,
                        Icon = FindAnythingPlugin.TexCache.GetIcon(group.Item.Icon),
                    });

                } else {
                    ctx.AddResult(new CraftingItemMergedResult {
                        Score = score * Weight,
                        RecipeGroup = group,
                        Name = itemEntry.Display,
                        Icon = FindAnythingPlugin.TexCache.GetIcon(group.Item.Icon),
                    });
                }
            }

            if (ctx.OverLimit()) break;
        }
    }

    public static unsafe void CraftingLogOpenRecipe(Recipe recipe) {
        AgentRecipeNote.Instance()->OpenRecipeByRecipeId(recipe.RowId);
    }

    public static unsafe void CraftingLogSearchRecipeItem(Recipe recipe) {
        if (recipe.ItemResult.ValueNullable is not { RowId: > 0 } itemResult)
            return;
        AgentRecipeNote.Instance()->SearchRecipeByItemId(itemResult.RowId);
    }

    public static unsafe void CraftingLogSearchItem(Item item) {
        if (item.RowId == 0)
            return;
        AgentRecipeNote.Instance()->SearchRecipeByItemId(item.RowId);
    }

    public static string GetCraftTypeName(Recipe recipe) {
        if (recipe.CraftType.ValueNullable is { } craftType) {
            return craftType.Name.ToText();
        }

        return "???";
    }

    private class CraftingRecipeResult : ISearchResult {
        public string CatName => $"Crafting Recipe ({GetCraftTypeName(Recipe)})";
        public required string Name { get; init; }
        public required ISharedImmediateTexture? Icon { get; init; }
        public required int Score { get; init; }
        public required Recipe Recipe { get; init; }

        public object Key => Recipe.RowId;

        public void Selected() {
            var mode = FindAnythingPlugin.Configuration.CraftingRecipeSelect;
            if (mode == Configuration.CraftingSingleSelectAction.OpenInLog) {
                CraftingLogOpenRecipe(Recipe);
            } else if (mode == Configuration.CraftingSingleSelectAction.SearchInLog) {
                CraftingLogSearchRecipeItem(Recipe);
            }
        }
    }

    public class CraftingItemSingleResult : ISearchResult {
        public string CatName => $"Crafting Recipe ({GetCraftTypeName(Recipe)})";
        public required string Name { get; init; }
        public required ISharedImmediateTexture? Icon { get; init; }
        public required int Score { get; init; }
        public required Recipe Recipe { get; init; }

        public object Key => Recipe.RowId;

        public void Selected() {
            var mode = FindAnythingPlugin.Configuration.CraftingItemSelectSingle;
            if (mode == Configuration.CraftingSingleSelectAction.OpenInLog) {
                CraftingLogOpenRecipe(Recipe);
            } else if (mode == Configuration.CraftingSingleSelectAction.SearchInLog) {
                CraftingLogSearchRecipeItem(Recipe);
            }
        }
    }

    public class CraftingItemMergedResult : ISearchResult {
        public string CatName => RecipeSelection is { } recipe
            ? $"Crafting Recipe ({GetCraftTypeName(recipe)})"
            : "Crafting Recipes";
        public required string Name { get; init; }
        public required ISharedImmediateTexture? Icon { get; init; }
        public required int Score { get; init; }
        public required RecipeGroup RecipeGroup { get; init; }

        public object Key => RecipeGroup.Item.RowId;

        public bool CloseFinder => RecipeSelection is not null
                                   || FindAnythingPlugin.Configuration.CraftingItemSelectMerged == Configuration.CraftingMergedSelectAction.SearchInLog;

        public Recipe? RecipeSelection { get; set; }

        public void Selected() {
            if (RecipeSelection is { } recipe) {
                CraftingLogOpenRecipe(recipe);
                return;
            }

            var mode = FindAnythingPlugin.Configuration.CraftingItemSelectMerged;
            if (mode == Configuration.CraftingMergedSelectAction.SearchInLog) {
                CraftingLogSearchItem(RecipeGroup.Item);
            } else if (mode == Configuration.CraftingMergedSelectAction.SearchInFinder) {
                CraftingRecipeLookup.SetBaseResult(this);
                FindAnythingPlugin.Instance.SwitchLookupType(LookupType.CraftingRecipe);
            }
        }
    }
}
