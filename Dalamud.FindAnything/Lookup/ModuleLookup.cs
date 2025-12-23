using Dalamud.FindAnything.Modules;
using System.Collections.Generic;
using System.Linq;

namespace Dalamud.FindAnything.Lookup;

public sealed class ModuleLookup : ILookup
{
    private readonly SearchModule[] configurableModules;
    private readonly SearchModule[] fixedModules;
    private readonly SearchModule[] allModules;
    private readonly History history = new();

    private SearchModule[] activeModules = [];
    private ISearchResult? currentHint;

    public ModuleLookup() {
        configurableModules = [
            new DutyModule(),
            new AetheryteModule(),
            new MainCommandModule(),
            new ExtraCommandModule(),
            new GeneralActionModule(),
            new EmoteModule(),
            new PluginSettingsModule(),
            new GearsetsModule(),
            new CraftingRecipesModule(),
            new GatheringItemsModule(),
            new MountsModule(),
            new MinionsModule(),
            new FashionAccessoriesModule(),
            new CollectionModule(),
            new MacroLinksModule(),
            new InternalModule(),
            new MathsModule(),
        ];

        fixedModules = [
            new ChatCommandModule(),
        ];

        allModules = configurableModules.Concat(fixedModules).ToArray();

        Configure(FindAnythingPlugin.Configuration);
        FindAnythingPlugin.ConfigManager.OnChange += Configure;
    }

    private void Configure(Configuration config) {
        Service.Log.Debug($"Configuring {nameof(ModuleLookup)}");

        foreach (var module in allModules) {
            module.Configure(config);
        }

        activeModules = new List<SearchModule>(configurableModules)
            .Where(x => config.Order.Contains(x.SearchSetting))
            .OrderBy(x => config.Order.IndexOf(x.SearchSetting))
            .Concat(fixedModules)
            .ToArray();
    }

    public void OnOpen() {
        if (HintProvider.NextHint() is { } hint) {
            currentHint = hint;
        } else {
            currentHint = null;
        }
    }

    public string GetPlaceholder() => "Type to search...";

    public LookupResult Lookup(SearchCriteria criteria) {
        if (!criteria.HasMatchString()) {
            return LookupResult.List(currentHint is not null ? [currentHint] : history.GetHistory());
        }

        var ctx = new SearchContext(criteria);
        var normalizer = FindAnythingPlugin.Normalizer.WithKana(criteria.ContainsKana);
        var matcher = new FuzzyMatcher(criteria.MatchString, criteria.MatchMode);
        var gameState = new GameState();

        foreach (var module in activeModules) {
            module.Search(ctx, normalizer, matcher, gameState);
        }

        return LookupResult.Bag(ctx.Results);
    }

    public void OnSelected(SearchCriteria criteria, ISearchResult result) {
        history.Add(LookupType.Module, criteria, result);
    }
}