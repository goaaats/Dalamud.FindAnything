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
        var recipeTable = FindAnythingPlugin.SearchDatabase.Recipe;
        foreach (var recipe in FindAnythingPlugin.GameStateCache.UnlockedRecipes) {
            var recipeEntry = recipeTable[recipe];
            var score = matcher.Matches(recipeEntry.Searchable);
            if (score > 0) {
                if (recipe.ItemResult.ValueNullable is { } itemResult) {
                    ctx.AddResult(new CraftingRecipeResult {
                        Score = score * Weight,
                        Recipe = recipe,
                        Item = itemResult,
                        Name = recipeEntry.Display,
                    });
                }
            }

            if (ctx.OverLimit()) break;
        }
    }

    private void SearchItems(SearchContext ctx, FuzzyMatcher matcher) {
        var craftableItemTable = FindAnythingPlugin.SearchDatabase.CraftableItem;
        foreach (var group in FindAnythingPlugin.GameStateCache.UnlockedRecipeGroups) {
            var craftableItemEntry = craftableItemTable[group.Item];
            var score = matcher.Matches(craftableItemEntry.Searchable);
            if (score > 0) {
                if (group.Recipes.Length == 1) {
                    ctx.AddResult(new CraftingItemSingleResult {
                        Score = score * Weight,
                        Recipe = group.Recipes[0],
                        Item = group.Item,
                        Name = craftableItemEntry.Display,
                    });

                } else {
                    ctx.AddResult(new CraftingItemMergedResult {
                        Score = score * Weight,
                        RecipeGroup = group,
                        Name = craftableItemEntry.Display,
                    });
                }
            }

            if (ctx.OverLimit()) break;
        }
    }

    private static unsafe void CraftingLogOpenRecipe(Recipe recipe) {
        AgentRecipeNote.Instance()->OpenRecipeByRecipeId(recipe.RowId);
    }

    private static unsafe void CraftingLogSearchRecipeItem(Recipe recipe) {
        if (recipe.ItemResult.ValueNullable is not { RowId: > 0 } itemResult)
            return;
        AgentRecipeNote.Instance()->SearchRecipeByItemId(itemResult.RowId);
    }

    private static unsafe void CraftingLogSearchItem(Item item) {
        if (item.RowId == 0)
            return;
        AgentRecipeNote.Instance()->SearchRecipeByItemId(item.RowId);
    }

    private static string GetCraftTypeName(Recipe recipe) {
        if (recipe.CraftType.ValueNullable is { } craftType) {
            return craftType.Name.ToText();
        }

        return "???";
    }

    private class CraftingRecipeResult : ISearchResult {
        public string CatName => field ??= $"Crafting Recipe ({GetCraftTypeName(Recipe)})";
        public required string Name { get; init; }
        public ISharedImmediateTexture Icon => field ??= FindAnythingPlugin.TexCache.GetIcon(Item.Icon);
        public required int Score { get; init; }
        public required Recipe Recipe { get; init; }
        public required Item Item { get; set; }

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
        public string CatName => field ??= $"Crafting Recipe ({GetCraftTypeName(Recipe)})";
        public required string Name { get; init; }
        public ISharedImmediateTexture Icon => field ??= FindAnythingPlugin.TexCache.GetIcon(Item.Icon);
        public required int Score { get; init; }
        public required Recipe Recipe { get; init; }
        public required Item Item { get; init; }

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
        public string CatName => field ??= RecipeSelection is { } recipe
            ? $"Crafting Recipe ({GetCraftTypeName(recipe)})"
            : "Crafting Recipes";
        public required string Name { get; init; }
        public ISharedImmediateTexture? Icon => field ??= FindAnythingPlugin.TexCache.GetIcon(RecipeGroup.Item.Icon);
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
