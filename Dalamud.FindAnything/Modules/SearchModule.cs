using System.Collections.Generic;

namespace Dalamud.FindAnything.Modules;

public abstract class SearchModule {
    public const int DefaultWeight = 100;

    public abstract Configuration.SearchSetting SearchSetting { get; }

    protected int Weight { get; private set; } = DefaultWeight;

    public void Configure(Configuration config) {
        Weight = config.SearchWeights.GetValueOrDefault(SearchSetting, DefaultWeight);
        Service.Log.Verbose($"Configured {GetType().Name} with weight {Weight}");
    }

    public abstract void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState);
}
