using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ImGuiNET;

namespace Dalamud.FindAnything;

public class SettingsWindow : Window
{
    private readonly FindAnythingPlugin plugin;
    private uint flags;

    public SettingsWindow(FindAnythingPlugin plugin) : base("Wotsit Settings", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize)
    {
        this.plugin = plugin;
    }

    public override void OnOpen()
    {
        flags = (uint) plugin.Configuration.ToSearch;
        base.OnOpen();
    }

    public override void Draw()
    {
        ImGui.CheckboxFlags("Search in Duties", ref this.flags, (uint) Configuration.SearchSetting.Duty);
        ImGui.CheckboxFlags("Search in Commands", ref this.flags, (uint) Configuration.SearchSetting.MainCommand);
        ImGui.CheckboxFlags("Search in Aetherytes", ref this.flags, (uint) Configuration.SearchSetting.Aetheryte);
        ImGui.CheckboxFlags("Search in General Actions", ref this.flags, (uint) Configuration.SearchSetting.GeneralAction);

        ImGuiHelpers.ScaledDummy(30);

        if (ImGui.Button("Save"))
        {
            plugin.Configuration.ToSearch = (Configuration.SearchSetting) this.flags;
            plugin.Configuration.Save();
            IsOpen = false;
        }

        ImGui.SameLine();

        if (ImGui.Button("Discard"))
        {
            IsOpen = false;
        }
    }
}