using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace Dalamud.FindAnything
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public SearchSetting ToSearch { get; set; } = SearchSetting.Aetheryte | SearchSetting.Duty | SearchSetting.MainCommand | SearchSetting.GeneralAction;

        [Flags]
        public enum SearchSetting : uint
        {
            None = 0,
            Duty = 1 << 0,
            Aetheryte = 1 << 1,
            MainCommand = 1 << 2,
            GeneralAction = 1 << 3,
        }

        // the below exist just to make saving less cumbersome

        [NonSerialized]
        private DalamudPluginInterface? pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface!.SavePluginConfig(this);
        }
    }
}