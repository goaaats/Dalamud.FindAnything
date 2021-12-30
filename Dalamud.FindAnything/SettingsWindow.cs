using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Newtonsoft.Json;

namespace Dalamud.FindAnything;

public class SettingsWindow : Window
{
    private readonly FindAnythingPlugin plugin;
    private uint flags;
    private Configuration.OpenMode openMode;
    private VirtualKey shiftShiftKey;
    private int shiftShiftDelay;
    private VirtualKey comboModifierKey;
    private VirtualKey comboModifier2Key;
    private VirtualKey comboKey;
    private VirtualKey wikiComboKey;
    private bool preventPassthrough;
    private List<Configuration.MacroEntry> macros = new();
    private bool aetheryteGilCost;
    private Configuration.EmoteMotionMode emoteMotionMode;
    private bool showEmoteCommand;
    private bool wikiModeNoSpoilers;
    private Dictionary<string, float> constants = new();
    private Vector2 posOffset;
    private bool onlyWiki;
    private VirtualKey quickSelectKey;
    private List<Configuration.SearchSetting> order = new();
    private Configuration.ScrollSpeed speed;
    private bool notInCombat;
    private bool tcForceBrowser;

    public SettingsWindow(FindAnythingPlugin plugin) : base("Wotsit Settings", ImGuiWindowFlags.NoResize)
    {
        this.SizeCondition = ImGuiCond.Always;
        this.Size = new Vector2(850, 660) * ImGuiHelpers.GlobalScale;
        this.plugin = plugin;
    }

    public override void OnOpen()
    {
        this.flags = (uint) FindAnythingPlugin.Configuration.ToSearchV3;
        this.openMode = FindAnythingPlugin.Configuration.Open;
        this.shiftShiftKey = FindAnythingPlugin.Configuration.ShiftShiftKey;
        this.shiftShiftDelay = (int) FindAnythingPlugin.Configuration.ShiftShiftDelay;
        this.comboKey = FindAnythingPlugin.Configuration.ComboKey;
        this.comboModifierKey = FindAnythingPlugin.Configuration.ComboModifier;
        this.comboModifier2Key = FindAnythingPlugin.Configuration.ComboModifier2;
        this.wikiComboKey = FindAnythingPlugin.Configuration.WikiComboKey;
        this.preventPassthrough = FindAnythingPlugin.Configuration.PreventPassthrough;
        this.macros = FindAnythingPlugin.Configuration.MacroLinks.Select(x => new Configuration.MacroEntry(x)).ToList();
        this.aetheryteGilCost = FindAnythingPlugin.Configuration.DoAetheryteGilCost;
        this.emoteMotionMode = FindAnythingPlugin.Configuration.EmoteMode;
        this.showEmoteCommand = FindAnythingPlugin.Configuration.ShowEmoteCommand;
        this.wikiModeNoSpoilers = FindAnythingPlugin.Configuration.WikiModeNoSpoilers;
        this.constants = FindAnythingPlugin.Configuration.MathConstants;
        this.posOffset = FindAnythingPlugin.Configuration.PositionOffset;
        this.onlyWiki = FindAnythingPlugin.Configuration.OnlyWikiMode;
        this.quickSelectKey = FindAnythingPlugin.Configuration.QuickSelectKey;
        this.order = FindAnythingPlugin.Configuration.Order.ToList();
        this.speed = FindAnythingPlugin.Configuration.Speed;
        this.notInCombat = FindAnythingPlugin.Configuration.NotInCombat;
        this.tcForceBrowser = FindAnythingPlugin.Configuration.TeamCraftForceBrowser;
        base.OnOpen();
    }

