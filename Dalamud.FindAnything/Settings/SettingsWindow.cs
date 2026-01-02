using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Dalamud.FindAnything.Settings;

public partial class SettingsWindow : Window
{
    private DateTime? finderOffsetChangeTime;
    private const int SaveDiscardOffset = -40;

    private readonly FilterCombo<VirtualKey> virtualKeyFilterCombo =
        new FilterCombo<VirtualKey>(Enum.GetValues<VirtualKey>().Where(x => x != VirtualKey.LBUTTON).ToArray())
            .WithRenderer(k => k.GetFancyName());

    public SettingsWindow() : base("Wotsit Settings") {
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(860, 660),
            MaximumSize = new Vector2(10000, 10000),
        };
    }

    public override void OnOpen() {
        macroRearrangeMode = false;
        CopyConfigToWindow(FindAnythingPlugin.Configuration);
        base.OnOpen();
    }

    public override void Draw() {
        using (var tabBar = ImRaii.TabBar("##mainTabs")) {
            if (tabBar) {
                DrawGeneralTab();
                DrawSearchTab();
                DrawMacrosTab();
            }
        }

        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(2);

        DrawSaveFooter();

        if (finderOffsetChangeTime is { } changeTime) {
            // Use the same positioning and size logic as the main window
            var textSize = ImGui.CalcTextSize("poop");
            var size = new Vector2(500 * ImGuiHelpers.GlobalScale,
                textSize.Y + ImGui.GetStyle().FramePadding.Y * 2 + ImGui.GetStyle().WindowPadding.Y * 2);

            var mainViewportSize = ImGuiHelpers.MainViewport.Size;
            var mainViewportMiddle = mainViewportSize / 2;
            var startPos = ImGuiHelpers.MainViewport.Pos + (mainViewportMiddle - (size / 2));
            startPos.Y -= 200;
            startPos += posOffset;

            ImGui.GetForegroundDrawList()
                .AddRect(startPos, startPos + size, ImGui.ColorConvertFloat4ToU32(ImGuiColors.ParsedGreen));

            if ((DateTime.UtcNow - changeTime).TotalSeconds > 3)
                finderOffsetChangeTime = null;
        }
    }

    private void VirtualKeySelect(string text, ref VirtualKey chosen) {
        virtualKeyFilterCombo.Draw(text, ref chosen);
    }

    private static bool IconButtonEnabledWhen(bool enabled, FontAwesomeIcon icon, string id, string tooltip = "") {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.5f, !enabled);
        var result = ImGuiComponents.IconButton(id, icon);
        DrawHoverTooltip(tooltip);
        return result && enabled;
    }
}

public class FilterCombo<T>
{
    private readonly T[] items;
    private readonly List<int> matchingIndexes = [];
    private readonly HashSet<uint> openIds = [];

    private Func<T, string?> renderer = value => value?.ToString() ?? string.Empty;

    private string filter = "";
    private int selectedMatchIndex = -1;

    public FilterCombo(T[] items) {
        this.items = items;
    }

    private void Reset() {
        filter = "";
        matchingIndexes.Clear();
        matchingIndexes.TrimExcess();
    }

    public FilterCombo<T> WithRenderer(Func<T, string?> func) {
        renderer = func;
        return this;
    }

    private string Render(T obj) => renderer.Invoke(obj) ?? string.Empty;

    private bool IsVisible(int globalIndex, string substring) {
        return Render(items[globalIndex]).Contains(substring, StringComparison.InvariantCultureIgnoreCase);
    }

    // ReSharper disable once UnusedMethodReturnValue.Global
    public bool Draw(string label, ref T currentItem, ImGuiComboFlags flags = ImGuiComboFlags.None) {
        var index = Array.IndexOf(items, currentItem);
        if (Draw(label, Render(currentItem), ref index, flags)) {
            currentItem = items[index];
            return true;
        }
        return false;
    }

