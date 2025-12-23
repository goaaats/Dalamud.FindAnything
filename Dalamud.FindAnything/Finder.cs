using Dalamud.Bindings.ImGui;
using Dalamud.FindAnything.Lookup;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using Serilog;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;

namespace Dalamud.FindAnything;

public sealed class Finder : IDisposable
{
    private const int MaxOnePage = 10;
    private const int SelectionScrollOffset = 1;

    public static Finder Instance { get; private set; } = null!;

    public readonly SearchState SearchState;
    public readonly RootLookup RootLookup;

    private readonly CursorController cursorControl;
    private ISearchResult[] results = [];

    private bool finderOpen;
    private int selectedIndex;

    private int framesSinceLastKbChange = 0;
    private long lastButtonPressTicks = 0;
    private bool isHeldTimeout = false;
    private bool isHeld = false;

    public bool IsOpen => finderOpen;

    public Finder() {
        Instance = this;

        SearchState = new SearchState(FindAnythingPlugin.Configuration, FindAnythingPlugin.Normalizer);
        RootLookup = new RootLookup();
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
        if (finderOpen)
            return;

        if (openToWiki || FindAnythingPlugin.Configuration.OnlyWikiMode) {
            SetLookupType(LookupType.Wiki);
        } else {
            SetLookupType(LookupType.Module);
        }

        FindAnythingPlugin.GameStateCache.Refresh();

        RootLookup.OnOpen();
        UpdateSearch("");
        finderOpen = true;
    }

    public void Close() {
        finderOpen = false;
        isHeld = false;
        selectedIndex = 0;
        SearchState.Reset();
        results = [];
    }

    private void SetLookupType(LookupType type) {
        RootLookup.SetType(type);
    }

    private void UpdateSearch(string term) {
        selectedIndex = 0;
        SearchState.Set(RootLookup.CurrentType(), term);
        results = GetResults(SearchState.Criteria());
    }

    public void SwitchLookupType(LookupType newType) {
        RootLookup.SetType(newType);
        UpdateSearch("");
        Service.Log.Information($"{nameof(SwitchLookupType)}: {newType}");
    }

    private ISearchResult[] GetResults(SearchCriteria criteria) {
        var lookupResult = RootLookup.Lookup(criteria);
        if (criteria.MatchMode != MatchMode.Simple && lookupResult.AllowSort) {
            return lookupResult.Results.OrderByDescending(r => r.Score).ToArray();
        }

        return lookupResult.Results.ToArray();
    }

