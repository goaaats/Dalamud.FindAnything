using Dalamud.Configuration;
using Dalamud.FindAnything.Game;
using Dalamud.Game.ClientState.Keys;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Dalamud.FindAnything;

[Serializable]
public class Configuration : IPluginConfiguration {
    public int Version { get; set; } = 0;

    public SearchSetting ToSearchV3 { get; set; } =
        SearchSetting.Aetheryte | SearchSetting.Duty | SearchSetting.MainCommand | SearchSetting.GeneralAction |
        SearchSetting.Emote | SearchSetting.PluginSettings | SearchSetting.Gearsets | SearchSetting.CraftingRecipes |
        SearchSetting.GatheringItems | SearchSetting.Mounts | SearchSetting.Minions | SearchSetting.MacroLinks |
        SearchSetting.Internal | SearchSetting.Maths | SearchSetting.FashionAccessories | SearchSetting.Collection |
        SearchSetting.ExtraCommand | SearchSetting.Facewear | SearchSetting.Coordinates | SearchSetting.Reserved13 |
        SearchSetting.Reserved14 | SearchSetting.Reserved15 | SearchSetting.Reserved16 | SearchSetting.Reserved17 |
        SearchSetting.Reserved18 | SearchSetting.Reserved19 | SearchSetting.Reserved20 | SearchSetting.Reserved21 |
        SearchSetting.Reserved22 | SearchSetting.Reserved23 | SearchSetting.Reserved24;

    public Dictionary<SearchSetting, int> SearchWeights = new();

    public OpenMode Open { get; set; } = OpenMode.Combo;

    public uint ShiftShiftDelay { get; set; } = 40;

    public enum DoubleTapUnit {
        Frames,
        Milliseconds,
    }

    public DoubleTapUnit ShiftShiftUnit { get; set; } = DoubleTapUnit.Frames;
    public VirtualKey ComboModifier { get; set; } = VirtualKey.CONTROL;
    public VirtualKey ComboModifier2 { get; set; } = VirtualKey.NO_KEY;
    public VirtualKey ComboKey { get; set; } = VirtualKey.T;
    public bool PreventPassthrough { get; set; } = true;

    public VirtualKey WikiComboKey { get; set; } = VirtualKey.NO_KEY;

    public VirtualKey ShiftShiftKey { get; set; } = VirtualKey.OEM_MINUS;

    public VirtualKey QuickSelectKey { get; set; } = VirtualKey.CONTROL;

    public bool WikiModeNoSpoilers { get; set; } = true;
    public bool TeamCraftForceBrowser { get; set; } = false;
    public bool DisableMouseSelection { get; set; } = false;
    public bool OpenCraftingLogToRecipe { get; set; } = false;

    public enum OpenMode {
        ShiftShift,
        Combo,
    }

    [Flags]
    public enum SearchSetting : uint {
        None = 0,
        Duty = 1 << 0,
        Aetheryte = 1 << 1,
        MainCommand = 1 << 2,
        GeneralAction = 1 << 3,
        Emote = 1 << 4,
        PluginSettings = 1 << 5,
        Gearsets = 1 << 6,
        CraftingRecipes = 1 << 7,
        GatheringItems = 1 << 8,
        Mounts = 1 << 9,
        Minions = 1 << 10,
        MacroLinks = 1 << 11, // Cannot be toggled off
        Internal = 1 << 12,   // Cannot be toggled off
        Maths = 1 << 13,
        FashionAccessories = 1 << 14,
        Collection = 1 << 15,
        ExtraCommand = 1 << 16,
        Facewear = 1 << 17,
        Coordinates = 1 << 18,
        Reserved13 = 1 << 19,
        Reserved14 = 1 << 20,
        Reserved15 = 1 << 21,
        Reserved16 = 1 << 22,
        Reserved17 = 1 << 23,
        Reserved18 = 1 << 24,
        Reserved19 = 1 << 25,
        Reserved20 = 1 << 26,
        Reserved21 = 1 << 27,
        Reserved22 = 1 << 28,
        Reserved23 = 1 << 29,
        Reserved24 = 1 << 30,
    }

    public class MacroEntry {
        public bool Shared { get; set; }
        public int Id { get; set; }
        public string Line { get; set; } = string.Empty;
        public string SearchName { get; set; } = string.Empty;
        public int IconId { get; set; }
        public MacroEntryKind Kind { get; set; }

        public enum MacroEntryKind {
            Id,
            SingleLine,
        }

        public MacroEntry() { }