    public override void Draw()
    {
        ImGui.TextColored(ImGuiColors.DalamudGrey, "What to search");
        for (var i = 0; i < this.order.Count; i++) {
            var search = this.order[i];

            var name = search switch {
                Configuration.SearchSetting.Duty => "Duties",
                Configuration.SearchSetting.Aetheryte => "Aetherytes",
                Configuration.SearchSetting.MainCommand => "Commands",
                Configuration.SearchSetting.GeneralAction => "General Actions",
                Configuration.SearchSetting.Emote => "Emotes",
                Configuration.SearchSetting.PluginSettings => "other plugins",
                Configuration.SearchSetting.Gearsets => "Gear Sets",
                Configuration.SearchSetting.CraftingRecipes => "Crafting Recipes",
                Configuration.SearchSetting.GatheringItems => "Gathering Items",
                Configuration.SearchSetting.Mounts => "Mounts",
                Configuration.SearchSetting.Minions => "Minions",
                Configuration.SearchSetting.MacroLinks => "Macro Links",
                Configuration.SearchSetting.Internal => "Wotsit",
                _ => null,
            };

            if (name == null) {
                continue;
            }

            var isRequired = search is Configuration.SearchSetting.Internal or Configuration.SearchSetting.MacroLinks;

            ImGui.PushFont(UiBuilder.IconFont);

            if (ImGui.Button($"{FontAwesomeIcon.ArrowUp.ToIconString()}##{search}") && i != 0) {
                (this.order[i], this.order[i - 1]) = (this.order[i - 1], this.order[i]);
            }

            ImGui.SameLine();

            if (ImGui.Button($"{FontAwesomeIcon.ArrowDown.ToIconString()}##{search}") && i != this.order.Count - 1) {
                (this.order[i], this.order[i + 1]) = (this.order[i + 1], this.order[i]);
            }

            ImGui.PopFont();

            ImGui.SameLine();

            if (isRequired) {
                ImGui.TextUnformatted($"Search in {name}");
            } else {
                ImGui.CheckboxFlags($"Search in {name}", ref this.flags, (uint) search);
            }
        }

        ImGui.CheckboxFlags("Mathematical Expressions", ref this.flags, (uint) Configuration.SearchSetting.Maths);

        ImGuiHelpers.ScaledDummy(15);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(15);

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
                VirtualKeySelect("Combo Modifier 1", ref comboModifierKey);
                VirtualKeySelect("Combo Modifier 2", ref comboModifier2Key);
                VirtualKeySelect("Combo Key", ref comboKey);

                ImGuiHelpers.ScaledDummy(2);
                VirtualKeySelect("Wiki Modifier(go directly to wiki mode)", ref wikiComboKey);
                ImGuiHelpers.ScaledDummy(2);

                ImGui.Checkbox("Prevent passthrough to game", ref preventPassthrough);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        ImGuiHelpers.ScaledDummy(5);

        VirtualKeySelect("Quick Select Key", ref quickSelectKey);

        ImGuiHelpers.ScaledDummy(15);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(15);

        ImGui.TextColored(ImGuiColors.DalamudGrey, "Macro Links");
        ImGui.TextWrapped("Use this menu to tie search results to macros.\nClick \"Add Macro\", enter the text you want to access it under, select whether or not it is a shared macro and enter its ID.\nUse the ';' character to add search text for a macro, only the first part text will be shown, e.g. \"SGE;sage;healer\".");

        DrawMacrosSection();

        ImGuiHelpers.ScaledDummy(15);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(15);

        ImGui.TextColored(ImGuiColors.DalamudGrey, "Math Constants");
        ImGui.TextWrapped("Use this menu to tie constants to values, to be used in expressions.\nAdd a constant again to edit it.");

        DrawConstantsSection();

        ImGuiHelpers.ScaledDummy(15);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(15);

        ImGui.TextColored(ImGuiColors.DalamudGrey, "Other stuff");

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
        ImGui.Checkbox("Try to prevent spoilers in wiki mode(not 100% reliable)", ref this.wikiModeNoSpoilers);
        ImGui.Checkbox("Directly go to wiki mode when opening search", ref this.onlyWiki);
        ImGui.SliderFloat2("Search window position offset", ref this.posOffset, -800, 800);
        if (ImGui.BeginCombo("Scroll Speed", this.speed.ToString()))
        {
            foreach (var key in Enum.GetValues<Configuration.ScrollSpeed>())
            {
                if (ImGui.Selectable(key.ToString(), key == this.speed))
                {
                    this.speed = key;
                }
            }

            ImGui.EndCombo();
        }
        ImGui.Checkbox("Don't open Wotsit in combat", ref this.notInCombat);
        ImGui.Checkbox("Force TeamCraft links to open in your browser", ref this.tcForceBrowser);

        ImGuiHelpers.ScaledDummy(5);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5);

