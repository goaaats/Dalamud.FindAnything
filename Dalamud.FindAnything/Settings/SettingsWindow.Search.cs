using Dalamud.Bindings.ImGui;
using Dalamud.FindAnything.Modules;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System.Collections.Generic;
using System.Numerics;

namespace Dalamud.FindAnything.Settings;

public partial class SettingsWindow {
    private void DrawSearchTab() {
        using var tabItem = ImRaii.TabItem("What to search");
        if (!tabItem) return;

        using var scrollChild = ImRaii.Child("scrollArea", ImGuiHelpers.ScaledVector2(0, SaveDiscardOffset), false, ImGuiWindowFlags.HorizontalScrollbar);
        if (!scrollChild) return;

        ImGui.TextColored(ImGuiColors.DalamudGrey, "What to search");

        ImGui.Columns(2);
        ImGui.SetColumnWidth(0, 300 + 5 * ImGuiHelpers.GlobalScale);
        ImGui.SetColumnWidth(1, 200 + 5 * ImGuiHelpers.GlobalScale);

        ImGui.Separator();

        ImGui.Text("Search order");
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(
            "When using the default \"Simple\" search mode, results will appear in the order defined " +
            "below.\n\n" +
            "For fuzzy search modes, results appear in 'best match' to 'worst match' order, and " +
            "the order defined below will be used only for tie-breaks (when multiple results match " +
            "equally well).\n\n" +
            "Un-ticking a check box will cause all entries from that category to be ignored in any mode.");
        ImGui.NextColumn();

        ImGui.Text("Weight");
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(
            "The weight setting for each category can be used to adjust the internal match score " +
            "calculated for results when using fuzzy search modes. When compared to the default weight " +
            "of 100, for example, a category with a weight of 200 will have its match scores doubled " +
            "while a category with a weight of 50 will have them halved.\n\n" +
            "Search results with higher weightings tend to have higher match scores, and therefore " +
            "appear higher in the results list.\n\n" +
            "Weights are ignored when using the \"Simple\" search mode.");
        ImGui.NextColumn();

        ImGui.Separator();

        for (var i = 0; i < order.Count; i++) {
            var search = order[i];

            var name = search switch {
                Configuration.SearchSetting.Duty => "Duties",
                Configuration.SearchSetting.Aetheryte => "Aetherytes",
                Configuration.SearchSetting.Coordinates => "Coordinates",
                Configuration.SearchSetting.MainCommand => "Commands",
                Configuration.SearchSetting.ExtraCommand => "Extra Commands",
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
                Configuration.SearchSetting.FashionAccessories => "Fashion Accessories",
                Configuration.SearchSetting.Facewear => "Facewear",
                Configuration.SearchSetting.Collection => "Collection",
                _ => null,
            };

            if (name == null) {
                continue;
            }

            var isRequired =
                search is Configuration.SearchSetting.Internal or Configuration.SearchSetting.MacroLinks;

            if (IconButtonEnabledWhen(i != 0, FontAwesomeIcon.ArrowUp, $"{search}")) {
                (order[i], order[i - 1]) = (order[i - 1], order[i]);
            }

            ImGui.SameLine();

            if (IconButtonEnabledWhen(i != order.Count - 1, FontAwesomeIcon.ArrowDown, $"{search}")) {
                (order[i], order[i + 1]) = (order[i + 1], order[i]);
            }

            ImGui.SameLine();

            if (isRequired) {
                CheckboxLocked($"Search in {name}");
            } else {
                ImGui.CheckboxFlags($"Search in {name}", ref flags, (uint)search);
            }

            ImGui.NextColumn();

            using (ImRaii.ItemWidth(120)) {
                var weight = searchWeights.GetValueOrDefault(search, SearchModule.DefaultWeight);
                if (ImGui.InputInt($"##{search}-weight", ref weight, SearchModule.DefaultWeight / 10, SearchModule.DefaultWeight)) {
                    if (weight is > 0 and < SearchModule.DefaultWeight * 1000) {
                        if (weight == SearchModule.DefaultWeight) {
                            searchWeights.Remove(search);
                        } else {
                            searchWeights[search] = weight;
                        }
                    }
                }
            }

            ImGui.Separator();
            ImGui.NextColumn();
        }

        ImGui.CheckboxFlags("Mathematical Expressions", ref flags,
            (uint)Configuration.SearchSetting.Maths);
        ImGui.Separator();

        ImGui.Columns();
    }

    private static void CheckboxLocked(string text) {
        using (new ImRaii.Color()
                   .Push(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled))
                   .Push(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.FrameBg))
                   .Push(ImGuiCol.ButtonActive, ImGui.GetColorU32(ImGuiCol.FrameBgActive))
                   .Push(ImGuiCol.ButtonHovered, ImGui.GetColorU32(ImGuiCol.FrameBgHovered))) {
            ImGuiComponents.IconButton(FontAwesomeIcon.Lock, new Vector2(ImGui.GetFrameHeight(), ImGui.GetFrameHeight()));
        }
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetCursorPos().X - ImGui.GetStyle().ItemSpacing.X + ImGui.GetStyle().ItemInnerSpacing.X);
        ImGui.Text(text);
    }
}
