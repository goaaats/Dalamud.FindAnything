using Dalamud.FindAnything.Lookup;
using Dalamud.Interface.Textures;
using System;

namespace Dalamud.FindAnything.Modules;

public sealed class InternalModule : SearchModule
{
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.Internal;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        foreach (var kind in Enum.GetValues<InternalSearchResultKind>()) {
            var score = matcher.Matches(normalizer.SearchableAscii(GetNameForKind(kind)));
            if (score > 0) {
                ctx.AddResult(new InternalSearchResult {
                    Score = score * Weight,
                    Kind = kind,
                });
            }
        }
    }

    private class InternalSearchResult : ISearchResult
    {
        public string CatName => GetCatNameForKind(Kind);
        public string Name => GetNameForKind(Kind);

        public ISharedImmediateTexture Icon => Kind switch {
            InternalSearchResultKind.WikiMode => FindAnythingPlugin.TexCache.WikiIcon,
            _ => FindAnythingPlugin.TexCache.PluginInstallerIcon,
        };

        public required int Score { get; init; }
        public required InternalSearchResultKind Kind { get; init; }

        public object Key => (int)Kind;

        public bool CloseFinder => Kind != InternalSearchResultKind.WikiMode;

        public void Selected() {
            switch (Kind) {
                case InternalSearchResultKind.Settings:
                    FindAnythingPlugin.Instance.OpenSettings();
                    break;
                case InternalSearchResultKind.DalamudPlugins:
                    Service.CommandManager.ProcessCommand("/xlplugins");
                    break;
                case InternalSearchResultKind.DalamudSettings:
                    Service.CommandManager.ProcessCommand("/xlsettings");
                    break;
                case InternalSearchResultKind.WikiMode:
                    FindAnythingPlugin.Instance.SwitchLookupType(LookupType.Wiki);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public enum InternalSearchResultKind
    {
        Settings,
        DalamudPlugins,
        DalamudSettings,
        WikiMode,
    }

    public static string GetNameForKind(InternalSearchResultKind kind) => kind switch {
        InternalSearchResultKind.Settings => "Wotsit Settings",
        InternalSearchResultKind.DalamudPlugins => "Dalamud Plugin Installer",
        InternalSearchResultKind.DalamudSettings => "Dalamud Settings",
        InternalSearchResultKind.WikiMode => "Search in wikis...",
        _ => throw new ArgumentOutOfRangeException($"Unknown InternalSearchResultKind: {kind}"),
    };

    public static string GetCatNameForKind(InternalSearchResultKind kind) => kind switch {
        InternalSearchResultKind.Settings => "Wotsit",
        InternalSearchResultKind.DalamudPlugins => "Dalamud",
        InternalSearchResultKind.DalamudSettings => "Dalamud",
        InternalSearchResultKind.WikiMode => string.Empty,
        _ => throw new ArgumentOutOfRangeException($"Unknown InternalSearchResultKind: {kind}"),
    };
}