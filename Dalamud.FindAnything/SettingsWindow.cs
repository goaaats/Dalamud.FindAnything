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
    private int shiftShiftDelay;
    private VirtualKey comboModifierKey;
    private VirtualKey comboKey;

    public SettingsWindow(FindAnythingPlugin plugin) : base("Wotsit Settings", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize)
    {
        this.plugin = plugin;
    }

    public override void OnOpen()
    {
        flags = (uint) FindAnythingPlugin.Configuration.ToSearchV2;
        openMode = FindAnythingPlugin.Configuration.Open;
        shiftShiftKey = FindAnythingPlugin.Configuration.ShiftShiftKey;
        shiftShiftDelay = (int) FindAnythingPlugin.Configuration.ShiftShiftDelay;
        comboKey = FindAnythingPlugin.Configuration.ComboKey;
        comboModifierKey = FindAnythingPlugin.Configuration.ComboModifier;
        base.OnOpen();
    }

    public override void Draw()
    {
        ImGui.TextColored(ImGuiColors.DalamudGrey, "What to search");
        ImGui.CheckboxFlags("Search in Duties", ref this.flags, (uint) Configuration.SearchSetting.Duty);
        ImGui.CheckboxFlags("Search in Commands", ref this.flags, (uint) Configuration.SearchSetting.MainCommand);
        ImGui.CheckboxFlags("Search in Aetherytes", ref this.flags, (uint) Configuration.SearchSetting.Aetheryte);
        ImGui.CheckboxFlags("Search in General Actions", ref this.flags, (uint) Configuration.SearchSetting.GeneralAction);
        ImGui.CheckboxFlags("Search in other Plugins", ref this.flags, (uint) Configuration.SearchSetting.PluginSettings);

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

                if (ImGui.InputInt("Delay (ms)", ref shiftShiftDelay))
                {
                    shiftShiftDelay = Math.Max(shiftShiftDelay, 0);
                }
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
            FindAnythingPlugin.Configuration.ToSearchV2 = (Configuration.SearchSetting) this.flags;

            FindAnythingPlugin.Configuration.Open = openMode;
            FindAnythingPlugin.Configuration.ShiftShiftKey = shiftShiftKey;
            FindAnythingPlugin.Configuration.ShiftShiftDelay = (uint) shiftShiftDelay;

            FindAnythingPlugin.Configuration.ComboKey = comboKey;
            FindAnythingPlugin.Configuration.ComboModifier = comboModifierKey;

            FindAnythingPlugin.Configuration.Save();
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