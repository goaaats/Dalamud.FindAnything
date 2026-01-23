using System;

namespace Dalamud.FindAnything;

public class ConfigManager {
    public readonly Configuration Config;
    public event Action<Configuration>? OnChange;

    public ConfigManager() {
        Config = Service.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Initialize();
    }

    public void Save() {
        Service.PluginInterface.SavePluginConfig(Config);
    }

    public void Notify() {
        Service.Log.Debug("Notifying subscribers of config change");
        OnChange?.Invoke(Config);
    }

    public void SaveAndNotify() {
        Save();
        Notify();
    }
}