        public MacroEntry(MacroEntry initial) {
            Shared = initial.Shared;
            Id = initial.Id;
            SearchName = initial.SearchName;
            IconId = initial.IconId;
            Kind = initial.Kind;
            Line = initial.Line;
        }
    }

    public List<MacroEntry> MacroLinks { get; set; } = [];

    public enum MacroSearchDirection {
        BottomToTop,
        TopToBottom,
    }

    public MacroSearchDirection MacroLinksSearchDirection { get; set; } = MacroSearchDirection.BottomToTop;

    public bool DoAetheryteGilCost { get; set; } = false;


    [Flags]
    public enum AetheryteAdditionalShortcut {
        None,
        MarketBoard = 1 << 0,
        StrikingDummy = 1 << 1,
        InnRoom = 1 << 2,
    }

    public AetheryteAdditionalShortcut AetheryteShortcuts { get; set; } = AetheryteAdditionalShortcut.MarketBoard |
                                                                          AetheryteAdditionalShortcut.StrikingDummy |
                                                                          AetheryteAdditionalShortcut.InnRoom;

    public bool AetheryteInnRoomShortcutExcludeLimsa { get; set; } = false;

    public EmoteMotionMode EmoteMode { get; set; } = EmoteMotionMode.Default;
    public bool ShowEmoteCommand { get; set; } = false;

    public HintKind HintLevel { get; set; } = HintKind.HintTyping;

    public Dictionary<string, float> MathConstants { get; set; } = new();

    public Vector2 PositionOffset { get; set; } = new(0, 0);

    public bool OnlyWikiMode { get; set; } = false;

    public List<SearchSetting> Order { get; set; } = [];

    public bool NotInCombat { get; set; } = false;

    public bool HistoryEnabled { get; set; } = true;

    public enum HintKind {
        HintTyping,
        HintEnter,
        HintUpDown,
        HintTeleport,
        HintEmoteDuty,
        HintGameCmd,
        HintChatCmd,
        HintMacroLink,
        HintGearset,
        HintMath,
    }

    public enum EmoteMotionMode {
        Default,
        Ask,
        AlwaysMotion,
    }

    public enum ScrollSpeed {
        Slow,
        Medium,
        Fast,
    }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ScrollSpeed? Speed { get; set; }

    private static readonly List<SearchSetting> DefaultOrder = [
        SearchSetting.MacroLinks,
        SearchSetting.Gearsets,
        SearchSetting.Aetheryte,
        SearchSetting.Coordinates,
        SearchSetting.Duty,
        SearchSetting.Mounts,
        SearchSetting.Minions,
        SearchSetting.MainCommand,
        SearchSetting.ExtraCommand,
        SearchSetting.GeneralAction,
        SearchSetting.PluginSettings,
        SearchSetting.Emote,
        SearchSetting.Internal,
        SearchSetting.Collection,
        SearchSetting.FashionAccessories,
        SearchSetting.Facewear,
        SearchSetting.CraftingRecipes,
        SearchSetting.GatheringItems,
    ];

    [JsonProperty("MatchMode", NullValueHandling = NullValueHandling.Ignore)]
    public MatchMode? MatchModeOld { get; set; }

    [JsonProperty("MatchModeNew")]
    public MatchMode MatchMode { get; set; } = MatchMode.Fuzzy;

    public string MatchSigilSimple { get; set; } = "'";

    public string MatchSigilFuzzy { get; set; } = "`";

    public string MatchSigilFuzzyParts { get; set; } = "~";

    public enum CursorControlType {
        System,
        Custom,
    }

    public CursorControlType CursorControl { get; set; } = CursorControlType.System;

    public int CursorLineRepeatDelay = 120;

    public int CursorLineRepeatInterval = 65;

    public int CursorPageRepeatDelay = 200;

    public int CursorPageRepeatInterval = 200;

    public bool MatchShortPluginSettings = false;

    public GameWindow.SimulationState? SimulationState { get; set; } = null;

    public int? GoldenTicketNumber { get; set; } = null;

    public void Initialize() {
        foreach (var search in DefaultOrder) {
            if (Order.Contains(search)) {
                continue;
            }
            Order.Add(search);
        }

        if (MatchModeOld.HasValue) {
            MatchMode = MatchModeOld.Value == MatchMode.Simple ? MatchMode.Fuzzy : MatchModeOld.Value;
            MatchModeOld = null;
        }

        if (Speed.HasValue) {
            CursorLineRepeatInterval = Speed switch {
                ScrollSpeed.Slow => 120,
                ScrollSpeed.Medium => 65,
                ScrollSpeed.Fast => 30,
                _ => 65,
            };
            Speed = null;
        }
    }
}
