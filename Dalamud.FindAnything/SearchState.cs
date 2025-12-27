using Dalamud.FindAnything.Lookup;

namespace Dalamud.FindAnything;

// Notes on the various string fields:
// - RawString: the actual contents of the ImGui search box buffer
// - CleanString: the above but with whitespace stripped
// - SemanticString: the above but with any sigils removed
// - MatchString: the above but in lower case and with kana and some special characters normalized
public class SearchState
{
    private const string ModeSigilWiki = "?";

    private LookupType? OverrideLookupType { get; set; }
    private MatchMode MatchMode { get; set; } = MatchMode.Simple;
    public string RawString { get; private set; } = string.Empty;
    private string CleanString { get; set; } = string.Empty;
    private string SemanticString { get; set; } = string.Empty;
    private string MatchString { get; set; } = string.Empty;
    private bool ContainsKana { get; set; }

    private readonly Configuration config;
    private readonly Normalizer normalizer;

    public SearchState(Configuration config, Normalizer normalizer) {
        this.config = config;
        this.normalizer = normalizer;
    }

    public void Reset() {
        OverrideLookupType = null;
        MatchMode = config.MatchMode;
        RawString = string.Empty;
        CleanString = string.Empty;
        SemanticString = string.Empty;
        MatchString = string.Empty;
        ContainsKana = false;
    }

    public void Set(LookupType currentLookupType, string term) {
        if (term.Length == 0) {
            // Skip more complex initialization if we know the term is empty
            OverrideLookupType = null;
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

        // Only recognize the wiki sigil when we're in module mode.
        if (currentLookupType == LookupType.Module && term.StartsWith(ModeSigilWiki)) {
            term = term[1..];
            OverrideLookupType = LookupType.Wiki;
        } else {
            OverrideLookupType = null;
        }

        var matchMode = config.MatchMode;
        if (!string.IsNullOrWhiteSpace(config.MatchSigilFuzzyParts) && term.StartsWith(config.MatchSigilFuzzyParts)) {
            matchMode = MatchMode.FuzzyParts;
            term = term[1..];
        } else if (!string.IsNullOrWhiteSpace(config.MatchSigilFuzzy) && term.StartsWith(config.MatchSigilFuzzy)) {
            matchMode = MatchMode.Fuzzy;
            term = term[1..];
        } else if (!string.IsNullOrWhiteSpace(config.MatchSigilSimple) && term.StartsWith(config.MatchSigilSimple)) {
            matchMode = MatchMode.Simple;
            term = term[1..];
        }

        SemanticString = term;

        ContainsKana = term.ContainsKana();
        term = normalizer.WithKana(ContainsKana).Searchable(term);

        MatchString = term;

        MatchMode = matchMode;
    }

    public SearchCriteria Criteria() {
        return new SearchCriteria(OverrideLookupType, MatchMode, CleanString, SemanticString, MatchString, ContainsKana);
    }
}

public record SearchCriteria(
    LookupType? OverrideLookupType,
    MatchMode MatchMode,
    string CleanString,
    string SemanticString,
    string MatchString,
    bool ContainsKana)
{
    public bool HasMatchString() {
        return MatchString.Length != 0;
    }
}