using Dalamud.Bindings.ImGui;
using Dalamud.FindAnything.Lookup;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Serilog;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;

namespace Dalamud.FindAnything;

public sealed class Finder : IDisposable {
    private const int MaxOnePage = 10;
    private const int SelectionScrollOffset = 1;

    public bool IsOpen { get; private set; }

    private readonly RootLookup rootLookup;
    private readonly SearchState searchState;
    private readonly CursorController cursorControl;

    private ISearchResult[] results = [];

    private int SelectedIndex {
        get;
        set {
            if (field != value) {
                resetScroll = true;
            }
            field = value;
        }
    }

    private bool resetScroll;

    public Finder(RootLookup rootLookup, Normalizer normalizer) {
        this.rootLookup = rootLookup;
        searchState = new SearchState(FindAnythingPlugin.Configuration, normalizer);
        cursorControl = new CursorController(this);

        Service.PluginInterface.UiBuilder.Draw += Draw;
    }

    public void Dispose() {
        Service.PluginInterface.UiBuilder.Draw -= Draw;
    }

    public void Open(bool openToWiki = false) {
#if !DEBUG
        if (!Service.ClientState.IsLoggedIn)
            return;
#endif
        if (IsOpen)
            return;

        FindAnythingPlugin.GameStateCache.Refresh();

        if (openToWiki || FindAnythingPlugin.Configuration.OnlyWikiMode) {
            rootLookup.SetBase(LookupType.Wiki);
        } else {
            rootLookup.SetBase(LookupType.Module);
        }

        rootLookup.OnOpen();
        resetScroll = true;
        UpdateSearch("");
        IsOpen = true;
    }

    public void Close() {
        IsOpen = false;
        SelectedIndex = 0;
        searchState.Reset();
        results = [];
    }

    private void UpdateSearch(string term) {
        SelectedIndex = 0;
        searchState.Set(rootLookup.GetBaseType(), term);

        var criteria = searchState.Criteria();
        if (criteria.OverrideLookupType is { } overrideType) {
            rootLookup.SetOverride(overrideType);
        } else {
            rootLookup.ClearOverride();
        }

        results = GetResults(searchState.Criteria());
    }

    public void SwitchLookupType(LookupType type) {
        rootLookup.SetBase(type);
        UpdateSearch("");
        Service.Log.Debug($"{nameof(SwitchLookupType)}: {type}");
    }

    private ISearchResult[] GetResults(SearchCriteria criteria) {
        var lookupResult = rootLookup.Lookup(criteria);
        if (criteria.MatchMode != MatchMode.Simple && lookupResult.AllowSort) {
            return lookupResult.Results.OrderByDescending(r => r.Score).ToArray();
        }

        return lookupResult.Results.ToArray();
    }

    private void Draw() {
        if (!IsOpen)
            return;

        ImGuiHelpers.ForceNextWindowMainViewport();

        var textSize = ImGui.CalcTextSize("poop");
        var size = new Vector2(500 * ImGuiHelpers.GlobalScale,
            textSize.Y + ImGui.GetStyle().FramePadding.Y * 2 + ImGui.GetStyle().WindowPadding.Y * 2);

        var mainViewportSize = ImGuiHelpers.MainViewport.Size;
        var mainViewportMiddle = mainViewportSize / 2;
        var startPos = ImGuiHelpers.MainViewport.Pos + (mainViewportMiddle - (size / 2));

        startPos.Y -= 200;
        startPos += FindAnythingPlugin.Configuration.PositionOffset;

        var scaledFour = 4 * ImGuiHelpers.GlobalScale;
        var iconSize = textSize with { X = textSize.Y };
        var scrollbarWidth = ImGui.GetStyle().ScrollbarSize + 2;
        var windowPadding = ImGui.GetStyle().WindowPadding.X * 2;

        if (results is { Length: > 0 }) {
            size.Y += Math.Min(results.Length, MaxOnePage) * (float.Floor(textSize.Y) + float.Floor(scaledFour));
            size.Y -= float.Floor(scaledFour / 2);
            size.Y += ImGui.GetStyle().ItemSpacing.Y;
        }

        ImGui.SetNextWindowPos(startPos);
        ImGui.SetNextWindowSize(size);
        ImGui.SetNextWindowSizeConstraints(size, size with { Y = size.Y + (400 * ImGuiHelpers.GlobalScale) });

        var closeFinder = false;
        if (ImGui.Begin("###findeverything",
                ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)) {
            try {
                DrawFinder(size, iconSize, textSize, windowPadding, scrollbarWidth, scaledFour, out closeFinder);
            } catch (Exception ex) {
                Log.Error(ex, "Error in DrawFinder");

                using var color = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
                ImGui.Text("Could render Wotsit UI. Please report this error.");
            }
        }

        ImGui.End();

        if (closeFinder) {
            Close();
        }
    }

