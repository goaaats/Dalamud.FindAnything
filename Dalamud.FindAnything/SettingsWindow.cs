using System;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ImGuiNET;

namespace Dalamud.FindAnything;

public class SettingsWindow : Window
{
    private readonly FindAnythingPlugin plugin;
    private uint flags;
    private Configuration.OpenMode openMode;
    private VirtualKey shiftShiftKey;
    private VirtualKey comboModifierKey;
    private VirtualKey comboKey;

    public SettingsWindow(FindAnythingPlugin plugin) : base("Wotsit Settings", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize)
    {
        this.plugin = plugin;
    }

    public override void OnOpen()
    {
        flags = (uint) plugin.Configuration.ToSearch;
        openMode = plugin.Configuration.Open;
        shiftShiftKey = plugin.Configuration.ShiftShiftKey;
        comboKey = plugin.Configuration.ComboKey;
        comboModifierKey = plugin.Configuration.ComboModifier;
        base.OnOpen();
    }

    public override void Draw()
    {
        ImGui.TextColored(ImGuiColors.DalamudGrey, "What to search");
        ImGui.CheckboxFlags("Search in Duties", ref this.flags, (uint) Configuration.SearchSetting.Duty);
        ImGui.CheckboxFlags("Search in Commands", ref this.flags, (uint) Configuration.SearchSetting.MainCommand);
        ImGui.CheckboxFlags("Search in Aetherytes", ref this.flags, (uint) Configuration.SearchSetting.Aetheryte);
        ImGui.CheckboxFlags("Search in General Actions", ref this.flags, (uint) Configuration.SearchSetting.GeneralAction);

        ImGuiHelpers.ScaledDummy(30);

        ImGui.TextColored(ImGuiColors.DalamudGrey, "How to open");

        if (ImGui.RadioButton("Keyboard Combo", openMode == Configuration.OpenMode.Combo))
        {
            openMode = Configuration.OpenMode.Combo;
        }
        if (ImGui.RadioButton("Key Double Tap", openMode == Configuration.OpenMode.ShiftShift))
        {
            openMode = Configuration.OpenMode.ShiftShift;
        }

        ImGuiHelpers.ScaledDummy(10);

        switch (openMode)
        {
            case Configuration.OpenMode.ShiftShift:
                VirtualKeySelect("Key to double tap", ref shiftShiftKey);
                break;
            case Configuration.OpenMode.Combo:
                VirtualKeySelect("Combo Modifier", ref comboModifierKey);
                VirtualKeySelect("Combo Key", ref comboKey);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (ImGui.Button("Save"))
        {
            plugin.Configuration.ToSearch = (Configuration.SearchSetting) this.flags;

            plugin.Configuration.Open = openMode;
            plugin.Configuration.ShiftShiftKey = shiftShiftKey;

            plugin.Configuration.ComboKey = comboKey;
            plugin.Configuration.ComboModifier = comboModifierKey;

            plugin.Configuration.Save();
            IsOpen = false;
        }

        ImGui.SameLine();

        if (ImGui.Button("Discard"))
        {
            IsOpen = false;
        }
    }

    private void VirtualKeySelect(string text, ref VirtualKey chosen)
    {
        if (ImGui.BeginCombo(text, chosen.ToString()))
        {
            foreach (var key in Enum.GetValues<VirtualKey>())
            {
                if (ImGui.Selectable(key.ToString(), key == chosen))
                {
                    chosen = (VirtualKey) key;
                }
            }

            ImGui.EndCombo();
        }
    }
}