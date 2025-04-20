using System.Collections.Generic;
using Dalamud.Plugin;

namespace Dalamud.FindAnything;

public class DalamudReflector
{
    public struct PluginEntry
    {
        public string Name { get; set; }
        public IExposedPlugin Plugin { get; set; }
        public bool HasConfigUi { get; set; }
        public bool HasMainUi { get; set; }

        public void OpenConfigUi()
        {
            Plugin.OpenConfigUi();
        }

        public void OpenMainUi()
        {
            Plugin.OpenMainUi();
        }
    }

    public IReadOnlyList<PluginEntry> OtherPlugins { get; private set; } = [];

    private DalamudReflector()
    {
        RefreshPlugins();
    }

    public void RefreshPlugins()
    {
        var list = new List<PluginEntry>();
        foreach (var plugin in FindAnythingPlugin.PluginInterface.InstalledPlugins) {
            if (plugin.Name == "Wotsit")
                continue;

            list.Add(new PluginEntry
            {
                Name = plugin.Name,
                Plugin = plugin,
                HasConfigUi = plugin.HasConfigUi,
                HasMainUi = plugin.HasMainUi
            });
        }

        OtherPlugins = list;
    }
    
    public static DalamudReflector Load() => new DalamudReflector();
}