    private void DrawFinder(Vector2 size, Vector2 iconSize, Vector2 textSize, float windowPadding, float scrollbarWidth, float scaledFour, out bool closeFinder) {
        closeFinder = false;

        using (ImRaii.ItemWidth(size.X - iconSize.Y - windowPadding - ImGui.GetStyle().FramePadding.X - ImGui.GetStyle().ItemSpacing.X)) {
            var searchInput = searchState.RawString;
            if (ImGui.InputTextWithHint("###findeverythinginput", rootLookup.GetPlaceholder(), ref searchInput, 1000,
                    ImGuiInputTextFlags.NoUndoRedo)) {
                UpdateSearch(searchInput);
            }
        }

        if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && !ImGui.IsAnyItemActive() && !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            ImGui.SetKeyboardFocusHere(-1);

        ImGui.SameLine();

        using (ImRaii.PushFont(UiBuilder.IconFont)) {
            var icon = rootLookup.GetActiveType() switch {
                LookupType.Module => FontAwesomeIcon.Search,
                LookupType.Wiki => FontAwesomeIcon.PuzzlePiece,
                LookupType.WikiSite => FontAwesomeIcon.PuzzlePiece,
                LookupType.EmoteMode => FontAwesomeIcon.HandsClapping,
                LookupType.Facewear => FontAwesomeIcon.Glasses,
                LookupType.CoordinateAction => FontAwesomeIcon.Flag,
                LookupType.CraftingRecipe => FontAwesomeIcon.Hammer,
                _ => FontAwesomeIcon.Star,
            };
            ImGui.Text(icon.ToIconString());
        }

        if (!ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) || ImGui.IsKeyDown(ImGuiHelpers.VirtualKeyToImGuiKey(VirtualKey.ESCAPE))) {
            Log.Verbose("Focus loss or escape");
            closeFinder = true;
        }

        using var style = new ImRaii.Style()
            .Push(ImGuiStyleVar.ItemSpacing, new Vector2(8 * ImGuiHelpers.GlobalScale, scaledFour))
            .Push(ImGuiStyleVar.ItemInnerSpacing, new Vector2(scaledFour, scaledFour));

        if (results.Length == 0)
            return;

        using var child = ImRaii.Child("###findAnythingScroller");
        if (!child)
            return;

        var childSize = ImGui.GetWindowSize();
        var selectableSize = new Vector2(childSize.X, textSize.Y);

        var quickSelectModifierKey = FindAnythingPlugin.Configuration.QuickSelectKey switch {
            VirtualKey.CONTROL => ImGuiKey.ModCtrl,
            VirtualKey.MENU => ImGuiKey.ModAlt,
            VirtualKey.SHIFT => ImGuiKey.ModShift,
            _ => ImGuiHelpers.VirtualKeyToImGuiKey(FindAnythingPlugin.Configuration.QuickSelectKey),
        };
        var isQuickSelect = ImGui.IsKeyDown(quickSelectModifierKey);

        cursorControl.ProcessInput();

        var numKeysPressed = new bool[10];
        for (var i = 0; i < 9; i++) {
            numKeysPressed[i] = ImGui.IsKeyPressed(ImGuiKey.Key1 + i);
        }

        var clickedIndex = -1;
        var selectedScrollPos = 0f;

        for (var i = 0; i < results.Length; i++) {
            var result = results[i];

            if (i == SelectedIndex - SelectionScrollOffset) {
                selectedScrollPos = ImGui.GetCursorPosY();
            }

            var selectableFlags = ImGuiSelectableFlags.None;
            var disableMouse = FindAnythingPlugin.Configuration.DisableMouseSelection && !isQuickSelect;
            if (disableMouse) {
                selectableFlags = ImGuiSelectableFlags.Disabled;
                ImGui.PushStyleVar(ImGuiStyleVar.DisabledAlpha, 1f);
            }

            var nameTextSize = ImGui.CalcTextSize(result.Name).X;

            // Debug scores
            // var scoreText = result.Score.ToString();
            // ImGui.TextColored(ImGuiColors.HealerGreen, scoreText);
            // nameTextSize += ImGui.CalcTextSize(scoreText).X + scaledFour * 2;
            // ImGui.SameLine();

            if (ImGui.Selectable($"{result.Name}###faEntry{i}", i == SelectedIndex, selectableFlags, selectableSize)) {
                clickedIndex = i;
            }

            if (disableMouse)
                ImGui.PopStyleVar();

            ImGui.SameLine(nameTextSize + scaledFour);
            ImGui.TextColored(ImGuiColors.DalamudGrey, result.CatName);

            if (i < 9 && FindAnythingPlugin.Configuration.QuickSelectKey != VirtualKey.NO_KEY) {
                ImGui.SameLine(size.X - iconSize.X * 1.75f - scrollbarWidth - windowPadding);
                ImGui.TextColored(ImGuiColors.DalamudGrey, (i + 1).ToString());
            }

            if (result.Icon != null) {
                ImGui.SameLine(size.X - iconSize.X - scrollbarWidth - windowPadding);
                ImGui.Image(result.Icon.GetWrapOrEmpty().Handle, iconSize);
            }
        }

