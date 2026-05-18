using Dalamud.Bindings.ImGui;
using Dalamud.FindAnything.Lookup;
using Dalamud.FindAnything.Modules;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Dalamud.FindAnything.Settings;

public partial class SettingsWindow {
    private string selectedPluginName = "";
    private string searchString = "";

    private void DrawIpcTab() {
        if (FindAnythingPlugin.Ipc.TrackedIpcs.Count == 0) return;

        using var tabItem = ImRaii.TabItem("Other plugins");
        if (!tabItem) return;

        using var scrollChild = ImRaii.Child("ipcScrollChild", Vector2.Zero, false, ImGuiWindowFlags.HorizontalScrollbar);
        if (!scrollChild) return;

        using var table = ImRaii.Table("BurdensLayout", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV);
        if (!table) return;

        ImGui.TableSetupColumn("Burdens", ImGuiTableColumnFlags.WidthFixed, 250);
        ImGui.TableSetupColumn("Details", ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        ImGui.TextColored(ImGuiColors.DalamudGrey, "Plugin");
        ImGui.Separator();

        DrawIpcSelection();

        ImGui.TableNextColumn();

        DrawIpcDetail();
    }

    private void DrawIpcSelection() {
        using var child = ImRaii.Child("ipcSelectionChild", new Vector2(-1, -2), false);
        if (!child) return;

        foreach (var ipcTrackedIpc in FindAnythingPlugin.Ipc.TrackedIpcs) {
            var isSelected = selectedPluginName == ipcTrackedIpc.Key;
            if (ImGui.Selectable($"{ipcTrackedIpc.Key}##selectable", isSelected, ImGuiSelectableFlags.AllowDoubleClick)) {
                selectedPluginName = ipcTrackedIpc.Key;
            }
        }
    }

    private void DrawIpcDetail() {
        if (selectedPluginName.Length == 0) {
            ImGui.Text("Please select a plugin");
            return;
        }

        if (!FindAnythingPlugin.Ipc.TrackedIpcs.TryGetValue(selectedPluginName, out var bindings)) {
            selectedPluginName = "";
            return;
        }

        DrawIpcDetailConfig();

        ImGui.Spacing();

        DrawIpcDetailBindings(bindings);
    }

    private void DrawIpcDetailConfig() {
        if (!ipcConfigs.TryGetValue(selectedPluginName, out var ipcConfig)) {
            ipcConfig = new Configuration.IpcConfig();
        }

        var changed = false;

        var enabled = ipcConfig.Enabled;
        if (ImGui.Checkbox("Enable", ref enabled)) {
            ipcConfig.Enabled = enabled;
            changed = true;
        }

        var overrideWeight = ipcConfig.OverrideWeight != null;
        var overrideWeightValue = ipcConfig.OverrideWeight ?? searchWeights.GetValueOrDefault(Configuration.SearchSetting.PluginSettings, SearchModule.DefaultWeight);
        if (ImGui.Checkbox("Override weight", ref overrideWeight)) {
            ipcConfig.OverrideWeight = overrideWeight ? overrideWeightValue : null;
            changed = true;
        }
        ImGui.SameLine();
        using (ImRaii.Disabled(!overrideWeight)) {
            var weight = overrideWeightValue;
            using (ImRaii.ItemWidth(120 * ImGuiHelpers.GlobalScale)) {
                if (ImGui.InputInt($"##weight", ref weight, SearchModule.DefaultWeight / 10, SearchModule.DefaultWeight)) {
                    if (weight is > 0 and < SearchModule.DefaultWeight * 1000) {
                        ipcConfig.OverrideWeight = weight;
                        changed = true;
                    }
                }
            }
        }

        if (changed) {
            ipcConfigs[selectedPluginName] = ipcConfig;
        }
    }

    private void DrawIpcDetailBindings(List<IpcSystem.IpcBinding> bindings) {
        var searchStringRef = searchString;
        if (ImGui.InputText("Search", ref searchStringRef, 256)) {
            searchString = searchStringRef;
        }

        var searchState = new SearchState(FindAnythingPlugin.Configuration, FindAnythingPlugin.Normalizer);
        searchState.Set(LookupType.Module, searchString);
        var criteria = searchState.Criteria();
        var normalizer = FindAnythingPlugin.Normalizer.WithKana(criteria.ContainsKana);
        var matcher = new FuzzyMatcher(criteria.MatchString, criteria.MatchMode);

        ImGuiComponents.HelpMarker("Searches via \"Search text\" (same as Wotsit search).");

        using var table = ImRaii.Table("ipcTable", 3, ImGuiTableFlags.Resizable | ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY, new Vector2(-1, -2));
        if (!table) return;

        ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
        ImGui.TableSetupColumn("Display name", ImGuiTableColumnFlags.WidthFixed, 250);
        ImGui.TableSetupColumn("Search text", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupScrollFreeze(0, 1);

        ImGui.TableHeadersRow();

        var sortedBindings = bindings.OrderBy(x => x.Display).ToList();
        foreach (var binding in sortedBindings) {
            if (criteria.HasMatchString() && matcher.Matches(normalizer.Searchable(binding.Search)) < 1) {
                continue;
            }

            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            if (Service.TextureProvider.GetFromGameIcon(new GameIconLookup(binding.IconId)) is { } texture) {
                using var wrap = texture.GetWrapOrEmpty();
                ImGui.Image(wrap.Handle, new Vector2(ImGui.GetFrameHeight(), ImGui.GetFrameHeight()));
            }
            ImGui.TableNextColumn();
            ImGui.Text(binding.Display);
            ImGui.TableNextColumn();
            ImGui.Text(binding.Search);
            if (ImGui.IsItemHovered() && ImGui.GetItemRectSize().X > ImGui.GetColumnWidth()) {
                ImGui.SetNextWindowSize(new Vector2(300 * ImGuiHelpers.GlobalScale, -1));
                using (ImRaii.Tooltip()) {
                    ImGui.TextWrapped(binding.Search);
                }
            }
        }
    }
}
