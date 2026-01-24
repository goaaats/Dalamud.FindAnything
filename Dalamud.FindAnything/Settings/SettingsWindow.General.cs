using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dalamud.FindAnything.Settings;

public partial class SettingsWindow {
    private void DrawGeneralTab() {
        using var tabItem = ImRaii.TabItem("General");
        if (!tabItem) return;

        using var scrollChild = ImRaii.Child("scrollArea", ImGuiHelpers.ScaledVector2(0, SaveDiscardOffset), false, ImGuiWindowFlags.HorizontalScrollbar);
        if (!scrollChild) return;

        ImGui.TextColored(ImGuiColors.DalamudGrey, "How to open");

        if (ImGui.RadioButton("Keyboard Combo", openMode == Configuration.OpenMode.Combo)) {
            openMode = Configuration.OpenMode.Combo;
        }

        if (ImGui.RadioButton("Key Double Tap", openMode == Configuration.OpenMode.ShiftShift)) {
            openMode = Configuration.OpenMode.ShiftShift;
        }

        ImGuiHelpers.ScaledDummy(10);

        switch (openMode) {
            case Configuration.OpenMode.ShiftShift:
                VirtualKeySelect("Key to double tap", ref shiftShiftKey);

                using (ImRaii.ItemWidth(ImGui.GetWindowWidth() * 0.2f)) {
                    if (ImGui.InputInt("##shift-shift-delay", ref shiftShiftDelay, 5, 50)) {
                        shiftShiftDelay = Math.Max(shiftShiftDelay, 0);
                    }

                    ImGui.SameLine();

                    if (ImGui.BeginCombo("Delay", shiftShiftUnit.ToString())) {
                        foreach (var key in Enum.GetValues<Configuration.DoubleTapUnit>()) {
                            if (ImGui.Selectable(key.ToString(), key == shiftShiftUnit)) {
                                shiftShiftUnit = key;
                            }
                        }

                        ImGui.EndCombo();
                    }
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

        DrawSpacedSeparator();

        ImGui.TextColored(ImGuiColors.DalamudGrey, "Search mode");
        ImGui.TextWrapped("Use this menu to select the default search mode:\n" +
                          "  - \"Simple\" looks for the exact text entered.\n" +
                          "  - \"Fuzzy\" finds close matches to your text even if some characters are missing (e.g. \"dufi\" can locate the Duty Finder).\n" +
                          "  - \"FuzzyParts\" is like Fuzzy but each word in the input is searched for separately, so that input word order does not matter.");
        ImGui.TextWrapped(
            "When using fuzzy search modes, results are shown in order from best match to worst match.");

        if (ImGui.BeginCombo("Search mode", matchMode.ToString())) {
            foreach (var key in Enum.GetValues<MatchMode>()) {
                if (ImGui.Selectable(key.ToString(), key == matchMode)) {
                    matchMode = key;
                }
            }

            ImGui.EndCombo();
        }

        ImGuiHelpers.ScaledDummy(5);
        ImGui.TextColored(ImGuiColors.DalamudGrey, "Search prefixes");
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(
            "Inputting one of the prefixes below as the first character of your search text " +
            "will temporarily change the search mode for that search.");

        using (ImRaii.ItemWidth(40)) {
            ImGui.InputText("Simple search mode prefix", ref matchSigilSimple, 1);
            ImGui.InputText("Fuzzy search mode prefix", ref matchSigilFuzzy, 1);
            ImGui.InputText("FuzzyParts search mode prefix", ref matchSigilFuzzyParts, 1);
        }

        DrawSpacedSeparator();

        DrawConstantsSection();

        DrawSpacedSeparator();

        using (ImRaii.ItemWidth(ImGui.GetWindowWidth() * 0.3f)) {
            ImGui.TextColored(ImGuiColors.DalamudGrey, "Scrolling");
            ImGui.TextWrapped("When using the keyboard to scroll through results, customise the scroll delay and speed.");
            using (var scrollCombo = ImRaii.Combo("Scroll style", cursorControl.ToString())) {
                if (scrollCombo) {
                    foreach (var key in Enum.GetValues<Configuration.CursorControlType>()) {
                        if (ImGui.Selectable(key.ToString(), key == cursorControl)) cursorControl = key;
                    }
                }
            }
            ImGuiComponents.HelpMarker(
                "- The \"System\" scroll style matches the key repeat rate and delays from your operating system.\n" +
                "- The \"Custom\" scroll style lets you customize these settings for Wotsit.");
        }

        if (cursorControl == Configuration.CursorControlType.Custom) {
            using (ImRaii.ItemWidth(ImGui.GetWindowWidth() * 0.2f)) {
                {
                    var val = lineScrollRepeatDelay;
                    if (ImGui.SliderInt("Line scroll repeat delay", ref val, 10, 500))
                        lineScrollRepeatDelay = Math.Clamp(val, 1, 10_000);
                    ImGuiComponents.HelpMarker("The number of milliseconds after to wait after pressing and holding Up/Down before the movement begins to repeat.\n\nControl-click the slider to enter a custom value. Default: 120.");
                }
                {
                    var val = lineScrollRepeatInterval;
                    if (ImGui.SliderInt("Line scroll repeat interval", ref val, 10, 500))
                        lineScrollRepeatInterval = Math.Clamp(val, 1, 10_000);
                    ImGuiComponents.HelpMarker("The number of milliseconds between each repeated line movement. Smaller numbers will repeat faster.\n\nIn the old system, \"Slow\" was 120, \"Medium\" was 65, and \"Fast\" was 30.\n\nControl-click the slider to enter a custom value. Default: 65.");
                }
                {
                    var val = pageScrollRepeatDelay;
                    if (ImGui.SliderInt("Page scroll repeat delay", ref val, 10, 500))
                        pageScrollRepeatDelay = Math.Clamp(val, 0, 10_000);
                    ImGuiComponents.HelpMarker("The number of milliseconds after to wait after pressing and holding Pgp/PgD before the movement begins to repeat.\n\nControl-click the slider to enter a custom value. Default: 200.");
                }
                {
                    var val = pageScrollRepeatInterval;
                    if (ImGui.SliderInt("Page scroll repeat interval", ref val, 10, 500))
                        pageScrollRepeatInterval = Math.Clamp(val, 0, 10_000);
                    ImGuiComponents.HelpMarker("The number of milliseconds between each repeated page movement. Smaller numbers will repeat faster.\n\nControl-click the slider to enter a custom value. Default: 200.");
                }
            }
        }

        DrawSpacedSeparator();

        const int craftingComboWidth = 300;
        ImGui.TextColored(ImGuiColors.DalamudGrey, "Crafting recipes");
        ImGui.Checkbox("Merge multiple crafting recipes for the same item", ref craftingMergeItems);
        if (craftingMergeItems) {
            ImGui.PushItemWidth(craftingComboWidth * ImGuiHelpers.GlobalScale);
            using (var singleCombo = ImRaii.Combo("When selecting a item with only one recipe...", craftingItemSingleSelect.GetDisplayName())) {
                if (singleCombo) {
                    foreach (var key in Enum.GetValues<Configuration.CraftingSingleSelectAction>()) {
                        if (ImGui.Selectable(key.GetDisplayName(), key == craftingItemSingleSelect)) {
                            craftingItemSingleSelect = key;
                        }
                    }
                }
            }
            ImGui.PushItemWidth(craftingComboWidth * ImGuiHelpers.GlobalScale);
            using (var mergedCombo = ImRaii.Combo("When selecting a item with multiple recipes...", craftingItemMergedSelect.GetDisplayName())) {
                if (mergedCombo) {
                    foreach (var key in Enum.GetValues<Configuration.CraftingMergedSelectAction>()) {
                        if (ImGui.Selectable(key.GetDisplayName(), key == craftingItemMergedSelect)) {
                            craftingItemMergedSelect = key;
                        }
                    }
                }
            }
        } else {
            ImGui.PushItemWidth(craftingComboWidth * ImGuiHelpers.GlobalScale);
            using (var singleCombo = ImRaii.Combo("When selecting a recipe...", craftingRecipeSelect.GetDisplayName())) {
                if (singleCombo) {
                    foreach (var key in Enum.GetValues<Configuration.CraftingSingleSelectAction>()) {
                        if (ImGui.Selectable(key.GetDisplayName(), key == craftingRecipeSelect)) {
                            craftingRecipeSelect = key;
                        }
                    }
                }
            }
        }

        DrawSpacedSeparator();

        ImGui.TextColored(ImGuiColors.DalamudGrey, "Other stuff");

        ImGui.Checkbox("Enable Search History", ref historyEnabled);
        ImGui.Checkbox("Show Gil cost in Aetheryte results", ref aetheryteGilCost);
        ImGui.Checkbox("Show \"Market Board\" shortcut to teleport to the closest market board city",
            ref marketBoardShortcut);
        ImGui.Checkbox("Show \"Striking Dummy\" shortcut to teleport to the closest striking dummy location",
            ref strikingDummyShortcut);
        ImGui.Checkbox("Show \"Inn Room\" shortcut to teleport to the closest inn room", ref innRoomShortcut);
        if (innRoomShortcut) {
            ImGui.Checkbox("Don't consider Limsa a valid Inn Room location", ref innRoomShortcutNoLimsa);
        }

        using (var combo = ImRaii.Combo("Emote Motion-Only?", emoteMotionMode.ToString())) {
            if (combo) {
                foreach (var key in Enum.GetValues<Configuration.EmoteMotionMode>()) {
                    if (ImGui.Selectable(key.ToString(), key == emoteMotionMode)) {
                        emoteMotionMode = key;
                    }
                }
            }
        }

        ImGui.Checkbox("Show Emote command in search result", ref showEmoteCommand);
        ImGui.Checkbox("Try to prevent spoilers in wiki mode(not 100% reliable)", ref wikiModeNoSpoilers);
        ImGui.Checkbox("Directly go to wiki mode when opening search", ref onlyWiki);
        if (ImGui.SliderFloat2("Search window position offset", ref posOffset, -800, 800)) {
            finderOffsetChangeTime = DateTime.UtcNow;
        }

        ImGui.Checkbox("Don't open Wotsit in combat", ref notInCombat);
        ImGui.Checkbox("Force TeamCraft links to open in your browser", ref tcForceBrowser);
        ImGui.Checkbox("Disable mouse selection in results list unless Quick Select Key is held", ref disableMouseSelection);
        ImGui.Checkbox("Match plugin settings and interface links using short form without 'Open'", ref matchShortPluginSettings);

        ImGuiHelpers.ScaledDummy(5);
    }


    private string tempConstantName = string.Empty;
    private float tempConstantValue;

    private void DrawConstantsSection() {
        ImGui.TextColored(ImGuiColors.DalamudGrey, "Math constants");
        ImGui.TextWrapped(
            "Use this menu to tie constants to values, to be used in expressions.\nAdd a constant again to edit it.");

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

        foreach (var constant in constants) {
            using var id = ImRaii.PushId($"constant_{constant.Key}");

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
            constants.Remove(toRemoveKey);

        ImGui.SetNextItemWidth(-1);
        ImGui.InputText($"###macroSn", ref tempConstantName, 100);

        ImGui.NextColumn();

        ImGui.SetNextItemWidth(-1);
        ImGui.InputFloat("###macroId", ref tempConstantValue);

        ImGui.NextColumn();

        using (ImRaii.PushId("constbtns")) {
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus)) {
                constants[tempConstantName] = tempConstantValue;

                tempConstantName = string.Empty;
                tempConstantValue = 0;
            }

            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("Add new constant");
            }

            ImGui.SameLine();

            if (ImGuiComponents.IconButton(FontAwesomeIcon.Copy)) {
                var json = JsonConvert.SerializeObject(constants);
                ImGui.SetClipboardText("WC1" + json);
            }

            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("Copy constants to clipboard");
            }

            ImGui.SameLine();

            if (ImGuiComponents.IconButton(FontAwesomeIcon.FileImport)) {
                ImportConstants(ImGui.GetClipboardText());
            }

            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("Import constants from clipboard");
            }

            ImGui.NextColumn();
        }

        ImGui.Columns();
    }

    private void ImportConstants(string contents) {
        if (!contents.StartsWith("WC1"))
            return;

        var data = JsonConvert.DeserializeObject<Dictionary<string, float>>(contents[3..]);

        data?.ToList().ForEach(x => {
            if (!constants.ContainsKey(x.Key)) {
                constants.Add(x.Key, x.Value);
            }
        });
    }
}
