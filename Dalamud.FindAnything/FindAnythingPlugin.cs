using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Web;
using Dalamud.Data;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Aetherytes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Gui;
using Dalamud.Game.Gui.Toast;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using ImGuiNET;
using ImGuiScene;
using Lumina.Excel.GeneratedSheets;
using NCalc;
using XivCommon;

namespace Dalamud.FindAnything
{
    public sealed class FindAnythingPlugin : IDalamudPlugin
    {
        public string Name => "Wotsit";

        private const string commandName = "/wotsit";

        [PluginService] public static DalamudPluginInterface PluginInterface { get; private set; }
        [PluginService] public static CommandManager CommandManager { get; private set; }
        public static Configuration Configuration { get; set; }
        [PluginService] public static Framework Framework { get; private set; }
        [PluginService] public static DataManager Data { get; private set; }
        [PluginService] public static KeyState Keys { get; private set; }
        [PluginService] public static ClientState ClientState { get; private set; }
        [PluginService] public static ChatGui ChatGui { get; private set; }
        [PluginService] public static ToastGui ToastGui { get; private set; }
        [PluginService] public static Dalamud.Game.ClientState.Conditions.Condition Condition { get; private set; }
        [PluginService] public static AetheryteList Aetheryes { get; private set; }
        [PluginService] public static SigScanner TargetScanner { get; private set; }

        public static TextureCache TexCache { get; private set; }
        private static SearchDatabase SearchDatabase { get; set; }
        private static AetheryteManager AetheryteManager { get; set; }
        private static DalamudReflector DalamudReflector { get; set; }
        private static GameStateCache GameStateCache { get; set; }
        private static Input? Input { get; set; }
        private static IpcSystem Ipc { get; set; }

        private bool finderOpen = false;
        private static string searchTerm = string.Empty;
        private static int selectedIndex = 0;

        private int framesSinceLastKbChange = 0;
        private int framesSinceButtonPress = 0;

        private int timeSinceLastShift = 0;
        private bool shiftArmed = false;
        private bool shiftOk = false;

        private WindowSystem windowSystem;
        private static SettingsWindow settingsWindow;

        private static XivCommonBase xivCommon;

        private enum SearchMode
        {
            Top,
            Wiki,
            WikiSiteChoicer,
            EmoteModeChoicer,
        }

        private static readonly IReadOnlyDictionary<uint, string> ClassJobRolesMap = new Dictionary<uint, string>
        {
            { 0, "adventurer" },
            { 1, "tank" },
            { 2, "melee dps" },
            { 3, "tank" },
            { 4, "melee dps" },
            { 5, "ranged dps" },
            { 6, "healer" },
            { 7, "ranged dps" },

            { 8, "doh" },
            { 9, "doh" },
            { 10, "doh" },
            { 11, "doh" },
            { 12, "doh" },
            { 13, "doh" },
            { 14, "doh" },
            { 15, "doh" },
            { 16, "dol" },
            { 17, "dol" },
            { 18, "dol" },

            { 19, "tank" },
            { 20, "melee dps" },
            { 21, "tank" },
            { 22, "melee dps" },
            { 23, "ranged dps" },
            { 24, "healer" },
            { 25, "ranged dps" },
            { 26, "ranged dps" },
            { 27, "ranged dps" },
            { 28, "healer" },
            { 29, "melee dps" },
            { 30, "melee dps" },
            { 31, "ranged dps" },
            { 32, "tank" },
            { 33, "healer" },
            { 34, "melee dps" },
            { 35, "ranged dps" },
            { 36, "duck" },
            { 37, "tank" },
            { 38, "ranged dps" },
            { 39, "melee dps" },
            { 40, "healer" },
        };

        private static SearchMode searchMode = SearchMode.Top;
        private static ISearchResult? choicerTempResult;

        private interface ISearchResult
        {
            public string CatName { get; }
            public string Name { get; }
            public TextureWrap? Icon { get; }
            public bool CloseFinder { get; }

            public void Selected();
        }

        private class WikiSearchResult : ISearchResult
        {
            public string CatName { get; set; }
            public string Name { get; set; }
            public TextureWrap? Icon { get; set; }
            public uint DataKey { get; set; }

            public enum DataCategory
            {
                Instance,
                Quest,
                Item,
            }

            public DataCategory DataCat { get; set; }

            public bool CloseFinder => false;

            public void Selected()
            {
                choicerTempResult = this;
                SwitchSearchMode(SearchMode.WikiSiteChoicer);
            }
        }

        private class WikiSiteChoicerResult : ISearchResult
        {
            public string CatName => string.Empty;
            public string Name => $"Open on {Site}";
            public TextureWrap? Icon => TexCache.WikiIcon;

            public enum SiteChoice
            {
                GamerEscape,
                GarlandTools,
                ConsoleGamesWiki,
                TeamCraft,
            }

            public SiteChoice Site { get; set; }

            public bool CloseFinder => true;

            private static void OpenWikiPage(string input, SiteChoice choice)
            {
                var name = input.Replace(' ', '_');
                name = name.Replace('–', '-');

                switch (choice)
                {
                    case SiteChoice.GamerEscape:
                        Util.OpenLink($"https://ffxiv.gamerescape.com/wiki/{HttpUtility.UrlEncode(name)}?useskin=Vector");
                        break;
                    case SiteChoice.ConsoleGamesWiki:
                        Util.OpenLink($"https://ffxiv.consolegameswiki.com/wiki/{HttpUtility.UrlEncode(name)}");
                        break;
                }

            }

