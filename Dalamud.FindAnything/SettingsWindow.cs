using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Utility.Numerics;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;

namespace Dalamud.FindAnything;

public class SettingsWindow : Window
{
    private readonly FindAnythingPlugin plugin;
    private uint flags;
    private Configuration.OpenMode openMode;
    private VirtualKey shiftShiftKey;
    private int shiftShiftDelay;
    private Configuration.DoubleTapUnit shiftShiftUnit;
    private VirtualKey comboModifierKey;
    private VirtualKey comboModifier2Key;
    private VirtualKey comboKey;
    private VirtualKey wikiComboKey;
    private bool preventPassthrough;
    private List<Configuration.MacroEntry> macros = new();
    private Configuration.MacroSearchDirection macroLinksSearch;
    private bool aetheryteGilCost;
    private bool marketBoardShortcut;
    private bool strikingDummyShortcut;
    private bool innRoomShortcut;
    private bool innRoomShortcutNoLimsa;
    private Configuration.EmoteMotionMode emoteMotionMode;
    private bool showEmoteCommand;
    private bool wikiModeNoSpoilers;
    private Dictionary<string, float> constants = new();
    private Vector2 posOffset;
    private bool onlyWiki;
    private VirtualKey quickSelectKey;
    private List<Configuration.SearchSetting> order = new();
    private Dictionary<Configuration.SearchSetting, int> searchWeights = new();
    private Configuration.ScrollSpeed speed;
    private bool notInCombat;
    private bool tcForceBrowser;
    private bool historyEnabled;
    private bool disableMouseSelection;
    private bool openCraftingLogToRecipe;
    private MatchMode matchMode;
    private string matchSigilSimple;
    private string matchSigilFuzzy;
    private string matchSigilFuzzyParts;

    private DateTime? windowOffsetChangeTime;
    private bool macroRearrangeMode;
    private const int SaveDiscardOffset = -40;

