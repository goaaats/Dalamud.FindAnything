namespace Dalamud.FindAnything;

internal class SearchState
{
    private SearchMode BaseSearchMode { get; set; } = SearchMode.Top;
    public SearchMode ActualSearchMode { get; private set; } = SearchMode.Top;
    private MatchMode MatchMode { get; set; } = MatchMode.Simple;
    public string RawString { get; private set; } = string.Empty;
    private string CleanString { get; set; } = string.Empty;
    private string SemanticString { get; set; } = string.Empty;
    private string MatchString { get; set; } = string.Empty;
    private bool ContainsKana { get; set; } = false;

    private readonly Configuration config;

    public SearchState(Configuration configuration)
    {
        config = configuration;
    }

    public void Reset()
    {
        BaseSearchMode = SearchMode.Top;
        ActualSearchMode = SearchMode.Top;
        MatchMode = config.MatchMode;
        RawString = string.Empty;
        CleanString = string.Empty;
        SemanticString = string.Empty;
        MatchString = string.Empty;
        ContainsKana = false;
    }

    public void SetBaseSearchModeAndTerm(SearchMode searchMode, string term)
    {
        if (searchMode != BaseSearchMode)
        {
            BaseSearchMode = searchMode;
            SetTerm(term);
        }
    }

    public void SetBaseSearchMode(SearchMode searchMode)
    {
        if (searchMode != BaseSearchMode)
        {
            BaseSearchMode = searchMode;
            SetTerm(RawString);
        }
    }

    public void SetTerm(string term)
    {
        ActualSearchMode = BaseSearchMode;

        if (term.Length == 0)
        {
            // Skip more complex initialization if we know the term is empty
            MatchMode = config.MatchMode;
            RawString = string.Empty;
            CleanString = string.Empty;
            SemanticString = string.Empty;
            MatchString = string.Empty;
            ContainsKana = false;
            return;
        }

        RawString = term;

        term = term.Trim();
        CleanString = term;

        // Only recognize the wiki sigil when we're in top mode.
        if (BaseSearchMode == SearchMode.Top && term.StartsWith(FindAnythingPlugin.ModeSigilWiki))
        {
            term = term[1..];
            ActualSearchMode = SearchMode.Wiki;
        }

        var matchMode = config.MatchMode;
        if (!string.IsNullOrWhiteSpace(config.MatchSigilFuzzyParts) && term.StartsWith(config.MatchSigilFuzzyParts))
        {
            matchMode = MatchMode.FuzzyParts;
            term = term[1..];
        }
        else if (!string.IsNullOrWhiteSpace(config.MatchSigilFuzzy) && term.StartsWith(config.MatchSigilFuzzy))
        {
            matchMode = MatchMode.Fuzzy;
            term = term[1..];
        }
        else if (!string.IsNullOrWhiteSpace(config.MatchSigilSimple) && term.StartsWith(config.MatchSigilSimple))
        {
            matchMode = MatchMode.Simple;
            term = term[1..];
        }

        SemanticString = term;

        term = term.ToLower().Replace("'", string.Empty);
        if (term.ContainsKana())
        {
            term = term.Downcase(normalizeKana: true).Replace("'", string.Empty);
            ContainsKana = true;
        }
        else
        {
            term = term.ToLowerInvariant().Replace("'", string.Empty);
            ContainsKana = false;
        }
        
        MatchString = term;

        MatchMode = matchMode;
    }

    public SearchCriteria CreateCriteria()
    {
        return new SearchCriteria(ActualSearchMode, MatchMode, CleanString, SemanticString, MatchString, ContainsKana);
    }
}

public enum SearchMode
{
    Top,
    Wiki,
    WikiSiteChoicer,
    EmoteModeChoicer
}

public class SearchCriteria
{
    public SearchMode SearchMode { get; }
    public MatchMode MatchMode { get; }
    public string CleanString { get; }
    public string SemanticString { get; }
    public string MatchString { get; }
    public bool ContainsKana { get; }

    public SearchCriteria(SearchMode searchMode, MatchMode matchMode, string cleanString, string semanticString,
        string matchString, bool containsKana)
    {
        SearchMode = searchMode;
        MatchMode = matchMode;
        CleanString = cleanString;
        SemanticString = semanticString;
        MatchString = matchString;
        ContainsKana = containsKana;
    }

    public bool HasMatchString()
    {
        return MatchString.Length != 0;
    }
}
