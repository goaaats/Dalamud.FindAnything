using Dalamud.Interface.Textures;
using Dalamud.Utility;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Dalamud.FindAnything.Lookup;

public class WikiLookup : ILookup
{
    private readonly History history = new();

    public string GetPlaceholder() => "Search in wikis...";

    public LookupResult Lookup(SearchCriteria criteria) {
        if (!criteria.HasMatchString()) {
            return LookupResult.List(history.GetHistory());
        }

        var ctx = new SearchContext(criteria);
        var matcher = new FuzzyMatcher(criteria.MatchString, criteria.MatchMode);

        {
            var score = matcher.Matches("here");
            if (score > 0) {
                var terriContent = Service.Data.GetExcelSheet<ContentFinderCondition>()
                    .FirstOrNull(x => x.TerritoryType.RowId == Service.ClientState.TerritoryType);
                if (terriContent != null) {
                    ctx.AddResult(new WikiDataResult {
                        Score = int.MaxValue,
                        Name = terriContent.Value.Name.ToText(),
                        DataKey = terriContent.Value.RowId,
                        CatName = "Current Duty",
                        DataCategory = WikiDataResult.Category.Instance,
                    });
                }
            }
        }

        ctx.AddResult(new WikiTextResult {
            Score = int.MaxValue,
            Query = criteria.SemanticString,
        });

        foreach (var cfc in FindAnythingPlugin.SearchDatabase.GetAll<ContentFinderCondition>()) {
            if (FindAnythingPlugin.Configuration.WikiModeNoSpoilers && !FindAnythingPlugin.GameStateCache.UnlockedDutyKeys.Contains(cfc.Key))
                continue;

            var score = matcher.Matches(cfc.Value.Searchable);
            if (score > 0)
                ctx.AddResult(new WikiDataResult {
                    Score = score,
                    Name = cfc.Value.Display,
                    DataKey = cfc.Key,
                    CatName = "Duty",
                    DataCategory = WikiDataResult.Category.Instance,
                });

            if (ctx.OverLimit()) break;
        }

        foreach (var quest in FindAnythingPlugin.SearchDatabase.GetAll<Quest>()) {
            var score = matcher.Matches(quest.Value.Searchable);
            if (score > 0)
                ctx.AddResult(new WikiDataResult {
                    Score = score,
                    Name = quest.Value.Display,
                    DataKey = quest.Key,
                    CatName = "Quest",
                    DataCategory = WikiDataResult.Category.Quest,
                });

            if (ctx.OverLimit()) break;
        }

        foreach (var item in FindAnythingPlugin.SearchDatabase.GetAll<Item>()) {
            var score = matcher.Matches(item.Value.Searchable);
            if (score > 0)
                ctx.AddResult(new WikiDataResult {
                    Score = score,
                    Name = item.Value.Display,
                    DataKey = item.Key,
                    CatName = "Item",
                    DataCategory = WikiDataResult.Category.Item,
                });

            if (ctx.OverLimit()) break;
        }

        return LookupResult.Bag(ctx.Results);
    }

    public void OnSelected(SearchCriteria criteria, ISearchResult result) {
        history.Add(LookupType.Wiki, criteria, result);
    }

    public class WikiDataResult : ISearchResult
    {
        public enum Category
        {
            Instance,
            Quest,
            Item,
        }

        public required string CatName { get; init; }
        public required string Name { get; init; }
        public ISharedImmediateTexture Icon => FindAnythingPlugin.TexCache.WikiIcon;
        public required int Score { get; init; }
        public required uint DataKey { get; init; }
        public required Category DataCategory { get; init; }

        public object Key => (DataKey, (int)DataCategory);

        public bool CloseFinder => false;

        public void Selected() {
            WikiSiteLookup.SetBaseResult(this);
            FindAnythingPlugin.Instance.SwitchLookupType(LookupType.WikiSite);
        }
    }

    public class WikiTextResult : ISearchResult
    {
        public string CatName => string.Empty;
        public string Name => $"Search for \"{Query}\" in wikis...";
        public ISharedImmediateTexture Icon => FindAnythingPlugin.TexCache.WikiIcon;
        public required int Score { get; init; }
        public required string Query { get; init; }

        public object Key => Query;

        public void Selected() {
            Util.OpenLink($"https://ffxiv.gamerescape.com/w/index.php?search={HttpUtility.UrlEncode(Query)}&title=Special%3ASearch&fulltext=1&useskin=Vector");
        }
    }
}