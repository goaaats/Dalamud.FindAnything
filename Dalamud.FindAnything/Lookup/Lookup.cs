using System.Collections.Generic;

namespace Dalamud.FindAnything.Lookup;

public interface ILookup {
    void OnOpen() { }

    string GetPlaceholder();

    LookupResult Lookup(SearchCriteria criteria);

    void OnSelected(SearchCriteria criteria, ISearchResult result) { }
}

public record LookupResult(List<ISearchResult> Results, bool AllowSort) {
    public static LookupResult Bag(List<ISearchResult> results) => new(results, true);
    public static LookupResult List(List<ISearchResult> results) => new(results, false);
    public static LookupResult Empty { get; } = new([], false);
}
