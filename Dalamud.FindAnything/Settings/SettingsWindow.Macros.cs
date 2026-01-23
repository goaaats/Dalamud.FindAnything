using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility.Numerics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Dalamud.FindAnything.Settings;

public partial class SettingsWindow {
    private bool macroRearrangeMode;
    private int macroDragDropSource = -1;

    private void DrawMacrosTab() {
        using var tabItem = ImRaii.TabItem("Macro links");
        if (!tabItem) return;

        using var scrollChild = ImRaii.Child("scrollArea", ImGuiHelpers.ScaledVector2(0, SaveDiscardOffset), false, ImGuiWindowFlags.HorizontalScrollbar);
        if (!scrollChild) return;

        ImGui.TextColored(ImGuiColors.DalamudGrey, "Macro links");

        if (macroRearrangeMode) {
            DrawMacroArrangeView();
        } else {
            DrawMacroSettingsView();
        }

        DrawSpacedSeparator();

        ImGui.TextColored(ImGuiColors.DalamudGrey, "Search direction");
        ImGui.TextWrapped(
            "Use this to change the order in which macro links are searched. This may affect which macro links are displayed higher in the result list.");

        if (ImGui.RadioButton("Top to bottom", macroLinksSearch == Configuration.MacroSearchDirection.TopToBottom)) {
            macroLinksSearch = Configuration.MacroSearchDirection.TopToBottom;
        }
        if (ImGui.RadioButton("Bottom to top", macroLinksSearch == Configuration.MacroSearchDirection.BottomToTop)) {
            macroLinksSearch = Configuration.MacroSearchDirection.BottomToTop;
        }
    }