    private bool Draw(string label, string preview, ref int currentIndex, ImGuiComboFlags flags = ImGuiComboFlags.None) {
        var id = ImGui.GetID(label);
        using var combo = ImRaii.Combo(label, preview, flags | ImGuiComboFlags.HeightLarge);

        if (!combo) {
            if (openIds.Remove(id)) {
                Service.Log.Warning("Resetting closed popup {Id} for {Label}.", id, label);
                Reset();
            }
            return false;
        }

        openIds.Add(id);

        var width = ImGui.GetWindowWidth() - 2 * ImGui.GetStyle().FramePadding.X;

        var filterDirty = false;
        var setScroll = false;

        if (ImGui.IsWindowAppearing()) {
            ImGui.SetKeyboardFocusHere();
            filterDirty = true;
            selectedMatchIndex = -1;
            setScroll = true;
        }

        ImGui.SetNextItemWidth(width);
        var filterInput = filter;
        if (ImGui.InputTextWithHint("##search", "Search...", ref filterInput, 100)) {
            if (filter != filterInput) {
                filter = filterInput;
                filterDirty = true;
            }
        }

        if (filterDirty) {
            var prevSelectedIndex = selectedMatchIndex >= 0 ? matchingIndexes[selectedMatchIndex] : -1;
            var selectedMatchIndexFromData = -1;
            var selectedMatchIndexFromPrevious = -1;

            selectedMatchIndex = -1;
            matchingIndexes.Clear();
            for (var idx = 0; idx < items.Length; ++idx) {
                if (IsVisible(idx, filter)) {
                    if (idx == currentIndex) {
                        selectedMatchIndexFromData = matchingIndexes.Count;
                    } else if (idx == prevSelectedIndex) {
                        selectedMatchIndexFromPrevious = matchingIndexes.Count;
                    }
                    matchingIndexes.Add(idx);
                }
            }

            if (selectedMatchIndexFromPrevious >= 0)
                selectedMatchIndex = selectedMatchIndexFromPrevious;
            else if (selectedMatchIndexFromData >= 0)
                selectedMatchIndex = selectedMatchIndexFromData;
        }

        if (matchingIndexes.Count > 0) {
            if (ImGui.IsKeyPressed(ImGuiKey.DownArrow)) {
                selectedMatchIndex = (selectedMatchIndex + 1) % matchingIndexes.Count;
                setScroll = true;
            } else if (ImGui.IsKeyPressed(ImGuiKey.UpArrow)) {
                selectedMatchIndex = (selectedMatchIndex - 1 + matchingIndexes.Count) % matchingIndexes.Count;
                setScroll = true;
            }
        }

        var selection = -1;
        var closePopup = false;

        if (ImGui.IsKeyPressed(ImGuiKey.Enter)) {
            if (selectedMatchIndex >= 0) {
                selection = matchingIndexes[selectedMatchIndex];
            } else if (matchingIndexes.Count > 0)
                selection = matchingIndexes[0];
            closePopup = true;
        } else if (ImGui.IsKeyPressed(ImGuiKey.Escape)) {
            closePopup = true;
        }

        void DrawSelectable(int globalIdx, int localIdx) {
            using var selectableId = ImRaii.PushId(globalIdx);
            var selected = selectedMatchIndex >= 0 && matchingIndexes[selectedMatchIndex] == globalIdx;
            var name = Render(items[globalIdx]);
            if (ImGui.Selectable(name, selected)) {
                selection = globalIdx;
                closePopup = true;
            }
        }

        var itemHeight = ImGui.GetTextLineHeightWithSpacing();
        var height = itemHeight * 12 - ImGui.GetFrameHeight() - ImGui.GetStyle().WindowPadding.Y;
        using (var _ = ImRaii.Child("filter-combo-lines", new Vector2(width, height))) {
            using var indent = ImRaii.PushIndent(ImGuiHelpers.GlobalScale);
            if (setScroll)
                ImGui.SetScrollFromPosY(selectedMatchIndex * itemHeight - ImGui.GetScrollY());
            ImGuiClip.ClippedDraw(matchingIndexes, DrawSelectable, itemHeight);
        }

        if (closePopup) {
            ImGui.CloseCurrentPopup();
            openIds.Remove(id);
            Reset();
        }

        if (selection >= 0) {
            currentIndex = selection;
            return true;
        }

        return false;
    }
}