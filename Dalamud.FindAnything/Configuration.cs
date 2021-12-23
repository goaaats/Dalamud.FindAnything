using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Keys;

namespace Dalamud.FindAnything
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public SearchSetting ToSearchV3 { get; set; } =
            SearchSetting.Aetheryte | SearchSetting.Duty | SearchSetting.MainCommand | SearchSetting.GeneralAction |
            SearchSetting.Emote | SearchSetting.PluginSettings
            | SearchSetting.Gearsets | SearchSetting.Reserved1 | SearchSetting.Reserved2 | SearchSetting.Mounts |
            SearchSetting.Reserved4 | SearchSetting.Reserved5 | SearchSetting.Reserved6 | SearchSetting.Reserved7 |
            SearchSetting.Reserved8
            | SearchSetting.Reserved9 | SearchSetting.Reserved10 | SearchSetting.Reserved11 | SearchSetting.Reserved12 |
            SearchSetting.Reserved13 | SearchSetting.Reserved14 | SearchSetting.Reserved15 | SearchSetting.Reserved16
            | SearchSetting.Reserved17 | SearchSetting.Reserved18 | SearchSetting.Reserved19 |
            SearchSetting.Reserved20 | SearchSetting.Reserved21 | SearchSetting.Reserved22 | SearchSetting.Reserved23 |
            SearchSetting.Reserved24;

        public OpenMode Open { get; set; } = OpenMode.Combo;

        public uint ShiftShiftDelay { get; set; } = 40;
        public VirtualKey ComboModifier { get; set; } = VirtualKey.CONTROL;
        public VirtualKey ComboKey { get; set; } = VirtualKey.T;
        public VirtualKey ShiftShiftKey { get; set; } = VirtualKey.OEM_MINUS;

        public bool WikiModeNoSpoilers { get; set; } = true;

        public enum OpenMode
        {
            ShiftShift,
            Combo,
        }

        [Flags]
        public enum SearchSetting : uint
        {
            None = 0,
            Duty = 1 << 0,
            Aetheryte = 1 << 1,
            MainCommand = 1 << 2,
            GeneralAction = 1 << 3,
            Emote = 1 << 4,
            PluginSettings = 1 << 5,
            Gearsets = 1 << 6,
            Reserved1 = 1 << 7,
            Reserved2 = 1 << 8,
            Mounts = 1 << 9,
            Reserved4 = 1 << 10,
            Reserved5 = 1 << 11,
            Reserved6 = 1 << 12,
            Reserved7 = 1 << 13,
            Reserved8 = 1 << 14,
            Reserved9 = 1 << 15,
            Reserved10 = 1 << 16,
            Reserved11 = 1 << 17,
            Reserved12 = 1 << 18,
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

        public class MacroEntry
        {
            public bool Shared { get; set; }
            public int Id { get; set; }
            public string Line { get; set; } = string.Empty;
            public string SearchName { get; set; }
            public int IconId { get; set; }
            public MacroEntryKind Kind { get; set; }

            public enum MacroEntryKind
            {
                Id,
                SingleLine
            }

            public MacroEntry()
            {

            }

            public MacroEntry(MacroEntry initial)
            {
                Shared = initial.Shared;
                Id = initial.Id;
                SearchName = initial.SearchName;
                IconId = initial.IconId;
                Kind = initial.Kind;
                Line = initial.Line;
            }
        }

        public List<MacroEntry> MacroLinks { get; set; } = new();

        public bool DoAetheryteGilCost { get; set; } = false;
        public EmoteMotionMode EmoteMode { get; set; } = EmoteMotionMode.Default;
        public bool ShowEmoteCommand { get; set; } = false;

        public HintKind HintLevel { get; set; } = HintKind.HintTyping;

        public Dictionary<string, float> MathConstants { get; set; } = new();

        public Vector2 PositionOffset { get; set; } = new(0, 0);

        public bool OnlyWikiMode { get; set; } = false;

        public enum HintKind
        {
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

        public enum EmoteMotionMode
        {
            Default,
            Ask,
            AlwaysMotion,
        }

        // the below exist just to make saving less cumbersome

        [NonSerialized]
        private DalamudPluginInterface? pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface!.SavePluginConfig(this);
        }
    }
}
