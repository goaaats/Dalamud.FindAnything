using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using Dalamud.Interface;
using Dalamud.Logging;
using Dalamud.Plugin;

namespace Dalamud.FindAnything;

public class DalamudReflector
{
    internal static T GetService<T>() => (T) typeof (IDalamudPlugin).Assembly.GetType("Dalamud.Service`1").MakeGenericType(typeof (T)).GetMethod("Get", BindingFlags.Static | BindingFlags.Public).Invoke((object) null, (object[]) null);
    
    internal static object GetService(string name) => typeof(IDalamudPlugin).Assembly.GetType("Dalamud.Service`1").MakeGenericType(typeof (IDalamudPlugin).Assembly.GetType(name)).GetMethod("Get", BindingFlags.Static | BindingFlags.Public).Invoke((object) null, (object[]) null);

    public struct PluginEntry
    {
        public string Name { get; set; }
        public UiBuilder UiBuilder { get; set; }

        public void OpenConfigUi()
        {
            UiBuilder.GetType().GetMethod("OpenConfig", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(UiBuilder, null);
        }
    }

    public IReadOnlyList<PluginEntry> OtherPlugins { get; private set; }

    private DalamudReflector()
    {
        RefreshPlugins();
    }

    public void RefreshPlugins()
    {
        var pluginMgr = GetService("Dalamud.Plugin.Internal.PluginManager");
        var mgrType = pluginMgr.GetType();
        var pluginList = mgrType.GetProperty("InstalledPlugins", BindingFlags.Public | BindingFlags.Instance).GetValue(pluginMgr);

        var list = new List<PluginEntry>();
        foreach (var item in (IEnumerable)pluginList)
        {
            var di = item.GetType().GetProperty("DalamudInterface", BindingFlags.Public | BindingFlags.Instance).GetValue(item);
            if (di == null)
                continue;
            var uib = di.GetType().GetProperty("UiBuilder", BindingFlags.Public | BindingFlags.Instance).GetValue(di);
            if (uib == null)
                continue;

            var configUiProp = uib.GetType().GetProperty("HasConfigUi", BindingFlags.NonPublic | BindingFlags.Instance);
            var hasConfigUi = (bool)configUiProp.GetValue(uib);
            if (!hasConfigUi)
                continue;

            var name = (string)item.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance)
                .GetValue(item);
            
            if (name == "Wotsit")
                continue;
            
            var entry = new PluginEntry()
            {
                Name =  name + " Settings",
                UiBuilder = uib as UiBuilder,
            };
            list.Add(entry);
        }

        OtherPlugins = list;
    }
    
    public static DalamudReflector Load() => new DalamudReflector();
}