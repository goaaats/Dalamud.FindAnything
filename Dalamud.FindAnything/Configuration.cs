using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Keys;

namespace Dalamud.FindAnything
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public SearchSetting ToSearchV2 { get; set; } = SearchSetting.Aetheryte | SearchSetting.Duty | SearchSetting.MainCommand | SearchSetting.GeneralAction | SearchSetting.Emote | SearchSetting.PluginSettings;

        public OpenMode Open { get; set; } = OpenMode.Combo;

        public uint ShiftShiftDelay { get; set; } = 40;
        public VirtualKey ComboModifier { get; set; } = VirtualKey.CONTROL;
        public VirtualKey ComboKey { get; set; } = VirtualKey.T;
        public VirtualKey ShiftShiftKey { get; set; } = VirtualKey.OEM_MINUS;

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
        }
        
        public class MacroEntry
        {
            public bool Shared { get; set; }
            public int Id { get; set; }
            public string SearchName { get; set; }
            public int IconId { get; set; }

            public MacroEntry()
            {
                
            }

            public MacroEntry(MacroEntry initial)
            {
                Shared = initial.Shared;
                Id = initial.Id;
                SearchName = initial.SearchName;
                IconId = initial.IconId;
            }
        }

        public List<MacroEntry> MacroLinks { get; set; } = new();

        public bool DoAetheryteGilCost { get; set; } = false;
        public EmoteMotionMode EmoteMode { get; set; } = EmoteMotionMode.Default;
        public bool ShowEmoteCommand { get; set; } = false;

        public HintKind HintLevel { get; set; } = HintKind.HintTyping;

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