        if (ImGui.Button("Save"))
        {
            FindAnythingPlugin.Configuration.ToSearchV3 = (Configuration.SearchSetting) this.flags;
            FindAnythingPlugin.Configuration.Order = this.order;

            FindAnythingPlugin.Configuration.Open = openMode;
            FindAnythingPlugin.Configuration.ShiftShiftKey = shiftShiftKey;
            FindAnythingPlugin.Configuration.ShiftShiftDelay = (uint) shiftShiftDelay;

            FindAnythingPlugin.Configuration.ComboKey = comboKey;
            FindAnythingPlugin.Configuration.ComboModifier = comboModifierKey;
            FindAnythingPlugin.Configuration.ComboModifier2 = comboModifier2Key;
            FindAnythingPlugin.Configuration.WikiComboKey = wikiComboKey;
            FindAnythingPlugin.Configuration.PreventPassthrough = preventPassthrough;

            FindAnythingPlugin.Configuration.MacroLinks = this.macros;

            FindAnythingPlugin.Configuration.DoAetheryteGilCost = this.aetheryteGilCost;
            FindAnythingPlugin.Configuration.EmoteMode = this.emoteMotionMode;
            FindAnythingPlugin.Configuration.ShowEmoteCommand = this.showEmoteCommand;
            FindAnythingPlugin.Configuration.WikiModeNoSpoilers = this.wikiModeNoSpoilers;
            FindAnythingPlugin.Configuration.PositionOffset = this.posOffset;
            FindAnythingPlugin.Configuration.OnlyWikiMode = this.onlyWiki;
            FindAnythingPlugin.Configuration.QuickSelectKey = this.quickSelectKey;
            FindAnythingPlugin.Configuration.Speed = this.speed;
            FindAnythingPlugin.Configuration.NotInCombat = this.notInCombat;
            FindAnythingPlugin.Configuration.TeamCraftForceBrowser = this.tcForceBrowser;

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
        ImGui.Columns(6);
        ImGui.SetColumnWidth(0, 200 + 5 * ImGuiHelpers.GlobalScale);
        ImGui.SetColumnWidth(1, 140 + 5 * ImGuiHelpers.GlobalScale);
        ImGui.SetColumnWidth(2, 80 + 5 * ImGuiHelpers.GlobalScale);
        ImGui.SetColumnWidth(3, 160 + 5 * ImGuiHelpers.GlobalScale);
        ImGui.SetColumnWidth(4, 160 + 5 * ImGuiHelpers.GlobalScale);
        ImGui.SetColumnWidth(5, 30 + 5 * ImGuiHelpers.GlobalScale);

        ImGui.Separator();

        ImGui.Text("Search Name");
        ImGui.NextColumn();
        ImGui.Text("Kind");
        ImGui.NextColumn();
        ImGui.Text("Shared");
        ImGui.NextColumn();
        ImGui.Text("ID/Line");
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

            if (ImGui.BeginCombo("###macroKnd", macro.Kind.ToString()))
            {
                foreach (var macroEntryKind in Enum.GetValues<Configuration.MacroEntry.MacroEntryKind>())
                {
                    if (ImGui.Selectable(macroEntryKind.ToString(), macroEntryKind == macro.Kind))
                    {
                        macro.Kind = macroEntryKind;
                    }
                }
                ImGui.EndCombo();
            }

            ImGui.NextColumn();

            if (macro.Kind == Configuration.MacroEntry.MacroEntryKind.Id)
            {
                var isShared = macro.Shared;
                if (ImGui.Checkbox($"###macroSh", ref isShared))
                {
                    macro.Shared = isShared;
                }
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.FrameBg, ImGuiColors.ParsedGrey);
                ImGui.PushStyleColor(ImGuiCol.FrameBgActive, ImGuiColors.ParsedGrey);
                ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, ImGuiColors.ParsedGrey);
                ImGui.PushStyleColor(ImGuiCol.CheckMark, ImGuiColors.ParsedGrey);

                var isShared = false;
                ImGui.Checkbox("###macroSh", ref isShared);

