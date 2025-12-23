using System.Collections.Generic;

namespace Dalamud.FindAnything;

public class SearchContext
{
    private const int MaxToSearch = 100;

    public readonly SearchCriteria Criteria;
    public readonly List<ISearchResult> Results = [];

    public SearchContext(SearchCriteria criteria) {
        Criteria = criteria;
    }

    public void AddResult(ISearchResult result) {
        Results.Add(result);
    }

    public int ResultCount => Results.Count;

    public bool OverLimit() {
        return Results.Count > MaxToSearch;
    }
}