using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
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
    private List<Configuration.MacroEntry> macros = new();
    private bool aetheryteGilCost;
    private Configuration.EmoteMotionMode emoteMotionMode;
    private bool showEmoteCommand;

    public SettingsWindow(FindAnythingPlugin plugin) : base("Wotsit Settings", ImGuiWindowFlags.NoResize)
    {
        this.SizeCondition = ImGuiCond.Always;
        this.Size = new Vector2(700, 550);
        this.plugin = plugin;
    }

    public override void OnOpen()
    {
        this.flags = (uint) FindAnythingPlugin.Configuration.ToSearchV2;
        this.openMode = FindAnythingPlugin.Configuration.Open;
        this.shiftShiftKey = FindAnythingPlugin.Configuration.ShiftShiftKey;
        this.shiftShiftDelay = (int) FindAnythingPlugin.Configuration.ShiftShiftDelay;
        this.comboKey = FindAnythingPlugin.Configuration.ComboKey;
        this.comboModifierKey = FindAnythingPlugin.Configuration.ComboModifier;
        this.macros = FindAnythingPlugin.Configuration.MacroLinks.Select(x => new Configuration.MacroEntry(x)).ToList();
        this.aetheryteGilCost = FindAnythingPlugin.Configuration.DoAetheryteGilCost;
        this.emoteMotionMode = FindAnythingPlugin.Configuration.EmoteMode;
        this.showEmoteCommand = FindAnythingPlugin.Configuration.ShowEmoteCommand;
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

        ImGuiHelpers.ScaledDummy(20);

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
        
        ImGuiHelpers.ScaledDummy(20);

        ImGui.TextColored(ImGuiColors.DalamudGrey, "Macro Links");
        ImGui.TextWrapped("Use this menu to tie search results to macros.\nClick \"Add Macro\", enter the text you want to access it under, select whether or not it is a shared macro and enter its ID.\nUse the ';' character to add search text for a macro, only the first part text will be shown, e.g. \"SGE;sage;healer\".");

        DrawMacrosSection();
        
        ImGuiHelpers.ScaledDummy(20);
        
        ImGui.TextColored(ImGuiColors.DalamudGrey, "Others");

        ImGui.Checkbox("Show Gil cost in Aetheryte results", ref this.aetheryteGilCost);

        if (ImGui.BeginCombo("Emote Motion-Only?", this.emoteMotionMode.ToString()))
        {
            foreach (var key in Enum.GetValues<Configuration.EmoteMotionMode>())
            {
                if (ImGui.Selectable(key.ToString(), key == this.emoteMotionMode))
                {
                    this.emoteMotionMode = key;
                }
            }

            ImGui.EndCombo();
        }

        ImGui.Checkbox("Show Emote command in search result", ref this.showEmoteCommand);
        
        ImGuiHelpers.ScaledDummy(10);

        if (ImGui.Button("Save"))
        {
            FindAnythingPlugin.Configuration.ToSearchV2 = (Configuration.SearchSetting) this.flags;

            FindAnythingPlugin.Configuration.Open = openMode;
            FindAnythingPlugin.Configuration.ShiftShiftKey = shiftShiftKey;
            FindAnythingPlugin.Configuration.ShiftShiftDelay = (uint) shiftShiftDelay;

            FindAnythingPlugin.Configuration.ComboKey = comboKey;
            FindAnythingPlugin.Configuration.ComboModifier = comboModifierKey;

            FindAnythingPlugin.Configuration.MacroLinks = this.macros;

            FindAnythingPlugin.Configuration.DoAetheryteGilCost = this.aetheryteGilCost;
            FindAnythingPlugin.Configuration.EmoteMode = this.emoteMotionMode;
            FindAnythingPlugin.Configuration.ShowEmoteCommand = this.showEmoteCommand;

            FindAnythingPlugin.Configuration.Save();

            FindAnythingPlugin.TexCache.ReloadMacroIcons();
            IsOpen = false;
        }

        ImGui.SameLine();

        if (ImGui.Button("Discard"))
        {
            IsOpen = false;
        }
    }

    private void DrawMacrosSection()
    {
        ImGui.Columns(5);
        ImGui.SetColumnWidth(0, 200 + 5 * ImGuiHelpers.GlobalScale);
        ImGui.SetColumnWidth(1, 80 + 5 * ImGuiHelpers.GlobalScale);
        ImGui.SetColumnWidth(2, 160 + 5 * ImGuiHelpers.GlobalScale);
        ImGui.SetColumnWidth(3, 160 + 5 * ImGuiHelpers.GlobalScale);
        ImGui.SetColumnWidth(4, 30 + 5 * ImGuiHelpers.GlobalScale);

        ImGui.Separator();

        ImGui.Text("Search Name");
        ImGui.NextColumn();
        ImGui.Text("Shared");
        ImGui.NextColumn();
        ImGui.Text("ID");
        ImGui.NextColumn();
        ImGui.Text("Icon");
        ImGui.NextColumn();
        ImGui.Text(string.Empty);
        ImGui.NextColumn();

        ImGui.Separator();

        for (var macroNumber = this.macros.Count - 1; macroNumber >= 0; macroNumber--)
        {
            var macro = this.macros[macroNumber];
            ImGui.PushID($"macro_{macroNumber}");

            ImGui.SetNextItemWidth(-1);
            
            var text = macro.SearchName;
            if (ImGui.InputText($"###macroSn", ref text, 100))
            {
                macro.SearchName = text;
            }
            
            ImGui.NextColumn();

            var isShared = macro.Shared;
            if (ImGui.Checkbox($"###macroSh", ref isShared))
            {
                macro.Shared = isShared;
            }

            ImGui.NextColumn();

            var id = macro.Id;
            if (ImGui.InputInt($"###macroId", ref id))
            {
                id = Math.Max(0, id);
                id = Math.Min(99, id);
                macro.Id = id;
            }
            
            ImGui.NextColumn();
            
            var icon = macro.IconId;
            if (ImGui.InputInt($"###macroIcon", ref icon))
            {
                icon = Math.Max(0, icon);
                macro.IconId = icon;
            } 
            
            ImGui.NextColumn();

            if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash)) this.macros.RemoveAt(macroNumber);

            ImGui.PopID();

            ImGui.NextColumn();
            ImGui.Separator();
        }

        ImGui.NextColumn();
        ImGui.NextColumn();
        ImGui.NextColumn();
        ImGui.NextColumn();
        
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus))
        {
            this.macros.Add(new Configuration.MacroEntry
            {
                Id = 0,
                SearchName = "New Macro",
                Shared = false,
                IconId = 066001,
            });
        }

        ImGui.Columns(1);
    }

    private void VirtualKeySelect(string text, ref VirtualKey chosen)
    {
        if (ImGui.BeginCombo(text, chosen.GetFancyName()))
        {
            foreach (var key in Enum.GetValues<VirtualKey>().Where(x => x != VirtualKey.LBUTTON))
            {
                if (ImGui.Selectable(key.GetFancyName(), key == chosen))
                {
                    chosen = key;
                }
            }

            ImGui.EndCombo();
        }
    }
}