        if (resetScroll) {
            if (SelectedIndex > 0) {
                ImGui.SetScrollY(selectedScrollPos);
            } else {
                ImGui.SetScrollY(0);
            }
            resetScroll = false;
        }

        if (isQuickSelect && numKeysPressed.Any(x => x)) {
            clickedIndex = Array.IndexOf(numKeysPressed, true);
        }

        if (ImGui.IsKeyPressed(ImGuiHelpers.VirtualKeyToImGuiKey(VirtualKey.RETURN)) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter) || clickedIndex != -1) {
            var index = clickedIndex == -1 ? SelectedIndex : clickedIndex;

            if (index < results.Length) {
                var result = results[index];
                closeFinder = result.CloseFinder;
                rootLookup.OnSelected(searchState.Criteria(), result);
                result.Selected();
            }
        }
    }

    private class CursorController {
        private readonly Finder finder;
        private Configuration.CursorControlType controlType;
        private ActionRepeater up;
        private ActionRepeater dn;
        private ActionRepeater pgUp;
        private ActionRepeater pgDn;

        public CursorController(Finder finder) {
            this.finder = finder;

            Configure(FindAnythingPlugin.Configuration);
            FindAnythingPlugin.ConfigManager.OnChange += Configure;
        }

        [MemberNotNull(nameof(controlType), nameof(up), nameof(dn), nameof(pgUp), nameof(pgDn))]
        private void Configure(Configuration config) {
            Service.Log.Debug($"Configuring {nameof(CursorController)}");

            controlType = config.CursorControl;

            var linePolicy = new RepeatPolicy(config.CursorLineRepeatDelay, config.CursorLineRepeatInterval);
            var pagePolicy = new RepeatPolicy(config.CursorPageRepeatDelay, config.CursorPageRepeatInterval);
            up = new ActionRepeater(linePolicy, CursorUp);
            dn = new ActionRepeater(linePolicy, CursorDown);
            pgUp = new ActionRepeater(pagePolicy, PageUp);
            pgDn = new ActionRepeater(pagePolicy, PageDown);
        }

        public void ProcessInput() {
            if (controlType == Configuration.CursorControlType.System) {
                if (ImGui.IsKeyPressed(ImGuiHelpers.VirtualKeyToImGuiKey(VirtualKey.UP))) CursorUp();
                if (ImGui.IsKeyPressed(ImGuiHelpers.VirtualKeyToImGuiKey(VirtualKey.DOWN))) CursorDown();
                if (ImGui.IsKeyPressed(ImGuiHelpers.VirtualKeyToImGuiKey(VirtualKey.PRIOR))) PageUp();
                if (ImGui.IsKeyPressed(ImGuiHelpers.VirtualKeyToImGuiKey(VirtualKey.NEXT))) PageDown();
            } else {
                var ticks = Environment.TickCount;
                up.Update(ImGui.IsKeyDown(ImGuiHelpers.VirtualKeyToImGuiKey(VirtualKey.UP)), ticks);
                dn.Update(ImGui.IsKeyDown(ImGuiHelpers.VirtualKeyToImGuiKey(VirtualKey.DOWN)), ticks);
                pgUp.Update(ImGui.IsKeyDown(ImGuiHelpers.VirtualKeyToImGuiKey(VirtualKey.PRIOR)), ticks);
                pgDn.Update(ImGui.IsKeyDown(ImGuiHelpers.VirtualKeyToImGuiKey(VirtualKey.NEXT)), ticks);
            }
        }

        private void CursorDown() {
            if (finder.SelectedIndex != finder.results.Length - 1) {
                finder.SelectedIndex++;
            } else {
                finder.SelectedIndex = 0;
            }
        }

        private void CursorUp() {
            if (finder.SelectedIndex != 0) {
                finder.SelectedIndex--;
            } else {
                finder.SelectedIndex = finder.results.Length - 1;
            }
        }

        private void PageUp() {
            finder.SelectedIndex = Math.Max(0, finder.SelectedIndex - MaxOnePage);
        }

        private void PageDown() {
            finder.SelectedIndex = Math.Min(finder.results.Length - 1, finder.SelectedIndex + MaxOnePage);
        }
    }
}