                ImGui.PopStyleColor(4);
            }


            ImGui.NextColumn();

            switch (macro.Kind)
            {
                case Configuration.MacroEntry.MacroEntryKind.Id:
                    ImGui.SetNextItemWidth(-1);

                    var id = macro.Id;
                    if (ImGui.InputInt($"###macroId", ref id))
                    {
                        id = Math.Max(0, id);
                        id = Math.Min(99, id);
                        macro.Id = id;
                    }

                    break;
                case Configuration.MacroEntry.MacroEntryKind.SingleLine:
                    var line = macro.Line;
                    line ??= string.Empty;
                    var didColor = false;
                    if (!line.StartsWith("/"))
                    {
                        didColor = true;
                        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
                    }

                    ImGui.SetNextItemWidth(-1);

                    if (ImGui.InputText($"###macroId", ref line, 100))
                    {
                        macro.Line = line;
                    }

                    if (didColor)
                    {
                        ImGui.PopStyleColor();
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            ImGui.NextColumn();

            var icon = macro.IconId;

            ImGui.SetNextItemWidth(-1);

            if (ImGui.InputInt($"###macroIcon", ref icon))
            {
                icon = Math.Max(0, icon);
                macro.IconId = icon;
            }

            ImGui.NextColumn();

            this.macros[macroNumber] = macro;

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
            this.macros.Insert(0, new Configuration.MacroEntry
            {
                Id = 0,
                SearchName = "New Macro",
                Shared = false,
                IconId = 066001,
                Line = string.Empty,
            });
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Add new macro link");
        }

        ImGui.SameLine();

        if (ImGuiComponents.IconButton(FontAwesomeIcon.Copy))
        {
            var json = JsonConvert.SerializeObject(this.macros);
            ImGui.SetClipboardText("WM1" + json);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Copy macro links to clipboard");
        }

        ImGui.SameLine();

        if (ImGuiComponents.IconButton(FontAwesomeIcon.FileImport))
        {
            ImportMacros(ImGui.GetClipboardText());
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Import macro links from clipboard");
        }

        ImGui.Columns(1);
    }

    private string tempConstantName = string.Empty;
    private float tempConstantValue = 0;

    private void DrawConstantsSection()
    {
        ImGui.Columns(3);
        ImGui.SetColumnWidth(0, 200 + 5 * ImGuiHelpers.GlobalScale);
        ImGui.SetColumnWidth(1, 200 + 5 * ImGuiHelpers.GlobalScale);
        ImGui.SetColumnWidth(2, 100 + 5 * ImGuiHelpers.GlobalScale);

        ImGui.Separator();

        ImGui.Text("Name");
        ImGui.NextColumn();
        ImGui.Text("Value");
        ImGui.NextColumn();
        ImGui.Text(string.Empty);
        ImGui.NextColumn();

        ImGui.Separator();

        string? toRemoveKey = null;

        foreach (var constant in this.constants) {
            ImGui.PushID($"constant_{constant.Key}");

            ImGui.SetNextItemWidth(-1);

            ImGui.Text(constant.Key);

            ImGui.NextColumn();

            ImGui.Text(constant.Value.ToString());

            ImGui.SetNextItemWidth(-1);

            ImGui.NextColumn();

            if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash)) toRemoveKey = constant.Key;

            ImGui.NextColumn();
            ImGui.Separator();
        }

        if (toRemoveKey != null)
            this.constants.Remove(toRemoveKey);

        ImGui.SetNextItemWidth(-1);
        ImGui.InputText($"###macroSn", ref this.tempConstantName, 100);

        ImGui.NextColumn();

        ImGui.SetNextItemWidth(-1);
        ImGui.InputFloat("###macroId", ref this.tempConstantValue);

        ImGui.NextColumn();

        ImGui.PushID("constbtns");

        if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus))
        {
            if (constants.ContainsKey(tempConstantName))
            {
                constants[tempConstantName] = tempConstantValue;
            }
            else
            {
                constants.Add(tempConstantName, tempConstantValue);
            }

            this.tempConstantName = string.Empty;
            this.tempConstantValue = 0;
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Add new constant");
        }

        ImGui.SameLine();

        if (ImGuiComponents.IconButton(FontAwesomeIcon.Copy))
        {
            var json = JsonConvert.SerializeObject(this.constants);
            ImGui.SetClipboardText("WC1" + json);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Copy constants to clipboard");
        }

        ImGui.SameLine();

        if (ImGuiComponents.IconButton(FontAwesomeIcon.FileImport))
        {
            ImportConstants(ImGui.GetClipboardText());
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Import constants from clipboard");
        }

        ImGui.NextColumn();

        ImGui.PopID();

        ImGui.Columns(1);
    }

    private void ImportMacros(string contents)
    {
        if (!contents.StartsWith("WM1"))
            return;

        var data = JsonConvert.DeserializeObject<List<Configuration.MacroEntry>>(contents.Substring(3));

        if (data == null)
            return;

        this.macros.InsertRange(0, data);
    }

    private void ImportConstants(string contents)
    {
        if (!contents.StartsWith("WC1"))
            return;

        var data = JsonConvert.DeserializeObject<Dictionary<string, float>>(contents.Substring(3));

        data?.ToList().ForEach(x =>
        {
            if (!this.constants.ContainsKey(x.Key))
            {
                this.constants.Add(x.Key, x.Value);
            }
        });
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
