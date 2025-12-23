using Dalamud.Interface.Textures;
using Dalamud.Plugin;

namespace Dalamud.FindAnything.Modules;

public sealed class PluginSettingsModule : SearchModule
{
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.PluginSettings;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        foreach (var plugin in Service.PluginInterface.InstalledPlugins) {
            if (plugin.Name == "Wotsit")
                continue;

            if (plugin.HasMainUi) {
                var name = $"Open {plugin.Name} Interface";
                var score = matcher.Matches(normalizer.Searchable(name));

                if (score > 0) {
                    ctx.AddResult(new PluginInterfaceSearchResult {
                        Score = score * Weight,
                        Name = name,
                        Plugin = plugin,
                    });
                }
            }

            if (plugin.HasConfigUi) {
                var name = $"Open {plugin.Name} Settings";
                var score = matcher.Matches(normalizer.Searchable(name));

                if (score > 0) {
                    ctx.AddResult(new PluginSettingsSearchResult {
                        Score = score * Weight,
                        Name = name,
                        Plugin = plugin,
                    });
                }
            }
        }

        foreach (var plugin in FindAnythingPlugin.Ipc.TrackedIpcs) {
            foreach (var ipcBinding in plugin.Value) {
                var score = matcher.Matches(ipcBinding.Search);
                if (score > 0) {
                    ctx.AddResult(new IpcSearchResult {
                        Score = score * Weight,
                        CatName = plugin.Key,
                        Name = ipcBinding.Display,
                        Guid = ipcBinding.Guid,
                        Icon = FindAnythingPlugin.TexCache.GetIcon(ipcBinding.IconId),
                    });
                }

                // Limit IPC results to 25
                if (ctx.ResultCount > 25) break;
            }
        }
    }

    private class PluginSettingsSearchResult : ISearchResult
    {
        public string CatName => "Other Plugins";
        public required string Name { get; init; }
        public ISharedImmediateTexture Icon => FindAnythingPlugin.TexCache.PluginInstallerIcon;
        public required int Score { get; init; }
        public required IExposedPlugin Plugin { get; init; }

        public object Key => Plugin.Name;

        public void Selected() {
            Plugin.OpenConfigUi();
        }
    }

    private class PluginInterfaceSearchResult : ISearchResult
    {
        public string CatName => "Other Plugins";
        public required string Name { get; init; }
        public ISharedImmediateTexture Icon => FindAnythingPlugin.TexCache.PluginInstallerIcon;
        public required int Score { get; init; }
        public required IExposedPlugin Plugin { get; init; }

        public object Key => Plugin.Name;

        public void Selected() {
            Plugin.OpenMainUi();
        }
    }

    private class IpcSearchResult : ISearchResult
    {
        public required string CatName { get; init; }
        public required string Name { get; init; }
        public required ISharedImmediateTexture? Icon { get; init; }
        public required int Score { get; init; }
        public required string Guid { get; init; }

        public object Key => Guid;

        public void Selected() {
            FindAnythingPlugin.Ipc.Invoke(Guid);
        }
    }
}