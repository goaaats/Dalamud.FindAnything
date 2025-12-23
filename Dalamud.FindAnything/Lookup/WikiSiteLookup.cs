using Dalamud.Interface.Textures;
using Dalamud.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Web;

namespace Dalamud.FindAnything.Lookup;

public class WikiSiteLookup : ILookup
{
    private static ISearchResult? _baseResult;
    private static bool _teamcraftLocalFailed;

    public static void SetBaseResult(ISearchResult result) {
        _baseResult = result;
    }

    public string GetPlaceholder() {
        return _baseResult switch {
            WikiLookup.WikiDataResult res => $"Choose site for \"{res.Name}\"...",
            WikiLookup.WikiTextResult searchRes => $"Choose site for \"{searchRes.Name}\"...",
            _ => "",
        };
    }

    public LookupResult Lookup(SearchCriteria criteria) {
        var ctx = new SearchContext(criteria);
        var normalizer = FindAnythingPlugin.Normalizer.WithKana(criteria.ContainsKana);
        var matcher = new FuzzyMatcher(criteria.MatchString, criteria.MatchMode);

        if (_baseResult is not WikiLookup.WikiDataResult wikiResult) {
            return LookupResult.Empty;
        }

        if (criteria.HasMatchString()) {
            foreach (var kind in Enum.GetValues<WikiSiteChoicerResult.SiteChoice>()) {
                if (kind == WikiSiteChoicerResult.SiteChoice.TeamCraft && wikiResult.DataCategory == WikiLookup.WikiDataResult.Category.Item)
                    continue;

                var score = matcher.Matches(normalizer.SearchableAscii(kind.ToString()));
                if (score > 0) {
                    ctx.AddResult(new WikiSiteChoicerResult {
                        Score = score,
                        Site = kind,
                    });
                }
            }
        }

        if (ctx.ResultCount == 0) {
            ctx.AddResult(new WikiSiteChoicerResult {
                Score = 1,
                Site = WikiSiteChoicerResult.SiteChoice.GamerEscape,
            });

            ctx.AddResult(new WikiSiteChoicerResult {
                Score = 1,
                Site = WikiSiteChoicerResult.SiteChoice.ConsoleGamesWiki,
            });

            ctx.AddResult(new WikiSiteChoicerResult {
                Score = 1,
                Site = WikiSiteChoicerResult.SiteChoice.GarlandTools,
            });

            if (wikiResult.DataCategory == WikiLookup.WikiDataResult.Category.Item) {
                ctx.AddResult(new WikiSiteChoicerResult {
                    Score = 1,
                    Site = WikiSiteChoicerResult.SiteChoice.TeamCraft,
                });
            }
        }

        return LookupResult.Bag(ctx.Results);
    }

    private class WikiSiteChoicerResult : ISearchResult
    {
        public enum SiteChoice
        {
            GamerEscape,
            GarlandTools,
            ConsoleGamesWiki,
            TeamCraft,
        }

        public string CatName => string.Empty;
        public string Name => $"Open on {Site}";
        public ISharedImmediateTexture Icon => FindAnythingPlugin.TexCache.WikiIcon;
        public required int Score { get; init; }
        public required SiteChoice Site { get; init; }

        public object Key => (int)Site;

        private void OpenWikiPage(string input, SiteChoice choice) {
            var name = input.Replace(' ', '_');
            name = name.Replace('–', '-');

            if (name.StartsWith("_") || name.StartsWith("_")) // "level sync" or "job lock" icon
                name = name[2..];

            switch (choice) {
                case SiteChoice.GamerEscape:
                    Util.OpenLink($"https://ffxiv.gamerescape.com/wiki/{HttpUtility.UrlEncode(name)}?useskin=Vector");
                    break;
                case SiteChoice.ConsoleGamesWiki:
                    Util.OpenLink($"https://ffxiv.consolegameswiki.com/wiki/{HttpUtility.UrlEncode(name)}");
                    break;
                case SiteChoice.GarlandTools:
                case SiteChoice.TeamCraft:
                default:
                    throw new ArgumentOutOfRangeException($"Invalid site choice for {nameof(OpenWikiPage)}: {choice}");
            }
        }

        private void OpenTeamcraft(uint id) {
            if (_teamcraftLocalFailed || FindAnythingPlugin.Configuration.TeamCraftForceBrowser) {
                Util.OpenLink($"https://ffxivteamcraft.com/db/en/item/{id}");
                return;
            }

            Task.Run(() => {
                try {
                    var wr = WebRequest.CreateHttp($"http://localhost:14500/db/en/item/{id}");
                    wr.Timeout = 500;
                    wr.Method = "GET";
                    wr.GetResponse().Close();
                } catch {
                    try {
                        if (Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ffxiv-teamcraft"))) {
                            Util.OpenLink($"teamcraft:///db/en/item/{id}");
                        } else {
                            _teamcraftLocalFailed = true;
                            Util.OpenLink($"https://ffxivteamcraft.com/db/en/item/{id}");
                        }
                    } catch {
                        _teamcraftLocalFailed = true;
                        Util.OpenLink($"https://ffxivteamcraft.com/db/en/item/{id}");
                    }
                }
            });
        }

        public void Selected() {
            if (_baseResult is not WikiLookup.WikiDataResult wikiResult) {
                throw new Exception($"{nameof(_baseResult)} was not of type {nameof(WikiLookup.WikiDataResult)}, was: {_baseResult?.GetType().FullName}");
            }

            switch (Site) {
                case SiteChoice.ConsoleGamesWiki:
                case SiteChoice.GamerEscape:
                    OpenWikiPage(wikiResult.Name, Site);
                    break;
                case SiteChoice.GarlandTools:
                    switch (wikiResult.DataCategory) {
                        case WikiLookup.WikiDataResult.Category.Instance:
                            Util.OpenLink($"https://garlandtools.org/db/#instance/{wikiResult.DataKey}");
                            break;
                        case WikiLookup.WikiDataResult.Category.Quest:
                            Util.OpenLink($"https://garlandtools.org/db/#quest/{wikiResult.DataKey}");
                            break;
                        case WikiLookup.WikiDataResult.Category.Item:
                            Util.OpenLink($"https://garlandtools.org/db/#item/{wikiResult.DataKey}");
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    break;
                case SiteChoice.TeamCraft:
                    OpenTeamcraft(wikiResult.DataKey);
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Invalid site choice for ${nameof(Selected)}: {Site}");
            }
        }
    }
}