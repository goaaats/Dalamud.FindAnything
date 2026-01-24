using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Dalamud.FindAnything.Settings;

public partial class SettingsWindow {
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
    private List<Configuration.MacroEntry> macros = [];
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
    private List<Configuration.SearchSetting> order = [];
    private Dictionary<Configuration.SearchSetting, int> searchWeights = new();
    private bool notInCombat;
    private bool tcForceBrowser;
    private bool historyEnabled;
    private bool disableMouseSelection;
    private MatchMode matchMode;
    private string matchSigilSimple = "'";
    private string matchSigilFuzzy = "`";
    private string matchSigilFuzzyParts = "~";
    private Configuration.CursorControlType cursorControl;
    private int lineScrollRepeatDelay;
    private int lineScrollRepeatInterval;
    private int pageScrollRepeatDelay;
    private int pageScrollRepeatInterval;
    private bool matchShortPluginSettings;
    private bool craftingMergeItems;
    private Configuration.CraftingSingleSelectAction craftingRecipeSelect;
    private Configuration.CraftingSingleSelectAction craftingItemSingleSelect;
    private Configuration.CraftingMergedSelectAction craftingItemMergedSelect;

    private void CopyConfigToWindow(Configuration config) {
        flags = (uint)config.ToSearchV3;
        openMode = config.Open;
        shiftShiftKey = config.ShiftShiftKey;
        shiftShiftDelay = (int)config.ShiftShiftDelay;
        shiftShiftUnit = config.ShiftShiftUnit;
        comboKey = config.ComboKey;
        comboModifierKey = config.ComboModifier;
        comboModifier2Key = config.ComboModifier2;
        wikiComboKey = config.WikiComboKey;
        preventPassthrough = config.PreventPassthrough;
        macros = config.MacroLinks.Select(x => new Configuration.MacroEntry(x)).ToList();
        macroLinksSearch = config.MacroLinksSearchDirection;
        aetheryteGilCost = config.DoAetheryteGilCost;
        marketBoardShortcut = config.AetheryteShortcuts.HasFlag(Configuration.AetheryteAdditionalShortcut.MarketBoard);
        strikingDummyShortcut = config.AetheryteShortcuts.HasFlag(Configuration.AetheryteAdditionalShortcut.StrikingDummy);
        emoteMotionMode = config.EmoteMode;
        showEmoteCommand = config.ShowEmoteCommand;
        wikiModeNoSpoilers = config.WikiModeNoSpoilers;
        constants = config.MathConstants;
        posOffset = config.PositionOffset;
        onlyWiki = config.OnlyWikiMode;
        quickSelectKey = config.QuickSelectKey;
        order = config.Order.ToList();
        searchWeights = new Dictionary<Configuration.SearchSetting, int>(config.SearchWeights);
        notInCombat = config.NotInCombat;
        tcForceBrowser = config.TeamCraftForceBrowser;
        historyEnabled = config.HistoryEnabled;
        disableMouseSelection = config.DisableMouseSelection;
        matchMode = config.MatchMode;
        matchSigilSimple = config.MatchSigilSimple;
        matchSigilFuzzy = config.MatchSigilFuzzy;
        matchSigilFuzzyParts = config.MatchSigilFuzzyParts;

        cursorControl = config.CursorControl;
        lineScrollRepeatDelay = config.CursorLineRepeatDelay;
        lineScrollRepeatInterval = config.CursorLineRepeatInterval;
        pageScrollRepeatDelay = config.CursorPageRepeatDelay;
        pageScrollRepeatInterval = config.CursorPageRepeatInterval;

        matchShortPluginSettings = config.MatchShortPluginSettings;

        craftingMergeItems = config.CraftingMergeItems;
        craftingRecipeSelect = config.CraftingRecipeSelect;
        craftingItemSingleSelect = config.CraftingItemSelectSingle;
        craftingItemMergedSelect = config.CraftingItemSelectMerged;
    }

    private void CopyWindowToConfig(Configuration config) {
        config.ToSearchV3 = (Configuration.SearchSetting)flags;
        config.Order = order;
        config.SearchWeights = searchWeights;

        config.Open = openMode;
        config.ShiftShiftKey = shiftShiftKey;
        config.ShiftShiftDelay = (uint)shiftShiftDelay;
        config.ShiftShiftUnit = shiftShiftUnit;

        config.ComboKey = comboKey;
        config.ComboModifier = comboModifierKey;
        config.ComboModifier2 = comboModifier2Key;
        config.WikiComboKey = wikiComboKey;
        config.PreventPassthrough = preventPassthrough;

        config.MacroLinks = macros;
        config.MacroLinksSearchDirection = macroLinksSearch;

        config.DoAetheryteGilCost = aetheryteGilCost;

        var aetheryteShortcuts = Configuration.AetheryteAdditionalShortcut.None;
        if (marketBoardShortcut) aetheryteShortcuts |= Configuration.AetheryteAdditionalShortcut.MarketBoard;
        if (strikingDummyShortcut) aetheryteShortcuts |= Configuration.AetheryteAdditionalShortcut.StrikingDummy;
        if (innRoomShortcut) aetheryteShortcuts |= Configuration.AetheryteAdditionalShortcut.InnRoom;
        config.AetheryteShortcuts = aetheryteShortcuts;
        config.AetheryteInnRoomShortcutExcludeLimsa = innRoomShortcutNoLimsa;

        config.EmoteMode = emoteMotionMode;
        config.ShowEmoteCommand = showEmoteCommand;
        config.WikiModeNoSpoilers = wikiModeNoSpoilers;
        config.PositionOffset = posOffset;
        config.OnlyWikiMode = onlyWiki;
        config.QuickSelectKey = quickSelectKey;
        config.NotInCombat = notInCombat;
        config.TeamCraftForceBrowser = tcForceBrowser;
        config.HistoryEnabled = historyEnabled;
        config.DisableMouseSelection = disableMouseSelection;

        config.MatchMode = matchMode;
        config.MatchSigilSimple = matchSigilSimple;
        config.MatchSigilFuzzy = matchSigilFuzzy;
        config.MatchSigilFuzzyParts = matchSigilFuzzyParts;

        config.CursorControl = cursorControl;
        config.CursorLineRepeatDelay = lineScrollRepeatDelay;
        config.CursorLineRepeatInterval = lineScrollRepeatInterval;
        config.CursorPageRepeatDelay = pageScrollRepeatDelay;
        config.CursorPageRepeatInterval = pageScrollRepeatInterval;

        config.MatchShortPluginSettings = matchShortPluginSettings;

        config.CraftingMergeItems = craftingMergeItems;
        config.CraftingRecipeSelect = craftingRecipeSelect;
        config.CraftingItemSelectSingle = craftingItemSingleSelect;
        config.CraftingItemSelectMerged = craftingItemMergedSelect;
    }

    private void DrawSaveFooter() {
        var save = ImGui.Button("Save");

        ImGui.SameLine();
        var saveAndClose = ImGui.Button("Save and Close");

        if (save || saveAndClose) {
            CopyWindowToConfig(FindAnythingPlugin.Configuration);
            FindAnythingPlugin.ConfigManager.SaveAndNotify();
        }

        ImGui.SameLine();

        if (ImGui.Button("Discard") || saveAndClose) {
            IsOpen = false;
            finderOffsetChangeTime = null;
        }
    }
}
