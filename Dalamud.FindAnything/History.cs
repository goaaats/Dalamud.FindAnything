using Dalamud.FindAnything.Lookup;
using System.Collections.Generic;
using System.Linq;

namespace Dalamud.FindAnything;

public class History {
    private const int HistoryMax = 5;

    private static bool _inReplay;

    private List<HistoryEntry> history = new(HistoryMax);

    private struct HistoryEntry {
        public LookupType LookupType;
        public SearchCriteria SearchCriteria;
        public ISearchResult Result;
    }

    private List<ISearchResult> Replay(HistoryEntry entry) {
        try {
            _inReplay = true;
            return FindAnythingPlugin.RootLookup.GetLookupForType(entry.LookupType)
                .Lookup(entry.SearchCriteria)
                .Results;
        } finally {
            _inReplay = false;
        }
    }

    public List<ISearchResult> GetHistory() {
        if (_inReplay)
            return [];

        if (!FindAnythingPlugin.Configuration.HistoryEnabled || history.Count == 0)
            return [];

        var newHistory = new List<HistoryEntry>();
        var results = new List<ISearchResult>();

        Service.Log.Verbose("{Num} histories:", history.Count);

        foreach (var entry in history) {
            if (Replay(entry).Contains(entry.Result, SearchResultComparer.Instance)) {
                newHistory.Add(entry);
                results.Add(entry.Result);
            } else {
                Service.Log.Verbose("Couldn't find {Term} anymore, removing from history", entry.SearchCriteria.MatchString);
            }
        }

        history = newHistory;
        return results;
    }

    public void Add(LookupType lookupType, SearchCriteria searchCriteria, ISearchResult result) {
        var index = history.FindIndex(h => SearchResultComparer.Instance.Equals(result, h.Result));
        if (index != -1) {
            var oldEntry = history[index];
            history.RemoveAt(index);
            history.Insert(0, oldEntry with {
                Result = result, // We update just the result in case it was altered by a lookup
            });
        } else {
            history.Insert(0, new HistoryEntry {
                LookupType = lookupType,
                SearchCriteria = searchCriteria,
                Result = result,
            });
        }

        if (history.Count > HistoryMax) {
            history.RemoveAt(history.Count - 1);
        }
    }
}