    private void DrawMacroSettingsView() {
        ImGui.TextWrapped("Use this menu to tie search results to macros.\nClick \"Add Macro\", enter the text you want to access it under, select whether or not it is a shared macro and enter its ID.\nUse the ';' character to add search text for a macro, only the first part text will be shown, e.g. \"SGE;sage;healer\".");

        ImGui.Columns(6);
        ImGui.SetColumnWidth(0, 240 + 5 * ImGuiHelpers.GlobalScale);
        ImGui.SetColumnWidth(1, 120 + 5 * ImGuiHelpers.GlobalScale);
        ImGui.SetColumnWidth(2, 60 + 5 * ImGuiHelpers.GlobalScale);
        ImGui.SetColumnWidth(3, 250 + 5 * ImGuiHelpers.GlobalScale);
        ImGui.SetColumnWidth(4, 70 + 5 * ImGuiHelpers.GlobalScale);
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

        for (var macroNumber = macros.Count - 1; macroNumber >= 0; macroNumber--) {
            DrawSingleMacroSetting(macroNumber);
            ImGui.Separator();
        }

        ImGui.Columns();

        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Plus, "Add")) {
            macros.Insert(0, new Configuration.MacroEntry {
                Id = 0,
                SearchName = "New Macro",
                Shared = false,
                IconId = 066001,
                Line = string.Empty,
            });
        }
        DrawHoverTooltip("Add new macro link");

        ImGui.SameLine();
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Copy, "Copy")) {
            var json = JsonConvert.SerializeObject(this.macros);
            ImGui.SetClipboardText("WM1" + json);
        }
        DrawHoverTooltip("Copy macro links to clipboard");

        ImGui.SameLine();
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.FileImport, "Import")) {
            ImportMacros(ImGui.GetClipboardText());
        }
        DrawHoverTooltip("Import macro links from clipboard");

        ImGui.SameLine();
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.ArrowsUpDown, "Rearrange")) {
            macroRearrangeMode = true;
        }
        DrawHoverTooltip("Rearrange macro links");
    }

    private void DrawSingleMacroSetting(int macroNumber) {
        var macro = macros[macroNumber];
        using var guiId = ImRaii.PushId($"macroSetting:{macroNumber}");

        ImGui.SetNextItemWidth(-1);

        var text = macro.SearchName;
        if (ImGui.InputText($"##name", ref text, 100)) {
            macro.SearchName = text;
        }

        ImGui.NextColumn();

        ImGui.SetNextItemWidth(90);
        using (var macroKindCombo = ImRaii.Combo("##kind", $"{macro.Kind}")) {
            if (macroKindCombo) {
                foreach (var macroEntryKind in Enum.GetValues<Configuration.MacroEntry.MacroEntryKind>()) {
                    if (ImGui.Selectable(macroEntryKind.ToString(), macroEntryKind == macro.Kind)) {
                        macro.Kind = macroEntryKind;
                    }
                }
            }
        }

        ImGui.NextColumn();

        if (macro.Kind == Configuration.MacroEntry.MacroEntryKind.Id) {
            var isShared = macro.Shared;
            if (ImGui.Checkbox($"##shared", ref isShared)) {
                macro.Shared = isShared;
            }
        } else {
            using (new ImRaii.Color()
                       .Push(ImGuiCol.FrameBg, ImGuiColors.ParsedGrey)
                       .Push(ImGuiCol.FrameBgActive, ImGuiColors.ParsedGrey)
                       .Push(ImGuiCol.FrameBgHovered, ImGuiColors.ParsedGrey)
                       .Push(ImGuiCol.CheckMark, ImGuiColors.ParsedGrey)) {
                var alwaysFalse = false;
                ImGui.Checkbox("##shared", ref alwaysFalse);
            }
        }


        ImGui.NextColumn();

        switch (macro.Kind) {
            case Configuration.MacroEntry.MacroEntryKind.Id:
                ImGui.SetNextItemWidth(-1);
                var id = macro.Id;
                if (ImGui.InputInt($"##id", ref id, 1, 10)) {
                    macro.Id = Math.Clamp(id, 0, 99);
                }

                break;

            case Configuration.MacroEntry.MacroEntryKind.SingleLine: {
                ImGui.SetNextItemWidth(-1);
                var line = macro.Line;
                using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed, !line.StartsWith('/'))) {
                    if (ImGui.InputText($"##id", ref line, 100)) {
                        macro.Line = line;
                    }
                }
            }
                break;
            default:
                throw new ArgumentOutOfRangeException($"Unknown MacroEntryKind: {macro.Kind}");
        }

        ImGui.NextColumn();

        ImGui.SetNextItemWidth(-1);
        var icon = macro.IconId;
        if (ImGui.InputInt($"##icon", ref icon)) {
            icon = Math.Max(0, icon);
            macro.IconId = icon;
        }

        ImGui.NextColumn();

        macros[macroNumber] = macro;

        if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
            macros.RemoveAt(macroNumber);

        ImGui.NextColumn();
    }

    private void DrawMacroArrangeView() {
        ImGui.TextWrapped("Use arrows or drag and drop macros to change the order.");

        for (var macroNumber = macros.Count - 1; macroNumber >= 0; macroNumber--) {
            var name = macros[macroNumber].SearchName;
            var size = ImGuiHelpers.GetButtonSize(name).WithX(300f);
            using (ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, new Vector2(0f, 0.5f))) {
                ImGui.Button($"{macros[macroNumber].SearchName}###b{macroNumber}", size);

                using (var source = ImRaii.DragDropSource()) {
                    if (source) {
                        ImGui.SetDragDropPayload("MACRO", ReadOnlySpan<byte>.Empty);
                        ImGui.Button($"{macros[macroNumber].SearchName}###d{macroNumber}", size);
                        macroDragDropSource = macroNumber;
                    }
                }

                using (var target = ImRaii.DragDropTarget()) {
                    if (target) {
                        ImGui.AcceptDragDropPayload("MACRO");
                        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left)) {
                            var moving = macros[macroDragDropSource];
                            macros.RemoveAt(macroDragDropSource);
                            macros.Insert(macroNumber, moving);
                        }
                    }
                }
            }

            ImGui.SameLine();
            if (IconButtonEnabledWhen(macroNumber <= macros.Count - 2, FontAwesomeIcon.AngleDoubleUp,
                    $"macroTop:{macroNumber}", "Move to top")) {
                var moving = macros[macroNumber];
                macros.RemoveAt(macroNumber);
                macros.Add(moving);
            }

            ImGui.SameLine();
            if (IconButtonEnabledWhen(macroNumber <= macros.Count - 2, FontAwesomeIcon.ArrowUp,
                    $"macroUp:{macroNumber}", "Move up")) {
                macros.Reverse(macroNumber, 2);
            }

            ImGui.SameLine();
            if (IconButtonEnabledWhen(macroNumber >= 1, FontAwesomeIcon.ArrowDown, $"macroDown:{macroNumber}", "Move down")) {
                macros.Reverse(macroNumber - 1, 2);
            }

            ImGui.SameLine();
            if (IconButtonEnabledWhen(macroNumber >= 1, FontAwesomeIcon.AngleDoubleDown,
                    $"macroBottom:{macroNumber}", "Move to bottom")) {
                var moving = macros[macroNumber];
                macros.RemoveAt(macroNumber);
                macros.Insert(0, moving);
            }
        }

        ImGuiHelpers.ScaledDummy(15);
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.HandPaper, "Stop rearranging")) {
            macroRearrangeMode = false;
        }
        ImGui.SameLine();
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.ArrowsAltV, "Reverse all")) {
            macros.Reverse();
        }
    }

    private void ImportMacros(string contents) {
        if (!contents.StartsWith("WM1"))
            return;

        var data = JsonConvert.DeserializeObject<List<Configuration.MacroEntry>>(contents[3..]);

        if (data == null)
            return;

        macros.InsertRange(0, data);
    }

    private static void DrawHoverTooltip(string text) {
        if (text == "") return;
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip(text);
        }
    }

    private static void DrawSpacedSeparator() {
        ImGuiHelpers.ScaledDummy(15);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(15);
    }
}