    private void Draw() {
        if (!finderOpen)
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

        ImGui.PushItemWidth(size.X - iconSize.Y - windowPadding - ImGui.GetStyle().FramePadding.X - ImGui.GetStyle().ItemSpacing.X);

        var resetScroll = false;

        var searchInput = SearchState.RawString;
        if (ImGui.InputTextWithHint("###findeverythinginput", RootLookup.GetPlaceholder(), ref searchInput, 1000,
                ImGuiInputTextFlags.NoUndoRedo)) {
            UpdateSearch(searchInput);
            framesSinceLastKbChange = 0;
            resetScroll = true;
        }

        ImGui.PopItemWidth();

        if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && !ImGui.IsAnyItemActive() && !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            ImGui.SetKeyboardFocusHere(-1);

        ImGui.SameLine();

        using (var _ = ImRaii.PushFont(UiBuilder.IconFont)) {
            ImGui.Text(FontAwesomeIcon.Search.ToIconString());
        }

        if (!ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) || ImGui.IsKeyDown(ImGuiHelpers.VirtualKeyToImGuiKey(VirtualKey.ESCAPE))) {
            Log.Verbose("Focus loss or escape");
            closeFinder = true;
        }

        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(8 * ImGuiHelpers.GlobalScale, scaledFour));
        style.Push(ImGuiStyleVar.ItemInnerSpacing, new Vector2(scaledFour, scaledFour));

        if (results is not { Length: > 0 })
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
            _ => ImGuiHelpers.VirtualKeyToImGuiKey(FindAnythingPlugin.Configuration.QuickSelectKey)
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

            if (i == selectedIndex - SelectionScrollOffset) {
                selectedScrollPos = ImGui.GetCursorPosY();
            }

            var selectableFlags = ImGuiSelectableFlags.None;
            var disableMouse = FindAnythingPlugin.Configuration.DisableMouseSelection && !isQuickSelect;
            if (disableMouse) {
                selectableFlags = ImGuiSelectableFlags.Disabled;
                ImGui.PushStyleVar(ImGuiStyleVar.DisabledAlpha, 1f);
            }

            if (ImGui.Selectable($"{result.Name}###faEntry{i}", i == selectedIndex, selectableFlags, selectableSize)) {
                Log.Information("Selectable click");
                clickedIndex = i;
            }

            if (disableMouse)
                ImGui.PopStyleVar();

            var thisTextSize = ImGui.CalcTextSize(result.Name);

            ImGui.SameLine(thisTextSize.X + scaledFour);

            ImGui.TextColored(ImGuiColors.DalamudGrey, result.CatName);
            // ImGui.TextColored(ImGuiColors.DalamudGrey, result.Score.ToString());

            if (i < 9 && FindAnythingPlugin.Configuration.QuickSelectKey != VirtualKey.NO_KEY) {
                ImGui.SameLine(size.X - iconSize.X * 1.75f - scrollbarWidth - windowPadding);
                ImGui.TextColored(ImGuiColors.DalamudGrey, (i + 1).ToString());
            }

            if (result.Icon != null) {
                ImGui.SameLine(size.X - iconSize.X - scrollbarWidth - windowPadding);
                ImGui.Image(result.Icon.GetWrapOrEmpty().Handle, iconSize);
            }
        }

        if (selectedIndex > 0) {
            ImGui.SetScrollY(selectedScrollPos);
        } else {
            ImGui.SetScrollY(0);
        }

        if (isQuickSelect && numKeysPressed.Any(x => x)) {
            clickedIndex = Array.IndexOf(numKeysPressed, true);
        }

        if (ImGui.IsKeyPressed(ImGuiHelpers.VirtualKeyToImGuiKey(VirtualKey.RETURN)) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter) || clickedIndex != -1) {
            var index = clickedIndex == -1 ? selectedIndex : clickedIndex;

            if (index < results.Length) {
                var result = results[index];
                closeFinder = result.CloseFinder;
                result.Selected();
                RootLookup.OnSelected(SearchState.Criteria(), result);
            }
        }
    }

    private class CursorController
    {
        private readonly Finder finder;
        private ActionRepeater up;
        private ActionRepeater dn;
        private ActionRepeater pgUp;
        private ActionRepeater pgDn;

        public CursorController(Finder finder) {
            this.finder = finder;

            Configure(FindAnythingPlugin.Configuration);
            FindAnythingPlugin.ConfigManager.OnChange += Configure;
        }

        [MemberNotNull(nameof(up), nameof(dn), nameof(pgUp), nameof(pgDn))]
        private void Configure(Configuration config) {
            Service.Log.Debug($"Configuring {nameof(CursorController)}");

            const int fastScrollWaitTicks = 120;
            var scrollSpeedTicks = config.Speed switch {
                Configuration.ScrollSpeed.Slow => 120,
                Configuration.ScrollSpeed.Medium => 65,
                Configuration.ScrollSpeed.Fast => 30,
                _ => throw new ArgumentOutOfRangeException($"Unknown ScrollSpeed: {config.Speed}"),
            };

            var linePolicy = new RepeatPolicy(fastScrollWaitTicks, scrollSpeedTicks);
            var pagePolicy = new RepeatPolicy(200, 200);
            up = new ActionRepeater(linePolicy, CursorUp);
            dn = new ActionRepeater(linePolicy, CursorDown);
            pgUp = new ActionRepeater(pagePolicy, PageUp);
            pgDn = new ActionRepeater(pagePolicy, PageDown);
        }

        public void ProcessInput() {
            var ticks = Environment.TickCount;
            up.Update(ImGui.IsKeyDown(ImGuiHelpers.VirtualKeyToImGuiKey(VirtualKey.UP)), ticks);
            dn.Update(ImGui.IsKeyDown(ImGuiHelpers.VirtualKeyToImGuiKey(VirtualKey.DOWN)), ticks);
            pgUp.Update(ImGui.IsKeyDown(ImGuiHelpers.VirtualKeyToImGuiKey(VirtualKey.PRIOR)), ticks);
            pgDn.Update(ImGui.IsKeyDown(ImGuiHelpers.VirtualKeyToImGuiKey(VirtualKey.NEXT)), ticks);
        }

        private void CursorDown() {
            if (finder.selectedIndex != finder.results.Length - 1) {
                finder.selectedIndex++;
            } else {
                finder.selectedIndex = 0;
            }
        }

        private void CursorUp() {
            if (finder.selectedIndex != 0) {
                finder.selectedIndex--;
            } else {
                finder.selectedIndex = finder.results.Length - 1;
            }
        }

        private void PageUp() {
            finder.selectedIndex = Math.Max(0, finder.selectedIndex - MaxOnePage);
        }

        private void PageDown() {
            finder.selectedIndex = Math.Min(finder.results.Length - 1, finder.selectedIndex + MaxOnePage);
        }
    }
}