    public SettingsWindow(FindAnythingPlugin plugin) : base("Wotsit Settings")
    {
        this.plugin = plugin;

        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(860, 660),
            MaximumSize = new Vector2(10000, 10000),
        };
    }

    public override void OnOpen()
    {
        this.flags = (uint) FindAnythingPlugin.Configuration.ToSearchV3;
        this.openMode = FindAnythingPlugin.Configuration.Open;
        this.shiftShiftKey = FindAnythingPlugin.Configuration.ShiftShiftKey;
        this.shiftShiftDelay = (int) FindAnythingPlugin.Configuration.ShiftShiftDelay;
        this.shiftShiftUnit = FindAnythingPlugin.Configuration.ShiftShiftUnit;
        this.comboKey = FindAnythingPlugin.Configuration.ComboKey;
        this.comboModifierKey = FindAnythingPlugin.Configuration.ComboModifier;
        this.comboModifier2Key = FindAnythingPlugin.Configuration.ComboModifier2;
        this.wikiComboKey = FindAnythingPlugin.Configuration.WikiComboKey;
        this.preventPassthrough = FindAnythingPlugin.Configuration.PreventPassthrough;
        this.macros = FindAnythingPlugin.Configuration.MacroLinks.Select(x => new Configuration.MacroEntry(x)).ToList();
        this.macroLinksSearch = FindAnythingPlugin.Configuration.MacroLinksSearchDirection;
        this.aetheryteGilCost = FindAnythingPlugin.Configuration.DoAetheryteGilCost;
        this.marketBoardShortcut = FindAnythingPlugin.Configuration.AetheryteShortcuts.HasFlag(Configuration.AetheryteAdditionalShortcut.MarketBoard);
        this.strikingDummyShortcut = FindAnythingPlugin.Configuration.AetheryteShortcuts.HasFlag(Configuration.AetheryteAdditionalShortcut.StrikingDummy);
        this.emoteMotionMode = FindAnythingPlugin.Configuration.EmoteMode;
        this.showEmoteCommand = FindAnythingPlugin.Configuration.ShowEmoteCommand;
        this.wikiModeNoSpoilers = FindAnythingPlugin.Configuration.WikiModeNoSpoilers;
        this.constants = FindAnythingPlugin.Configuration.MathConstants;
        this.posOffset = FindAnythingPlugin.Configuration.PositionOffset;
        this.onlyWiki = FindAnythingPlugin.Configuration.OnlyWikiMode;
        this.quickSelectKey = FindAnythingPlugin.Configuration.QuickSelectKey;
        this.order = FindAnythingPlugin.Configuration.Order.ToList();
        this.searchWeights = new Dictionary<Configuration.SearchSetting, int>(FindAnythingPlugin.Configuration.SearchWeights);
        this.speed = FindAnythingPlugin.Configuration.Speed;
        this.notInCombat = FindAnythingPlugin.Configuration.NotInCombat;
        this.tcForceBrowser = FindAnythingPlugin.Configuration.TeamCraftForceBrowser;
        this.historyEnabled = FindAnythingPlugin.Configuration.HistoryEnabled;
        this.disableMouseSelection = FindAnythingPlugin.Configuration.DisableMouseSelection;
        this.openCraftingLogToRecipe = FindAnythingPlugin.Configuration.OpenCraftingLogToRecipe;
        this.matchMode = FindAnythingPlugin.Configuration.MatchMode;
        this.matchSigilSimple = FindAnythingPlugin.Configuration.MatchSigilSimple;
        this.matchSigilFuzzy = FindAnythingPlugin.Configuration.MatchSigilFuzzy;
        this.matchSigilFuzzyParts = FindAnythingPlugin.Configuration.MatchSigilFuzzyParts;

        this.macroRearrangeMode = false;
        base.OnOpen();
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("##find-anything-tabs")) {
            if (ImGui.BeginTabItem("General")) {
                ImGui.BeginChild("ScrollingOthers", ImGuiHelpers.ScaledVector2(0, SaveDiscardOffset), true,
                    ImGuiWindowFlags.HorizontalScrollbar | ImGuiWindowFlags.NoBackground);

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

                        // ImGui.PushItemWidth(200);
                        ImGui.PushItemWidth(ImGui.GetWindowWidth() * 0.2f);

                        if (ImGui.InputInt("", ref shiftShiftDelay)) {
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

                        ImGui.PopItemWidth();
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

                ImGui.TextColored(ImGuiColors.DalamudGrey, "Search mode");
                ImGui.TextWrapped("Use this menu to select the default search mode:\n" +
                                  "  - \"Simple\" looks for the exact text entered.\n" +
                                  "  - \"Fuzzy\" finds close matches to your text even if some characters are missing (e.g. \"dufi\" can locate the Duty Finder).\n" +
                                  "  - \"FuzzyParts\" is like Fuzzy but each word in the input is searched for separately, so that input word order does not matter.");
                ImGui.TextWrapped(
                    "When using fuzzy search modes, results are shown in order from best match to worst match.");

                if (ImGui.BeginCombo("Search mode", this.matchMode.ToString())) {
                    foreach (var key in Enum.GetValues<MatchMode>()) {
                        if (ImGui.Selectable(key.ToString(), key == this.matchMode)) {
                            this.matchMode = key;
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

                ImGui.PushItemWidth(40);
                ImGui.InputText("Simple search mode prefix", ref this.matchSigilSimple, 1);
                ImGui.InputText("Fuzzy search mode prefix", ref this.matchSigilFuzzy, 1);
                ImGui.InputText("FuzzyParts search mode prefix", ref this.matchSigilFuzzyParts, 1);
                ImGui.PopItemWidth();

                ImGuiHelpers.ScaledDummy(15);
                ImGui.Separator();
                ImGuiHelpers.ScaledDummy(15);

                ImGui.TextColored(ImGuiColors.DalamudGrey, "Math constants");
                ImGui.TextWrapped(
                    "Use this menu to tie constants to values, to be used in expressions.\nAdd a constant again to edit it.");

                DrawConstantsSection();

                ImGuiHelpers.ScaledDummy(15);
                ImGui.Separator();
                ImGuiHelpers.ScaledDummy(15);

                ImGui.TextColored(ImGuiColors.DalamudGrey, "Other stuff");

                ImGui.Checkbox("Enable Search History", ref this.historyEnabled);
                ImGui.Checkbox("Show Gil cost in Aetheryte results", ref this.aetheryteGilCost);
                ImGui.Checkbox("Show \"Market Board\" shortcut to teleport to the closest market board city",
                    ref this.marketBoardShortcut);
                ImGui.Checkbox("Show \"Striking Dummy\" shortcut to teleport to the closest striking dummy location",
                    ref this.strikingDummyShortcut);
                ImGui.Checkbox("Show \"Inn Room\" shortcut to teleport to the closest inn room", ref this.innRoomShortcut);
                if (this.innRoomShortcut)
                {
                    ImGui.Checkbox("Don't consider Limsa a valid Inn Room location", ref this.innRoomShortcutNoLimsa);
                }

                if (ImGui.BeginCombo("Emote Motion-Only?", this.emoteMotionMode.ToString())) {
                    foreach (var key in Enum.GetValues<Configuration.EmoteMotionMode>()) {
                        if (ImGui.Selectable(key.ToString(), key == this.emoteMotionMode)) {
                            this.emoteMotionMode = key;
                        }
                    }

                    ImGui.EndCombo();
                }

                ImGui.Checkbox("Show Emote command in search result", ref this.showEmoteCommand);
                ImGui.Checkbox("Try to prevent spoilers in wiki mode(not 100% reliable)", ref this.wikiModeNoSpoilers);
                ImGui.Checkbox("Directly go to wiki mode when opening search", ref this.onlyWiki);
                if (ImGui.SliderFloat2("Search window position offset", ref this.posOffset, -800, 800)) {
                    this.windowOffsetChangeTime = DateTime.UtcNow;
                }
                if (ImGui.BeginCombo("Scroll Speed", this.speed.ToString())) {
                    foreach (var key in Enum.GetValues<Configuration.ScrollSpeed>()) {
                        if (ImGui.Selectable(key.ToString(), key == this.speed)) {
                            this.speed = key;
                        }
                    }

                    ImGui.EndCombo();
                }

                ImGui.Checkbox("Don't open Wotsit in combat", ref this.notInCombat);
                ImGui.Checkbox("Force TeamCraft links to open in your browser", ref this.tcForceBrowser);
                ImGui.Checkbox("Disable mouse selection in results list unless Quick Select Key is held", ref this.disableMouseSelection);
                ImGui.Checkbox("When selecting a crafting recipe, jump to the recipe instead of using Recipe Search", ref this.openCraftingLogToRecipe);

                ImGuiHelpers.ScaledDummy(5);

                // End scrollable container
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("What to search")) {
                ImGui.BeginChild("ScrollingMain", ImGuiHelpers.ScaledVector2(0, SaveDiscardOffset), true,
                    ImGuiWindowFlags.HorizontalScrollbar | ImGuiWindowFlags.NoBackground);
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

                for (var i = 0; i < this.order.Count; i++) {
                    var search = this.order[i];

                    var name = search switch
                    {
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
                        Configuration.SearchSetting.FashionAccessories => "Fashion Accessories",
                        Configuration.SearchSetting.Collection => "Collection",
                        _ => null,
                    };

                    if (name == null) {
                        continue;
                    }

                    var isRequired =
                        search is Configuration.SearchSetting.Internal or Configuration.SearchSetting.MacroLinks;

                    ImGui.PushFont(UiBuilder.IconFont);

                    if (IconButtonEnabledWhen(i != 0, FontAwesomeIcon.ArrowUp, $"{search}")) {
                        (this.order[i], this.order[i - 1]) = (this.order[i - 1], this.order[i]);
                    }

                    ImGui.SameLine();

                    if (IconButtonEnabledWhen(i != this.order.Count - 1, FontAwesomeIcon.ArrowDown, $"{search}")) {
                        (this.order[i], this.order[i + 1]) = (this.order[i + 1], this.order[i]);
                    }

                    ImGui.PopFont();

                    ImGui.SameLine();

                    if (isRequired) {
                        var locked = true;
                        ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
                        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Vector4.Zero);
                        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Zero);
                        ImGui.PushStyleColor(ImGuiCol.CheckMark, ImGuiColors.ParsedGrey);
                        ImGui.Checkbox($"Search in {name}", ref locked);
                        ImGui.PopStyleColor(4);
                    }
                    else {
                        ImGui.CheckboxFlags($"Search in {name}", ref this.flags, (uint)search);
                    }

                    ImGui.NextColumn();

                    ImGui.PushItemWidth(120);
                    var weight = searchWeights.GetValueOrDefault(search, FindAnythingPlugin.DefaultWeight);
                    if (ImGui.InputInt($"##{search}-weight", ref weight, FindAnythingPlugin.DefaultWeight / 10, FindAnythingPlugin.DefaultWeight)) {
                        if (weight is > 0 and < FindAnythingPlugin.DefaultWeight * 1000) {
                            if (weight == FindAnythingPlugin.DefaultWeight) {
                                searchWeights.Remove(search);
                            }
                            else {
                                searchWeights[search] = weight;
                            }
                        }
                    }
                    ImGui.PopItemWidth();

                    ImGui.Separator();
                    ImGui.NextColumn();
                }

                ImGui.CheckboxFlags("Mathematical Expressions", ref this.flags,
                    (uint)Configuration.SearchSetting.Maths);
                ImGui.Separator();

                ImGui.Columns(1);

                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Macro links")) {
                ImGui.BeginChild("ScrollingMain", ImGuiHelpers.ScaledVector2(0, SaveDiscardOffset), true,
                    ImGuiWindowFlags.HorizontalScrollbar | ImGuiWindowFlags.NoBackground);

                ImGui.TextColored(ImGuiColors.DalamudGrey, "Macro links");

                DrawMacrosSection();

                ImGuiHelpers.ScaledDummy(15);
                ImGui.Separator();
                ImGuiHelpers.ScaledDummy(15);

                ImGui.TextColored(ImGuiColors.DalamudGrey, "Search direction");
                ImGui.TextWrapped(
                    "Use this to change the order in which macro links are searched. This may affect which macro links are displayed higher in the result list.");

                if (ImGui.RadioButton("Top to bottom", macroLinksSearch == Configuration.MacroSearchDirection.TopToBottom)) {
                    macroLinksSearch = Configuration.MacroSearchDirection.TopToBottom;
                }

                if (ImGui.RadioButton("Bottom to top", macroLinksSearch == Configuration.MacroSearchDirection.BottomToTop)) {
                    macroLinksSearch = Configuration.MacroSearchDirection.BottomToTop;
                }

                ImGui.EndChild();
                ImGui.EndTabItem();
            }


            ImGui.EndTabBar();
        }

        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(2);

        var save = ImGui.Button("Save");
        ImGui.SameLine();
        var saveAndClose = ImGui.Button("Save and Close");

        if (save || saveAndClose)
        {
            FindAnythingPlugin.Configuration.ToSearchV3 = (Configuration.SearchSetting) this.flags;
            FindAnythingPlugin.Configuration.Order = this.order;
            FindAnythingPlugin.Configuration.SearchWeights = this.searchWeights;

            FindAnythingPlugin.Configuration.Open = openMode;
            FindAnythingPlugin.Configuration.ShiftShiftKey = shiftShiftKey;
            FindAnythingPlugin.Configuration.ShiftShiftDelay = (uint) shiftShiftDelay;
            FindAnythingPlugin.Configuration.ShiftShiftUnit = shiftShiftUnit;

            FindAnythingPlugin.Configuration.ComboKey = comboKey;
            FindAnythingPlugin.Configuration.ComboModifier = comboModifierKey;
            FindAnythingPlugin.Configuration.ComboModifier2 = comboModifier2Key;
            FindAnythingPlugin.Configuration.WikiComboKey = wikiComboKey;
            FindAnythingPlugin.Configuration.PreventPassthrough = preventPassthrough;

            FindAnythingPlugin.Configuration.MacroLinks = this.macros;
            FindAnythingPlugin.Configuration.MacroLinksSearchDirection = this.macroLinksSearch;

            FindAnythingPlugin.Configuration.DoAetheryteGilCost = this.aetheryteGilCost;
            
            var aetheryteShortcuts = Configuration.AetheryteAdditionalShortcut.None;
            if (this.marketBoardShortcut) aetheryteShortcuts |= Configuration.AetheryteAdditionalShortcut.MarketBoard;
            if (this.strikingDummyShortcut) aetheryteShortcuts |= Configuration.AetheryteAdditionalShortcut.StrikingDummy;
            if (this.innRoomShortcut) aetheryteShortcuts |= Configuration.AetheryteAdditionalShortcut.InnRoom;
            FindAnythingPlugin.Configuration.AetheryteShortcuts = aetheryteShortcuts;
            FindAnythingPlugin.Configuration.AetheryteInnRoomShortcutExcludeLimsa = this.innRoomShortcutNoLimsa;
            
            FindAnythingPlugin.Configuration.EmoteMode = this.emoteMotionMode;
            FindAnythingPlugin.Configuration.ShowEmoteCommand = this.showEmoteCommand;
            FindAnythingPlugin.Configuration.WikiModeNoSpoilers = this.wikiModeNoSpoilers;
            FindAnythingPlugin.Configuration.PositionOffset = this.posOffset;
            FindAnythingPlugin.Configuration.OnlyWikiMode = this.onlyWiki;
            FindAnythingPlugin.Configuration.QuickSelectKey = this.quickSelectKey;
            FindAnythingPlugin.Configuration.Speed = this.speed;
            FindAnythingPlugin.Configuration.NotInCombat = this.notInCombat;
            FindAnythingPlugin.Configuration.TeamCraftForceBrowser = this.tcForceBrowser;
            FindAnythingPlugin.Configuration.HistoryEnabled = this.historyEnabled;
            FindAnythingPlugin.Configuration.DisableMouseSelection = this.disableMouseSelection;
            FindAnythingPlugin.Configuration.OpenCraftingLogToRecipe = this.openCraftingLogToRecipe;

            FindAnythingPlugin.Configuration.MatchMode = this.matchMode;
            FindAnythingPlugin.Configuration.MatchSigilSimple = this.matchSigilSimple;
            FindAnythingPlugin.Configuration.MatchSigilFuzzy = this.matchSigilFuzzy;
            FindAnythingPlugin.Configuration.MatchSigilFuzzyParts = this.matchSigilFuzzyParts;

            FindAnythingPlugin.Configuration.Save();
        }

        ImGui.SameLine();

        if (ImGui.Button("Discard") || saveAndClose)
        {
            IsOpen = false;
            windowOffsetChangeTime = null;
        }

        if (windowOffsetChangeTime != null) {
            // Use same positioning/size logic as main window
            var size = new Vector2(500, 40) * ImGuiHelpers.GlobalScale;
            var startPos = ImGuiHelpers.MainViewport.Pos + (ImGuiHelpers.MainViewport.Size / 2 - size / 2);
            startPos.Y -= 200;
            startPos += posOffset;

            var drawList = ImGui.GetForegroundDrawList();
            drawList.AddRect(startPos, startPos + size, ImGui.ColorConvertFloat4ToU32(ImGuiColors.ParsedGreen));

            if ((DateTime.UtcNow - windowOffsetChangeTime.Value).TotalSeconds > 3) {
                windowOffsetChangeTime = null;
            }
        }
    }

    private int dropSource = -1;

    private void DrawMacrosSection()
    {
        if (macroRearrangeMode) {
            ImGui.TextWrapped("Use arrows or drag and drop macros to change the order.");

            for (var macroNumber = macros.Count - 1; macroNumber >= 0; macroNumber--) {
                var name = macros[macroNumber].SearchName;
                var size = ImGuiHelpers.GetButtonSize(name).WithX(300f);
                ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new Vector2(0f, 0.5f));
                ImGui.Button($"{macros[macroNumber].SearchName}###b{macroNumber}", size);

                if (ImGui.BeginDragDropSource()) {
                    ImGui.SetDragDropPayload("MACRO", ReadOnlySpan<byte>.Empty, 0);
                    ImGui.Button($"{macros[macroNumber].SearchName}###d{macroNumber}", size);
                    dropSource = macroNumber;
                    ImGui.EndDragDropSource();
                }

                if (ImGui.BeginDragDropTarget()) {
                    ImGui.AcceptDragDropPayload("MACRO");
                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Left)) {
                        var moving = macros[dropSource];
                        macros.RemoveAt(dropSource);
                        macros.Insert(macroNumber, moving);
                    }

                    ImGui.EndDragDropTarget();
                }

                ImGui.PopStyleVar();

                ImGui.SameLine();

                if (IconButtonEnabledWhen(macroNumber <= macros.Count - 2, FontAwesomeIcon.AngleDoubleUp,
                        $"macro-top-{macroNumber}")) {
                    var moving = macros[macroNumber];
                    macros.RemoveAt(macroNumber);
                    macros.Add(moving);
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("Move to top");
                }

                ImGui.SameLine();

                if (IconButtonEnabledWhen(macroNumber <= macros.Count - 2, FontAwesomeIcon.ArrowUp,
                        $"macro-up-{macroNumber}")) {
                    macros.Reverse(macroNumber, 2);
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("Move up");
                }

                ImGui.SameLine();

                if (IconButtonEnabledWhen(macroNumber >= 1, FontAwesomeIcon.ArrowDown, $"macro-down-{macroNumber}")) {
                    macros.Reverse(macroNumber - 1, 2);
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("Move down");
                }

                ImGui.SameLine();

                if (IconButtonEnabledWhen(macroNumber >= 1, FontAwesomeIcon.AngleDoubleDown,
                        $"macro-bottom-{macroNumber}")) {
                    var moving = macros[macroNumber];
                    macros.RemoveAt(macroNumber);
                    macros.Insert(0, moving);
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("Move to bottom");
                }
            }

            ImGuiHelpers.ScaledDummy(15);
            if (ImGui.Button("Stop rearranging")) {
                macroRearrangeMode = false;
            }
            ImGui.SameLine();
            if (ImGui.Button("Reverse all")) {
                macros.Reverse();
            }

        }
        else {
            ImGui.TextWrapped(
                "Use this menu to tie search results to macros.\nClick \"Add Macro\", enter the text you want to access it under, select whether or not it is a shared macro and enter its ID.\nUse the ';' character to add search text for a macro, only the first part text will be shown, e.g. \"SGE;sage;healer\".");

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

            for (var macroNumber = this.macros.Count - 1; macroNumber >= 0; macroNumber--) {
                var macro = this.macros[macroNumber];
                ImGui.PushID($"macro_{macroNumber}");

                ImGui.SetNextItemWidth(-1);

                var text = macro.SearchName;
                if (ImGui.InputText($"###macroSn", ref text, 100)) {
                    macro.SearchName = text;
                }

                ImGui.NextColumn();

                if (ImGui.BeginCombo("###macroKnd", macro.Kind.ToString())) {
                    foreach (var macroEntryKind in Enum.GetValues<Configuration.MacroEntry.MacroEntryKind>()) {
                        if (ImGui.Selectable(macroEntryKind.ToString(), macroEntryKind == macro.Kind)) {
                            macro.Kind = macroEntryKind;
                        }
                    }

                    ImGui.EndCombo();
                }

                ImGui.NextColumn();

                if (macro.Kind == Configuration.MacroEntry.MacroEntryKind.Id) {
                    var isShared = macro.Shared;
                    if (ImGui.Checkbox($"###macroSh", ref isShared)) {
                        macro.Shared = isShared;
                    }
                }
                else {
                    ImGui.PushStyleColor(ImGuiCol.FrameBg, ImGuiColors.ParsedGrey);
                    ImGui.PushStyleColor(ImGuiCol.FrameBgActive, ImGuiColors.ParsedGrey);
                    ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, ImGuiColors.ParsedGrey);
                    ImGui.PushStyleColor(ImGuiCol.CheckMark, ImGuiColors.ParsedGrey);

                    var isShared = false;
                    ImGui.Checkbox("###macroSh", ref isShared);

                    ImGui.PopStyleColor(4);
                }


                ImGui.NextColumn();

                switch (macro.Kind) {
                    case Configuration.MacroEntry.MacroEntryKind.Id:
                        ImGui.SetNextItemWidth(-1);

                        var id = macro.Id;
                        if (ImGui.InputInt($"###macroId", ref id)) {
                            id = Math.Max(0, id);
                            id = Math.Min(99, id);
                            macro.Id = id;
                        }

                        break;
                    case Configuration.MacroEntry.MacroEntryKind.SingleLine:
                        var line = macro.Line;
                        line ??= string.Empty;
                        var didColor = false;
                        if (!line.StartsWith("/")) {
                            didColor = true;
                            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
                        }

                        ImGui.SetNextItemWidth(-1);

                        if (ImGui.InputText($"###macroId", ref line, 100)) {
                            macro.Line = line;
                        }

                        if (didColor) {
                            ImGui.PopStyleColor();
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                ImGui.NextColumn();

                var icon = macro.IconId;

                ImGui.SetNextItemWidth(-1);

                if (ImGui.InputInt($"###macroIcon", ref icon)) {
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

            if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus)) {
                this.macros.Insert(0, new Configuration.MacroEntry
                {
                    Id = 0,
                    SearchName = "New Macro",
                    Shared = false,
                    IconId = 066001,
                    Line = string.Empty,
                });
            }

            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("Add new macro link");
            }

            ImGui.SameLine();

            if (ImGuiComponents.IconButton(FontAwesomeIcon.Copy)) {
                var json = JsonConvert.SerializeObject(this.macros);
                ImGui.SetClipboardText("WM1" + json);
            }

            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("Copy macro links to clipboard");
            }

            ImGui.SameLine();

            if (ImGuiComponents.IconButton(FontAwesomeIcon.FileImport)) {
                ImportMacros(ImGui.GetClipboardText());
            }

            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("Import macro links from clipboard");
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowsUpDown)) {
                macroRearrangeMode = true;
            }
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("Rearrange macro links");
            }

            ImGui.Columns(1);
        }
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

    private static bool IconButtonEnabledWhen(bool enabled, FontAwesomeIcon icon, string id)
    {
        if (!enabled)
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
        var result = ImGuiComponents.IconButton(id, icon);
        if (!enabled)
            ImGui.PopStyleVar();

        return result && enabled;
    }
}