            public void Selected()
            {
                if (choicerTempResult == null)
                    throw new Exception("wikiSiteChoicerResult was null!");

                var wikiResult = choicerTempResult as WikiSearchResult;

                switch (Site)
                {
                    case SiteChoice.ConsoleGamesWiki:
                    case SiteChoice.GamerEscape:
                    {
                        OpenWikiPage(choicerTempResult.Name, Site);
                    }
                        break;
                    case SiteChoice.GarlandTools:
                    {
                        switch (wikiResult.DataCat)
                        {
                            case WikiSearchResult.DataCategory.Instance:
                                Util.OpenLink($"https://garlandtools.org/db/#instance/{wikiResult.DataKey}");
                                break;
                            case WikiSearchResult.DataCategory.Quest:
                                Util.OpenLink($"https://garlandtools.org/db/#quest/{wikiResult.DataKey}");
                                break;
                            case WikiSearchResult.DataCategory.Item:
                                Util.OpenLink($"https://garlandtools.org/db/#item/{wikiResult.DataKey}");
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                        break;
                    case SiteChoice.TeamCraft:
                    {
                        Util.OpenLink($"https://ffxivteamcraft.com/db/en/item/{wikiResult.DataKey}");
                    }
                        break;
                }
            }
        }

        private class SearchWikiSearchResult : ISearchResult
        {
            public string CatName => string.Empty;
            public string Name => $"Search for \"{Query}\" in wikis...";
            public TextureWrap? Icon => TexCache.WikiIcon;

            public string Query { get; set; }

            public bool CloseFinder => true;

            public void Selected()
            {
                Util.OpenLink($"https://ffxiv.gamerescape.com/w/index.php?search={HttpUtility.UrlEncode(Query)}&title=Special%3ASearch&fulltext=1&useskin=Vector");
            }
        }

        private class AetheryteSearchResult : ISearchResult
        {
            public string CatName
            {
                get
                {
                    var name = "Aetherytes";
                    if (Configuration.DoAetheryteGilCost)
                    {
                        name += $" ({TerriName} - {Data.GilCost} Gil)";
                    }
                    else
                    {
                        name += $" ({TerriName})";
                    }

                    return name;
                }
            }

            public string Name { get; set; }
            public TextureWrap? Icon { get; set; }
            public AetheryteEntry Data { get; set; }

            public string TerriName { get; set; }

            public bool CloseFinder => true;

            public void Selected()
            {
                try
                {
                    var didTeleport = TeleportIpc.InvokeFunc(Data.AetheryteId, Data.SubIndex);

                    if (!didTeleport)
                    {
                        UserError("Cannot teleport in this situation.");
                    }
                    else
                    {
                        ChatGui.Print($"Teleporting to {Name}...");
                    }
                }
                catch (IpcNotReadyError)
                {
                    PluginLog.Error("Teleport IPC not found.");
                    UserError("To use Aetherytes within Wotsit, you must install the \"Teleporter\" plugin.");
                }
            }
        }

        private static void UserError(string error)
        {
            ChatGui.PrintError(error);
            ToastGui.ShowError(error);
        }

        private class MainCommandSearchResult : ISearchResult
        {
            public string CatName => "Commands";
            public string Name { get; set; }
            public TextureWrap? Icon { get; set; }
            public uint CommandId { get; set; }

            public bool CloseFinder => true;

            public unsafe void Selected()
            {
                FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->GetUiModule()->ExecuteMainCommand(CommandId);
            }
        }

        private class InternalSearchResult : ISearchResult
        {
            public string CatName => Kind switch
            {
                InternalSearchResultKind.Settings => "Wotsit",
                InternalSearchResultKind.DalamudPlugins => "Dalamud",
                InternalSearchResultKind.DalamudSettings => "Dalamud",
                InternalSearchResultKind.WikiMode => string.Empty,
                _ => throw new ArgumentOutOfRangeException()
            };

            public string Name => GetNameForKind(this.Kind);

            public TextureWrap? Icon => Kind switch
            {
                InternalSearchResultKind.WikiMode => TexCache.WikiIcon,
                _ => TexCache.PluginInstallerIcon,
            };

            public enum InternalSearchResultKind
            {
                Settings,
                DalamudPlugins,
                DalamudSettings,
                WikiMode,
            }

            public InternalSearchResultKind Kind { get; set; }

            public bool CloseFinder => Kind != InternalSearchResultKind.WikiMode;

            public static string GetNameForKind(InternalSearchResultKind kind) => kind switch {
                InternalSearchResultKind.Settings => "Wotsit Settings",
                InternalSearchResultKind.DalamudPlugins => "Dalamud Plugin Installer",
                InternalSearchResultKind.DalamudSettings => "Dalamud Settings",
                InternalSearchResultKind.WikiMode => "Search in wikis...",
                _ => throw new ArgumentOutOfRangeException()
            };

            public void Selected()
            {
                switch (Kind)
                {
                    case InternalSearchResultKind.Settings:
                        settingsWindow.IsOpen = true;
                        break;
                    case InternalSearchResultKind.DalamudPlugins:
                        CommandManager.ProcessCommand("/xlplugins");
                        break;
                    case InternalSearchResultKind.DalamudSettings:
                        CommandManager.ProcessCommand("/xlsettings");
                        break;
                    case InternalSearchResultKind.WikiMode:
                        SwitchSearchMode(SearchMode.Wiki);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private static void SwitchSearchMode(SearchMode newMode)
        {
            searchMode = newMode;
            searchTerm = string.Empty;
            selectedIndex = 0;
            UpdateSearchResults();
            PluginLog.Information($"Now in mode: {newMode}");
        }

        public class GeneralActionSearchResult : ISearchResult
        {
            public string CatName => "General Actions";
            public string Name { get; set;  }
            public TextureWrap? Icon { get; set; }

            public bool CloseFinder => true;

            public void Selected()
            {
                var message = $"/gaction \"{Name}\"";
                xivCommon.Functions.Chat.SendMessage(message);
            }
        }

        private class PluginSettingsSearchResult : ISearchResult
        {
            public string CatName => "Other Plugins";
            public string Name { get; set; }
            public TextureWrap? Icon => TexCache.PluginInstallerIcon;
            public bool CloseFinder => true;
            public DalamudReflector.PluginEntry Plugin { get; set; }

            public void Selected()
            {
                Plugin.OpenConfigUi();
            }
        }

        private class MacroLinkSearchResult : ISearchResult
        {
            public string CatName => "Macros";
            public string Name => Entry.SearchName.Split(';', StringSplitOptions.TrimEntries).First();
            public TextureWrap? Icon
            {
                get
                {
                    if (TexCache.ExtraIcons.ContainsKey((uint) Entry.IconId))
                        return TexCache.ExtraIcons[(uint) Entry.IconId];

                    return null;
                }
            }

            public bool CloseFinder => true;

            public Configuration.MacroEntry Entry { get; set; }

            public unsafe void Selected()
            {
                switch (Entry.Kind)
                {
                    case Configuration.MacroEntry.MacroEntryKind.Id:
                        RaptureShellModule.Instance->ExecuteMacro((Entry.Shared ? RaptureMacroModule.Instance->Shared : RaptureMacroModule.Instance->Individual)[Entry.Id]);
                        break;
                    case Configuration.MacroEntry.MacroEntryKind.SingleLine:
                        if (!Entry.Line.StartsWith("/") || Entry.Line.Length > 100)
                        {
                            PluginLog.Error("Invalid slash command:" + Entry.Line);
                            return;
                        }

                        xivCommon.Functions.Chat.SendMessage(Entry.Line);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private class DutySearchResult : ISearchResult
        {
            public string CatName { get; set; }
            public string Name { get; set; }
            public TextureWrap? Icon { get; set; }
            public bool CloseFinder => true;

            public uint DataKey { get; set; }

            public void Selected()
            {
                xivCommon.Functions.DutyFinder.OpenDuty(DataKey);
            }
        }

        private class ContentRouletteSearchResult : ISearchResult
        {
            public string CatName => "Duty Roulette";
            public string Name { get; set; }
            public TextureWrap? Icon => TexCache.ContentTypeIcons[1];
            public bool CloseFinder => true;

            public byte DataKey { get; set; }

            public void Selected()
            {
                xivCommon.Functions.DutyFinder.OpenRoulette(DataKey);
            }
        }

        private class EmoteSearchResult : ISearchResult
        {
            public string CatName
            {
                get
                {
                    var cat = "Emote";
                    if (Configuration.ShowEmoteCommand)
                        cat += $" ({SlashCommand})";

                    return cat;
                }
            }

            public string Name { get; set; }
            public TextureWrap? Icon { get; set; }
            public string SlashCommand { get; set; }

            public bool CloseFinder => Configuration.EmoteMode != Configuration.EmoteMotionMode.Ask;

            public Configuration.EmoteMotionMode MotionMode { get; set; } = Configuration.EmoteMode;

            public void Selected()
            {
                if (MotionMode == Configuration.EmoteMotionMode.Ask)
                {
                    choicerTempResult = this;
                    SwitchSearchMode(SearchMode.EmoteModeChoicer);
                    return;
                }

                var cmd = SlashCommand;
                if (!cmd.StartsWith("/"))
                    throw new Exception($"SlashCommand prop does not actually start with a slash: {SlashCommand}");

                if (MotionMode == Configuration.EmoteMotionMode.AlwaysMotion)
                    cmd += " motion";

                xivCommon.Functions.Chat.SendMessage(cmd);
            }
        }

        private class EmoteModeChoicerResult : ISearchResult
        {
            public string CatName => string.Empty;

            public string Name => Choice switch
            {
                EmoteModeChoice.Default => "Default Choice",
                EmoteModeChoice.MotionOnly => "Only Motion",
                _ => throw new ArgumentOutOfRangeException()
            };

            public TextureWrap? Icon => TexCache.EmoteIcon;
            public bool CloseFinder => true;

            public enum EmoteModeChoice
            {
                Default,
                MotionOnly,
            }

            public EmoteModeChoice Choice { get; set; }

            public void Selected()
            {
                var emoteRes = choicerTempResult as EmoteSearchResult;

                switch (this.Choice)
                {
                    case EmoteModeChoice.Default:
                        emoteRes.MotionMode = Configuration.EmoteMotionMode.Default;
                        break;
                    case EmoteModeChoice.MotionOnly:
                        emoteRes.MotionMode = Configuration.EmoteMotionMode.AlwaysMotion;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                emoteRes.Selected();
            }
        }

        private class HintResult : ISearchResult
        {
            public string CatName => string.Empty;

            public string Name => HintLevel switch
            {
                Configuration.HintKind.HintTyping => "Just start typing to search!",
                Configuration.HintKind.HintEnter => "Press enter to select results!",
                Configuration.HintKind.HintUpDown => "Press the up and down buttons to scroll!",
                Configuration.HintKind.HintTeleport => "Search for aetherytes or zone names!",
                Configuration.HintKind.HintEmoteDuty =>  "Search for emotes or duties!",
                Configuration.HintKind.HintGameCmd => "Search for game commands, like timers!",
                Configuration.HintKind.HintChatCmd => "Run chat commands by typing them here!",
                Configuration.HintKind.HintMacroLink => "Link macros to search in \"wotsit settings\"!",
                Configuration.HintKind.HintGearset => "Search for names of gearsets, jobs or roles!",
                Configuration.HintKind.HintMath => "Type mathematical expressions into the search bar!",
                _ => throw new ArgumentOutOfRangeException()
            };

            public TextureWrap? Icon => TexCache.HintIcon;
            public bool CloseFinder => false;

            public Configuration.HintKind HintLevel { get; set; }

            public void Selected()
            {
                // ignored
            }
        }

        private class ChatCommandSearchResult : ISearchResult
        {
            public string CatName => string.Empty;
            public string Name => $"Run chat command \"{Command}\"";
            public TextureWrap? Icon => TexCache.ChatIcon;
            public bool CloseFinder => true;

            public string Command { get; set; }

            public void Selected()
            {
                if (!Command.StartsWith("/"))
                    throw new Exception("Command in ChatCommandSearchResult didn't start with slash!");

                xivCommon.Functions.Chat.SendMessage(Command);
            }
        }

        private class IpcSearchResult : ISearchResult
        {
            public string CatName { get; set; }
            public string Name { get; set; }
            public TextureWrap? Icon { get; set; }
            public bool CloseFinder => true;

            public string Guid { get; set; }

            public void Selected()
            {
                Ipc.Invoke(Guid);
            }
        }

        private static object? lastAcceptedExpressionResult;

        private class ExpressionResult : ISearchResult
        {
            public string CatName => string.Empty;

            public string Name
            {
                get
                {
                    if (!HasError)
                    {
                        return $" = {Result}";
                    }
                    return " = ERROR";
                }
            }

            public TextureWrap Icon => TexCache.MathsIcon;

            public bool CloseFinder => true;

            public object? Result { get; set; }

            public bool HasError { get; set; }

            public void Selected()
            {
                if (!HasError)
                {
                    lastAcceptedExpressionResult = Result;
                    ImGui.SetClipboardText(Result!.ToString());
                }
            }
        }

        private class GearsetSearchResult : ISearchResult
        {
            public string CatName => "Gearset";
            public string Name => Gearset.Name;
            public TextureWrap? Icon => TexCache.ClassJobIcons[Gearset.ClassJob];
            public bool CloseFinder => true;

            public GameStateCache.Gearset Gearset { get; set; }

            public void Selected()
            {
                xivCommon.Functions.Chat.SendMessage("/gs change " + Gearset.Slot);
            }
        }

        private class MountResult : ISearchResult
        {
            public string CatName => "Mount";
            public string Name => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Mount.Singular);
            public TextureWrap? Icon => TexCache.MountIcons[Mount.RowId];
            public bool CloseFinder => true;

            public Mount Mount { get; set; }

            public void Selected()
            {
                xivCommon.Functions.Chat.SendMessage($"/mount \"{Mount.Singular}\"");
            }
        }

        private static ISearchResult[]? results;

        public static ICallGateSubscriber<uint, byte, bool> TeleportIpc { get; private set; }

        public FindAnythingPlugin()
        {
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);

            CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open the Wotsit settings."
            });

            PluginInterface.UiBuilder.Draw += DrawUI;
            Framework.Update += FrameworkOnUpdate;

            PluginInterface.UiBuilder.DisableCutsceneUiHide = true;
            PluginInterface.UiBuilder.DisableUserUiHide = true;

            PluginInterface.UiBuilder.OpenConfigUi += () =>
            {
                settingsWindow.IsOpen = true;
            };

            TeleportIpc = PluginInterface.GetIpcSubscriber<uint, byte, bool>("Teleport");

            TexCache = TextureCache.Load(null, Data);
            SearchDatabase = SearchDatabase.Load();
            AetheryteManager = AetheryteManager.Load();

            windowSystem = new WindowSystem("wotsit");
            settingsWindow = new SettingsWindow(this) { IsOpen = false };
            windowSystem.AddWindow(settingsWindow);
            PluginInterface.UiBuilder.Draw += windowSystem.Draw;

            xivCommon = new XivCommonBase();
            DalamudReflector = DalamudReflector.Load();
            GameStateCache = GameStateCache.Load();
            Input = new Input();
            Ipc = new IpcSystem(PluginInterface, Data, TexCache);

            Expression.CacheEnabled = true;
            new Expression("1+1").Evaluate(); // Warm up evaluator, takes like 100ms
        }

        private void FrameworkOnUpdate(Framework framework)
        {
            if (Input.Disabled || Input == null)
                return;

            Input.Update();

            if (Input.IsDown(VirtualKey.ESCAPE))
            {
                CloseFinder();
            }
            else
            {
                switch (Configuration.Open)
                {
                    case Configuration.OpenMode.ShiftShift:
                        var shiftDown = Input.IsDown(Configuration.ShiftShiftKey);

                        if (shiftDown && !shiftArmed)
                        {
                            shiftArmed = true;
                        }

                        if (!shiftDown && shiftArmed)
                        {
                            shiftOk = true;
                        }

                        if (shiftOk && !shiftDown)
                        {
                            timeSinceLastShift++;
                        }
                        else if (shiftDown && shiftOk && timeSinceLastShift < Configuration.ShiftShiftDelay)
                        {
                            OpenFinder();
                            timeSinceLastShift = 0;
                            shiftArmed = false;
                            shiftOk = false;
                        }
                        else if (shiftOk && timeSinceLastShift > Configuration.ShiftShiftDelay)
                        {
                            timeSinceLastShift = 0;
                            shiftArmed = false;
                            shiftOk = false;
                        }

                        break;
                    case Configuration.OpenMode.Combo:
                        var mod = Configuration.ComboModifier == VirtualKey.NO_KEY || Input.IsDown(Configuration.ComboModifier);
                        var key = Configuration.ComboKey == VirtualKey.NO_KEY || Input.IsDown(Configuration.ComboKey);

                        if (mod && key)
                        {
                            OpenFinder();
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private static bool CheckInDuty()
        {
            return Condition[ConditionFlag.BoundByDuty] || Condition[ConditionFlag.BoundByDuty56] ||
                   Condition[ConditionFlag.BoundByDuty95];
        }

        private static bool CheckInEvent()
        {
            return Condition[ConditionFlag.Occupied] ||
                   Condition[ConditionFlag.OccupiedInCutSceneEvent];
        }

        private static bool CheckInCombat()
        {
            return Condition[ConditionFlag.InCombat];
        }

        private static void UpdateSearchResults()
        {
            if (searchTerm.IsNullOrEmpty() && searchMode != SearchMode.WikiSiteChoicer && searchMode != SearchMode.EmoteModeChoicer)
            {
                results = null;

                return;
            }

#if DEBUG
            var sw = Stopwatch.StartNew();
#endif

            var term = searchTerm.ToLower();
            var isInDuty = CheckInDuty();
            var isInEvent = CheckInEvent();
            var isInCombat = CheckInCombat();

            var cResults = new List<ISearchResult>();

            switch (searchMode)
            {
                case SearchMode.Top:
                {
                    DalamudReflector.RefreshPlugins();

                    foreach (var macroLink in Configuration.MacroLinks)
                    {
                        if (macroLink.SearchName.ToLower().Contains(term))
                        {
                            cResults.Add(new MacroLinkSearchResult
                            {
                                Entry = macroLink,
                            });
                        }
                    }

                    if (Configuration.ToSearchV3.HasFlag(Configuration.SearchSetting.Gearsets) && !isInCombat)
                    {
                        var cj = Data.GetExcelSheet<ClassJob>()!;
                        foreach (var gearset in GameStateCache.Gearsets)
                        {
                            var cjRow = cj.GetRow(gearset.ClassJob)!;

                            if (gearset.Name.ToLower().Contains(term) || cjRow.Name.RawString.ToLower().Contains(term) || cjRow.Abbreviation.RawString.ToLower().Contains(term) || ClassJobRolesMap[gearset.ClassJob].Contains(term))
                            {
                                cResults.Add(new GearsetSearchResult
                                {
                                    Gearset = gearset,
                                });
                            }
                        }
                    }
                    
                    if (Configuration.ToSearchV3.HasFlag(Configuration.SearchSetting.Mounts) && !isInDuty && !isInCombat)
                    {
                        foreach (var mount in Data.GetExcelSheet<Mount>()!)
                        {
                            if (!GameStateCache.UnlockedMountKeys.Contains(mount.RowId))
                                continue;

                            if (mount.Singular.RawString.ToLower().Contains(term))
                            {
                                cResults.Add(new MountResult
                                {
                                    Mount = mount,
                                });
                            }
                            
                            if (cResults.Count > MAX_TO_SEARCH)
                                break;
                        }
                    }

                    if (Configuration.ToSearchV3.HasFlag(Configuration.SearchSetting.Aetheryte) && !isInDuty && !isInCombat)
                    {
                        foreach (var aetheryte in Aetheryes)
                        {
                            var aetheryteName = AetheryteManager.GetAetheryteName(aetheryte);
                            var terriName = SearchDatabase.GetString<TerritoryType>(aetheryte.TerritoryId);
                            if (aetheryteName.ToLower().Contains(term) || terriName.Searchable.Contains(term))
                                cResults.Add(new AetheryteSearchResult
                                {
                                    Name = aetheryteName,
                                    Data = aetheryte,
                                    Icon = TexCache.AetheryteIcon,
                                    TerriName = terriName.Display
                                });

                            if (cResults.Count > MAX_TO_SEARCH)
                                break;
                        }
                    }

                    if (Configuration.ToSearchV3.HasFlag(Configuration.SearchSetting.Duty) && !isInDuty)
                    {
                        foreach (var cfc in SearchDatabase.GetAll<ContentFinderCondition>())
                        {
                            if (!GameStateCache.UnlockedDutyKeys.Contains(cfc.Key))
                                continue;

                            var row = Data.GetExcelSheet<ContentFinderCondition>()!.GetRow(cfc.Key);

                            if (row == null || row.ContentType == null)
                                continue;

                            /*
                            switch (row.ContentType.Row)
                            {
                                case 0: // Invalid
                                case 3: // Guildhests
                                case 7: // Quest Battles
                                case 8: // FATEs
                                case 9: // Treasure Hunts
                                case 20: // Novice Hall
                                case 21: // DD
                                case 26: // Eureka
                                    continue;
                            }
                            */

                            // Only include dungeon, trials, raids, ultimates
                            if (row.ContentType.Row is not (2 or 4 or 5 or 28))
                                continue;

                            if (cfc.Value.Searchable.Contains(term))
                            {
                                cResults.Add(new DutySearchResult
                                {
                                    CatName = row.ContentType?.Value?.Name ?? "Duty",
                                    DataKey = cfc.Key,
                                    Name = cfc.Value.Display,
                                    Icon = TexCache.ContentTypeIcons[row.ContentType.Row],
                                });
                            }

                            if (cResults.Count > MAX_TO_SEARCH)
                                break;
                        }

                        foreach (var contentRoulette in Data.GetExcelSheet<ContentRoulette>()!.Where(x => x.IsInDutyFinder))
                        {
                            var text = SearchDatabase.GetString<ContentRoulette>(contentRoulette.RowId);

                            if (text.Searchable.Contains(term))
                            {
                                cResults.Add(new ContentRouletteSearchResult()
                                {
                                    DataKey = (byte) contentRoulette.RowId,
                                    Name = contentRoulette.Category.ToDalamudString().TextValue
                                });
                            }

                            if (cResults.Count > MAX_TO_SEARCH)
                                break;
                        }
                    }

                    if (Configuration.ToSearchV3.HasFlag(Configuration.SearchSetting.MainCommand) && !isInEvent)
                    {
                        foreach (var mainCommand in SearchDatabase.GetAll<MainCommand>())
                        {
                            // Record ready check, internal ones
                            if (mainCommand.Key is 79 or 38 or 39 or 40 or 43 or 26)
                                continue;

                            var searchable = mainCommand.Value.Searchable;
                            if (searchable == "log out")
                                searchable = "logout";

                            if (searchable.Contains(term))
                            {
                                cResults.Add(new MainCommandSearchResult
                                {
                                    CommandId = mainCommand.Key,
                                    Name = mainCommand.Value.Display,
                                    Icon = TexCache.MainCommandIcons[mainCommand.Key]
                                });

                                if (cResults.Count > MAX_TO_SEARCH)
                                    break;
                            }
                        }
                    }

                    if (Configuration.ToSearchV3.HasFlag(Configuration.SearchSetting.GeneralAction) && !isInEvent)
                    {
                        var hasMelding = xivCommon.Functions.Journal.IsQuestCompleted(66175); // Waking the Spirit
                        var hasAdvancedMelding = xivCommon.Functions.Journal.IsQuestCompleted(66176); // Melding Materia Muchly

                        foreach (var generalAction in SearchDatabase.GetAll<GeneralAction>())
                        {
                            // Skip invalid entries, jump, etc
                            if (generalAction.Key is 2 or 3 or 1 or 0 or 11 or 26 or 27 or 16 or 17)
                                continue;

                            // Skip Materia Melding/Advanced Material Melding, based on what is unlocked
                            if ((!hasMelding || hasAdvancedMelding) && generalAction.Key is 12)
                                continue;
                            if (!hasAdvancedMelding && generalAction.Key is 13)
                                continue;

                            if (generalAction.Value.Searchable.Contains(term))
                                cResults.Add(new GeneralActionSearchResult
                                {
                                    Name = generalAction.Value.Display,
                                    Icon = TexCache.GeneralActionIcons[generalAction.Key]
                                });
                        }
                    }

                    if (Configuration.ToSearchV3.HasFlag(Configuration.SearchSetting.PluginSettings))
                    {
                        foreach (var plugin in DalamudReflector.OtherPlugins)
                        {
                            if (plugin.Name.ToLower().Contains(term))
                            {
                                cResults.Add(new PluginSettingsSearchResult
                                {
                                    Name = plugin.Name,
                                    Plugin = plugin,
                                });
                            }
                        }

                        foreach (var plugin in Ipc.TrackedIpcs)
                        {
                            foreach (var ipcBinding in plugin.Value)
                            {
                                if (ipcBinding.Search.Contains(term))
                                {
                                    cResults.Add(new IpcSearchResult
                                    {
                                        CatName = plugin.Key,
                                        Name = ipcBinding.Display,
                                        Guid = ipcBinding.Guid,
                                        Icon = TexCache.ExtraIcons[ipcBinding.IconId],
                                    });
                                }

                                // Limit IPC results to 25
                                if (cResults.Count > 25)
                                    break;
                            }
                        }
                    }

                    foreach (var kind in Enum.GetValues<InternalSearchResult.InternalSearchResultKind>())
                    {
                        if (InternalSearchResult.GetNameForKind(kind).ToLower().Contains(term))
                        {
                            cResults.Add(new InternalSearchResult
                            {
                                Kind = kind
                            });
                        }
                    }

                    if (Configuration.ToSearchV3.HasFlag(Configuration.SearchSetting.Emote) && !isInEvent)
                    {
                        foreach (var emoteRow in Data.GetExcelSheet<Emote>()!.Where(x => x.Order != 0 && GameStateCache.UnlockedEmoteKeys.Contains(x.RowId)))
                        {
                            var text = SearchDatabase.GetString<Emote>(emoteRow.RowId);
                            var slashCmd = emoteRow.TextCommand.Value!;
                            var slashCmdMatch = slashCmd.Command.RawString.Contains(term) ||
                                                slashCmd.Alias.RawString.Contains(term) ||
                                                slashCmd.ShortCommand.RawString.Contains(term) ||
                                                slashCmd.ShortAlias.RawString.Contains(term);

                            if (text.Searchable.Contains(term) || slashCmdMatch)
                            {
                                cResults.Add(new EmoteSearchResult
                                {
                                    Name = text.Display,
                                    SlashCommand = slashCmd.Command.RawString,
                                    Icon = TexCache.EmoteIcons[emoteRow.RowId]
                                });

                                if (cResults.Count > MAX_TO_SEARCH)
                                    break;
                            }
                        }
                    }

                    var expression = new Expression(searchTerm);

                    expression.EvaluateFunction += delegate(string name, FunctionArgs args)
                    {
                        switch (name)
                        {
                            case "lexp":
                                if (args.Parameters.Length == 1)
                                {
                                    var num = (int)args.EvaluateParameters()[0];
                                    args.Result = MathAux.GetNeededExpForLevel((uint)num);
                                    args.HasResult = true;
                                    PluginLog.Information($"exp called with {num} was {args.Result}",
                                        Array.Empty<object>());
                                }
                                else if (args.Parameters.Length == 0)
                                {
                                    args.Result = MathAux.GetNeededExpForCurrentLevel();
                                    args.HasResult = true;
                                }

                                break;
                            case "cexp":
                                if (args.Parameters.Length == 0)
                                {
                                    args.Result = MathAux.GetCurrentExp();
                                    args.HasResult = true;
                                }

                                break;
                            case "expleft":
                                if (args.Parameters.Length == 0)
                                {
                                    args.Result = MathAux.GetExpLeft();
                                    args.HasResult = true;
                                }

                                break;
                            case "lvl":
                                if (args.Parameters.Length == 0)
                                {
                                    args.Result = MathAux.GetLevel();
                                    args.HasResult = true;
                                }

                                break;
                            default:
                                args.Result = null;
                                args.HasResult = false;
                                break;
                        }
                    };

                    expression.EvaluateParameter += delegate(string sender, ParameterArgs args)
                    {
                        if (sender == "ans")
                        {
                            args.Result = lastAcceptedExpressionResult ?? 0;
                            args.HasResult = true;
                        }
                        else if (Configuration.MathConstants.ContainsKey(sender))
                        {
                            args.Result = Configuration.MathConstants[sender];
                            args.HasResult = true;
                        }
                        else
                        {
                            args.Result = null;
                            args.HasResult = false;
                        }
                    };

                    if (!expression.HasErrors())
                    {
                        try
                        {
                            var result = expression.Evaluate();
                            if (result is not 0)
                                cResults.Add(new ExpressionResult
                                {
                                    Result = result
                                });
                        }
                        catch (ArgumentException ex)
                        {
                            PluginLog.Verbose(ex, "Expression evaluate error", Array.Empty<object>());
                            if (searchTerm.Any(x => x is >= '0' and <= '9'))
                                cResults.Add(new ExpressionResult
                                {
                                    Result = null,
                                    HasError = true
                                });
                        }
                    }
                    else
                    {
                        PluginLog.Verbose("Expression parse error: " + expression.Error, Array.Empty<object>());
                        if (searchTerm.Any(x => x is >= '0' and <= '9'))
                            cResults.Add(new ExpressionResult
                            {
                                Result = null,
                                HasError = true
                            });
                    }
                }
                    break;

                case SearchMode.Wiki:
                {
                    var terriContent = Data.GetExcelSheet<ContentFinderCondition>()!
                        .FirstOrDefault(x => x.TerritoryType.Row == ClientState.TerritoryType);
                    if ("here".Contains(term) && terriContent != null)
                    {
                        cResults.Add(new WikiSearchResult
                        {
                            Name = terriContent.Name,
                            DataKey = terriContent.RowId,
                            Icon = TexCache.WikiIcon,
                            CatName = "Current Duty",
                            DataCat = WikiSearchResult.DataCategory.Instance,
                        });
                    }

                    cResults.Add(new SearchWikiSearchResult
                    {
                        Query = searchTerm
                    });

                    foreach (var cfc in SearchDatabase.GetAll<ContentFinderCondition>())
                    {
                        if (!GameStateCache.UnlockedDutyKeys.Contains(cfc.Key) && Configuration.WikiModeNoSpoilers)
                            continue;

                        if (cfc.Value.Searchable.Contains(term))
                            cResults.Add(new WikiSearchResult
                            {
                                Name = cfc.Value.Display,
                                DataKey = cfc.Key,
                                Icon = TexCache.WikiIcon,
                                CatName = "Duty",
                                DataCat = WikiSearchResult.DataCategory.Instance,
                            });

                        if (cResults.Count > MAX_TO_SEARCH)
                            break;
                    }

                    foreach (var quest in SearchDatabase.GetAll<Quest>())
                    {
                        if (quest.Value.Searchable.Contains(term))
                            cResults.Add(new WikiSearchResult
                            {
                                Name = quest.Value.Display,
                                DataKey = quest.Key,
                                Icon = TexCache.WikiIcon,
                                CatName = "Quest",
                                DataCat = WikiSearchResult.DataCategory.Quest,
                            });

                        if (cResults.Count > MAX_TO_SEARCH)
                            break;
                    }

                    foreach (var item in SearchDatabase.GetAll<Item>())
                    {
                        if (item.Value.Searchable.Contains(term))
                            cResults.Add(new WikiSearchResult
                            {
                                Name = item.Value.Display,
                                DataKey = item.Key,
                                Icon = TexCache.WikiIcon,
                                CatName = "Item",
                                DataCat = WikiSearchResult.DataCategory.Item,
                            });

                        if (cResults.Count > MAX_TO_SEARCH)
                            break;
                    }
                }
                    break;

                case SearchMode.WikiSiteChoicer:
                {
                    var wikiResult = choicerTempResult as WikiSearchResult;
                    if (!term.IsNullOrEmpty())
                    {
                        foreach (var kind in Enum.GetValues<WikiSiteChoicerResult.SiteChoice>())
                        {
                            if (kind == WikiSiteChoicerResult.SiteChoice.TeamCraft && wikiResult.DataCat == WikiSearchResult.DataCategory.Item)
                                continue;

                            if (kind.ToString().ToLower().Contains(term))
                            {
                                cResults.Add(new WikiSiteChoicerResult
                                {
                                    Site = kind
                                });
                            }
                        }
                    }

                    if (cResults.Count == 0)
                    {
                        cResults.Add(new WikiSiteChoicerResult
                        {
                            Site = WikiSiteChoicerResult.SiteChoice.GamerEscape
                        });

                        cResults.Add(new WikiSiteChoicerResult
                        {
                            Site = WikiSiteChoicerResult.SiteChoice.ConsoleGamesWiki
                        });

                        cResults.Add(new WikiSiteChoicerResult
                        {
                            Site = WikiSiteChoicerResult.SiteChoice.GarlandTools
                        });

                        if (wikiResult.DataCat == WikiSearchResult.DataCategory.Item)
                        {
                            cResults.Add(new WikiSiteChoicerResult
                            {
                                Site = WikiSiteChoicerResult.SiteChoice.TeamCraft
                            });
                        }
                    }
                }
                    break;

                case SearchMode.EmoteModeChoicer:
                {
                    foreach (var choice in Enum.GetValues<EmoteModeChoicerResult.EmoteModeChoice>())
                    {
                        cResults.Add(new EmoteModeChoicerResult
                        {
                            Choice = choice,
                        });
                    }
                }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (!isInDuty && searchTerm.StartsWith("/"))
            {
                cResults.Add(new ChatCommandSearchResult
                {
                    Command = searchTerm,
                });
            }

            results = cResults.ToArray();

#if DEBUG
            sw.Stop();
            PluginLog.Debug($"Took: {sw.ElapsedMilliseconds}ms");
#endif
        }

        public void Dispose()
        {
            PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
            PluginInterface.UiBuilder.Draw -= DrawUI;
            Framework.Update -= FrameworkOnUpdate;
            CommandManager.RemoveHandler(commandName);
            xivCommon.Dispose();

            TexCache.Dispose();
            Ipc.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            settingsWindow.IsOpen = true;

            if (args.Contains("reset"))
            {
                Configuration.HintLevel = Configuration.HintKind.HintTyping;
                Configuration.Save();
            }
        }

        private void OpenFinder()
        {
#if !DEBUG
            if (ClientState.LocalPlayer == null)
                return;
#endif
            if (this.finderOpen == true)
                return;

            if (Configuration.HintLevel != Configuration.HintKind.HintMath + 1)
            {
                var nextHint = Configuration.HintLevel++;
                PluginLog.Information($"Hint: {nextHint}");
                results = new ISearchResult[]
                {
                    new HintResult
                    {
                        HintLevel = nextHint
                    }
                };
                Configuration.Save();
            }

            if (Configuration.OnlyWikiMode)
            {
                searchMode = SearchMode.Wiki;
            }

            GameStateCache.Refresh();
            finderOpen = true;
        }

        private void CloseFinder()
        {
            finderOpen = false;
            searchTerm = string.Empty;
            selectedIndex = 0;
            searchMode = SearchMode.Top;
            choicerTempResult = null;
            UpdateSearchResults();
        }

        private const int MAX_ONE_PAGE = 10;
        private const int MAX_TO_SEARCH = 100;

        private void DrawUI()
        {
            if (!finderOpen)
                return;

            var closeFinder = false;

            ImGuiHelpers.ForceNextWindowMainViewport();

            var size = new Vector2(500, 40);
            size *= ImGuiHelpers.GlobalScale;

            var mainViewportSize = ImGuiHelpers.MainViewport.Size;
            var mainViewportMiddle = mainViewportSize / 2;
            var startPos = ImGuiHelpers.MainViewport.Pos + (mainViewportMiddle - (size / 2));

            startPos.Y -= 200;
            startPos += Configuration.PositionOffset;

            var scaledFour = 4 * ImGuiHelpers.GlobalScale;

            if (results != null)
                size.Y += Math.Min(results.Length, MAX_ONE_PAGE) * (21 * ImGuiHelpers.GlobalScale);

            ImGui.SetNextWindowPos(startPos);
            ImGui.SetNextWindowSize(size);
            ImGui.SetNextWindowSizeConstraints(size, new Vector2(size.X, size.Y + (400 * ImGuiHelpers.GlobalScale)));

            ImGui.Begin("###findeverything", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

            ImGui.PushItemWidth(size.X - (45 * ImGuiHelpers.GlobalScale));

            var searchHint = searchMode switch
            {
                SearchMode.Top => "Type to search...",
                SearchMode.Wiki => "Search in wikis...",
                SearchMode.WikiSiteChoicer => $"Choose site for \"{choicerTempResult.Name}\"...",
                SearchMode.EmoteModeChoicer => $"Choose emote mode for \"{choicerTempResult.Name}\"...",
                _ => throw new ArgumentOutOfRangeException()
            };

            var resetScroll = false;

            if (ImGui.InputTextWithHint("###findeverythinginput", searchHint, ref searchTerm, 1000,
                    ImGuiInputTextFlags.NoUndoRedo))
            {
                UpdateSearchResults();
                selectedIndex = 0;
                framesSinceLastKbChange = 0;
                resetScroll = true;
            }

            ImGui.PopItemWidth();

            if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && !ImGui.IsAnyItemActive() && !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                ImGui.SetKeyboardFocusHere(0);

            ImGui.SameLine();

            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.Text(FontAwesomeIcon.Search.ToIconString());
            ImGui.PopFont();

            if (!ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) || ImGui.IsKeyDown((int) VirtualKey.ESCAPE))
            {
                PluginLog.Verbose("Focus loss or escape");
                closeFinder = true;
            }

            var textSize = ImGui.CalcTextSize("poop");

            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8 * ImGuiHelpers.GlobalScale, scaledFour));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, new Vector2(scaledFour, scaledFour));

            if (results != null && results.Length > 0)
            {
                if (ImGui.BeginChild("###findAnythingScroller"))
                {
                    var childSize = ImGui.GetWindowSize();

                    var isDown = ImGui.IsKeyDown((int)VirtualKey.DOWN);
                    var isUp = ImGui.IsKeyDown((int)VirtualKey.UP);
                    var isPgUp = ImGui.IsKeyDown((int)VirtualKey.PRIOR);
                    var isPgDn = ImGui.IsKeyDown((int)VirtualKey.NEXT);

                    if (isDown && framesSinceButtonPress is 0 or > 20)
                    {
                        if (selectedIndex != results.Length - 1)
                        {
                            selectedIndex++;
                        }
                        else
                        {
                            selectedIndex = 0;
                        }

                        framesSinceLastKbChange = 0;
                        framesSinceButtonPress++;
                    }
                    else if (isUp && framesSinceButtonPress is 0 or > 20)
                    {
                        if (selectedIndex != 0)
                        {
                            selectedIndex--;
                        }
                        else
                        {
                            selectedIndex = results.Length - 1;
                        }

                        framesSinceLastKbChange = 0;
                        framesSinceButtonPress++;
                    }
                    else if(isUp || isDown || isPgUp || isPgDn)
                    {
                        framesSinceButtonPress++;
                    }
                    else if (!isDown && !isUp && !isPgUp && !isPgDn)
                    {
                        framesSinceButtonPress = 0;
                    }

                    if (isPgUp && framesSinceLastKbChange > 10)
                    {
                        selectedIndex = Math.Max(0, selectedIndex - MAX_ONE_PAGE);
                        framesSinceLastKbChange = 0;
                    }
                    else if (isPgDn && framesSinceLastKbChange > 10)
                    {
                        selectedIndex = Math.Min(results.Length - 1, selectedIndex + MAX_ONE_PAGE);
                        framesSinceLastKbChange = 0;
                    }

                    framesSinceLastKbChange++;
                    var clickedIndex= -1;

                    for (var i = 0; i < results.Length; i++)
                    {
                        var result = results[i];
                        if (ImGui.Selectable($"{result.Name}###faEntry{i}", i == selectedIndex, ImGuiSelectableFlags.None,
                                new Vector2(childSize.X, textSize.Y)))
                        {
                            PluginLog.Information("Selectable click");
                            clickedIndex = i;
                        }

                        var thisTextSize = ImGui.CalcTextSize(result.Name);

                        ImGui.SameLine(thisTextSize.X + scaledFour);

                        ImGui.TextColored(ImGuiColors.DalamudGrey, result.CatName);

                        if (result.Icon != null)
                        {
                            ImGui.SameLine(size.X - (50 * ImGuiHelpers.GlobalScale));
                            ImGui.Image(result.Icon.ImGuiHandle, new Vector2(17, 17) * ImGuiHelpers.GlobalScale);
                        }
                    }

                    if(isUp || isDown || isPgUp || isPgDn || resetScroll)
                    {
                        if (selectedIndex > 1)
                        {
                            ImGui.SetScrollY((selectedIndex - 1) * (textSize.Y + scaledFour));
                        }
                        else
                        {
                            ImGui.SetScrollY(0);
                        }
                    }

                    if (ImGui.IsKeyPressed((int) VirtualKey.RETURN) || clickedIndex != -1)
                    {
                        var index = clickedIndex == -1 ? selectedIndex : clickedIndex;
                        closeFinder = results[index].CloseFinder;
                        results[index].Selected();
                    }
                }

                ImGui.EndChild();
            }

            ImGui.PopStyleVar(2);

            ImGui.End();

            if (closeFinder)
            {
                CloseFinder();
            }
        }
    }
}
