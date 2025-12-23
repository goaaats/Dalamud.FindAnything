using Dalamud.Interface.Textures;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace Dalamud.FindAnything.Modules;

public sealed class CraftingRecipesModule : SearchModule
{
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.CraftingRecipes;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        foreach (var recipeSearch in FindAnythingPlugin.SearchDatabase.GetAll<Recipe>()) {
            var score = matcher.Matches(recipeSearch.Value.Searchable);
            if (score > 0) {
                var recipe = Service.Data.GetExcelSheet<Recipe>().GetRow(recipeSearch.Key);
                if (recipe.ItemResult.ValueNullable is { } itemResult) {
                    ctx.AddResult(new CraftingRecipeResult {
                        Score = score * Weight,
                        Recipe = recipe,
                        Name = recipeSearch.Value.Display,
                        CraftType = recipe.CraftType.Value,
                        Icon = FindAnythingPlugin.TexCache.GetIcon(itemResult.Icon),
                    });
                }
            }

            if (ctx.OverLimit()) break;
        }
    }

    private class CraftingRecipeResult : ISearchResult
    {
        public string CatName =>
            CraftType is { } craftType
                ? $"Crafting Recipe ({craftType.Name.ToText()})"
                : "Crafting Recipe";

        public required string Name { get; init; }
        public required ISharedImmediateTexture? Icon { get; init; }
        public required int Score { get; init; }
        public required Recipe Recipe { get; init; }
        public required CraftType? CraftType { get; init; }

        public object Key => Recipe.RowId;

        public unsafe void Selected() {
            if (FindAnythingPlugin.Configuration.OpenCraftingLogToRecipe) {
                AgentRecipeNote.Instance()->OpenRecipeByRecipeId(Recipe.RowId);
            } else {
                var id = ItemUtil.GetBaseId(Recipe.ItemResult.ValueNullable?.RowId ?? 0).ItemId;
                if (id > 0) {
                    AgentRecipeNote.Instance()->SearchRecipeByItemId(id);
                }
            }
        }
    }
}