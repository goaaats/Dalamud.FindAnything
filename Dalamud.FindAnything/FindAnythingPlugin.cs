using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading.Tasks;
using System.Web;
using Dalamud.Data;
using Dalamud.FindAnything.Game;
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
        [PluginService] public static AetheryteList Aetherytes { get; private set; }
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
        private int lastButtonPressTicks = 0;
        private bool isHeldTimeout = false;
        private bool isHeld = false;

        private int timeSinceLastShift = 0;
        private bool shiftArmed = false;
        private bool shiftOk = false;

        private WindowSystem windowSystem;
        private static SettingsWindow settingsWindow;
        private static GameWindow gameWindow;

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
        private static bool wasTempSearchMode = false;
        private static ISearchResult? choicerTempResult;

        private struct HistoryEntry
        {
            public ISearchResult Result;
            public string SearchTerm;
        }

        private static List<HistoryEntry> history = new();
        private const int HistoryMax = 5;

        private interface ISearchResult
        {
            public string CatName { get; }
            public string Name { get; }
            public TextureWrap? Icon { get; }
            public bool CloseFinder { get; }

            public void Selected();
        }

        private class WikiSearchResult : ISearchResult, IEquatable<WikiSearchResult>
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

            public bool Equals(WikiSearchResult other)
            {
                return this.DataKey == other.DataKey && this.DataCat == other.DataCat;
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((WikiSearchResult)obj);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(this.DataKey, (int)this.DataCat);
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

                if (name.StartsWith("_")) // "level sync" icon
                    name = name.Substring(2);

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

            private static bool teamcraftLocalFailed = false;
            private static void OpenTeamcraft(uint id) {
                if (teamcraftLocalFailed || Configuration.TeamCraftForceBrowser) {
                    Util.OpenLink($"https://ffxivteamcraft.com/db/en/item/{id}");
                    return;
                }

                Task.Run(() => {
                    try {
                        var wr = WebRequest.CreateHttp($"http://localhost:14500/db/en/item/{id}");
                        wr.Timeout = 500;
                        wr.Method = "GET";
                        wr.GetResponse().Close();
                    } catch {
                        try {
                            if (System.IO.Directory.Exists(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ffxiv-teamcraft"))) {
                                Util.OpenLink($"teamcraft:///db/en/item/{id}");
                            } else {
                                teamcraftLocalFailed = true;
                                Util.OpenLink($"https://ffxivteamcraft.com/db/en/item/{id}");
                            }
                        } catch {
                            teamcraftLocalFailed = true;
                            Util.OpenLink($"https://ffxivteamcraft.com/db/en/item/{id}");
                        }
                    }
                });
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
                        OpenTeamcraft(wikiResult.DataKey);
                    }
                        break;
                }
            }

            public bool Equals(WikiSiteChoicerResult other)
            {
                return this.Site == other.Site;
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((WikiSiteChoicerResult)obj);
            }

            public override int GetHashCode()
            {
                return (int)this.Site;
            }
        }

        private class SearchWikiSearchResult : ISearchResult, IEquatable<SearchWikiSearchResult>
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

            public bool Equals(SearchWikiSearchResult? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return this.Query == other.Query;
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((SearchWikiSearchResult)obj);
            }

            public override int GetHashCode()
            {
                return this.Query.GetHashCode();
            }
        }

        private class AetheryteSearchResult : ISearchResult, IEquatable<AetheryteSearchResult>
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
                    var showTeleportChatMessage = ShowTeleportChatMessageIpc.InvokeFunc();

                    if (!didTeleport)
                    {
                        UserError("Cannot teleport in this situation.");
                    }
                    else if (showTeleportChatMessage)
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

            public bool Equals(AetheryteSearchResult? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return this.Data.AetheryteId.Equals(other.Data.AetheryteId);
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((AetheryteSearchResult)obj);
            }

            public override int GetHashCode()
            {
                return this.Data.GetHashCode();
            }
        }

        private static void UserError(string error)
        {
            ChatGui.PrintError(error);
            ToastGui.ShowError(error);
        }

        private class MainCommandSearchResult : ISearchResult, IEquatable<MainCommandSearchResult>
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

            public bool Equals(MainCommandSearchResult? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return this.CommandId == other.CommandId;
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((MainCommandSearchResult)obj);
            }

            public override int GetHashCode()
            {
                return (int)this.CommandId;
            }
        }

        private class InternalSearchResult : ISearchResult, IEquatable<InternalSearchResult>
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

            public bool Equals(InternalSearchResult? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return this.Kind == other.Kind;
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((InternalSearchResult)obj);
            }

            public override int GetHashCode()
            {
                return (int)this.Kind;
            }
        }

        private static void SwitchSearchMode(SearchMode newMode)
        {
            searchMode = newMode;
            searchTerm = string.Empty;
            selectedIndex = 0;
            results = UpdateSearchResults(searchTerm);
            PluginLog.Information($"Now in mode: {newMode}");
        }

        public class GeneralActionSearchResult : ISearchResult, IEquatable<GeneralActionSearchResult>
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

            public bool Equals(GeneralActionSearchResult? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return this.Name == other.Name;
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((GeneralActionSearchResult)obj);
            }

            public override int GetHashCode()
            {
                return this.Name.GetHashCode();
            }
        }

        private class PluginSettingsSearchResult : ISearchResult, IEquatable<PluginSettingsSearchResult>
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

            public bool Equals(PluginSettingsSearchResult? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return this.Plugin.Name.Equals(other.Plugin.Name);
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((PluginSettingsSearchResult)obj);
            }

            public override int GetHashCode()
            {
                return this.Plugin.GetHashCode();
            }
        }

        private class MacroLinkSearchResult : ISearchResult, IEquatable<MacroLinkSearchResult>
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

            public bool Equals(MacroLinkSearchResult? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return this.Entry.Equals(other.Entry);
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((MacroLinkSearchResult)obj);
            }

            public override int GetHashCode()
            {
                return this.Entry.GetHashCode();
            }
        }

        private class DutySearchResult : ISearchResult, IEquatable<DutySearchResult>
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

            public bool Equals(DutySearchResult? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return this.DataKey == other.DataKey;
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((DutySearchResult)obj);
            }

            public override int GetHashCode()
            {
                return (int)this.DataKey;
            }
        }

        private class ContentRouletteSearchResult : ISearchResult, IEquatable<ContentRouletteSearchResult>
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

            public bool Equals(ContentRouletteSearchResult? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return this.DataKey == other.DataKey;
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ContentRouletteSearchResult)obj);
            }

            public override int GetHashCode()
            {
                return this.DataKey.GetHashCode();
            }
        }

        private class EmoteSearchResult : ISearchResult, IEquatable<EmoteSearchResult>
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

            public bool Equals(EmoteSearchResult? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return this.SlashCommand == other.SlashCommand;
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((EmoteSearchResult)obj);
            }

            public override int GetHashCode()
            {
                return this.SlashCommand.GetHashCode();
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

        private class ChatCommandSearchResult : ISearchResult, IEquatable<ChatCommandSearchResult>
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

            public bool Equals(ChatCommandSearchResult? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return this.Command == other.Command;
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ChatCommandSearchResult)obj);
            }

            public override int GetHashCode()
            {
                return this.Command.GetHashCode();
            }
        }

        private class IpcSearchResult : ISearchResult, IEquatable<IpcSearchResult>
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

            public bool Equals(IpcSearchResult? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return this.Guid == other.Guid;
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((IpcSearchResult)obj);
            }

            public override int GetHashCode()
            {
                return this.Guid.GetHashCode();
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

        private class GearsetSearchResult : ISearchResult, IEquatable<GearsetSearchResult>
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

            public bool Equals(GearsetSearchResult? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return this.Gearset.Slot.Equals(other.Gearset.Slot);
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((GearsetSearchResult)obj);
            }

            public override int GetHashCode()
            {
                return this.Gearset.GetHashCode();
            }
        }

        private class MountResult : ISearchResult, IEquatable<MountResult>
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

            public bool Equals(MountResult? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return this.Mount.RowId.Equals(other.Mount.RowId);
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((MountResult)obj);
            }

            public override int GetHashCode()
            {
                return this.Mount.GetHashCode();
            }
        }

        private class MinionResult : ISearchResult, IEquatable<MinionResult>
        {
            public string CatName => "Minion";
            public string Name => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Minion.Singular);
            public TextureWrap? Icon => TexCache.MinionIcons[Minion.RowId];
            public bool CloseFinder => true;

            public Companion Minion { get; set; }

            public void Selected() {
                xivCommon.Functions.Chat.SendMessage($"/minion \"{Minion.Singular}\"");
            }

            public bool Equals(MinionResult? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return this.Minion.RowId.Equals(other.Minion.RowId);
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((MinionResult)obj);
            }

            public override int GetHashCode()
            {
                return this.Minion.GetHashCode();
            }
        }

        private class CraftingRecipeResult : ISearchResult, IEquatable<CraftingRecipeResult> {
            public string CatName => "Crafting Recipe";
            public string Name { get; set; }
            public TextureWrap? Icon { get; set; }
            public bool CloseFinder => true;

            public Recipe Recipe { get; set; }

            public void Selected() {
                var id = this.Recipe.ItemResult.Value?.RowId ?? 0;
                if (id > 0) {
                    GameStateCache.SearchForItemByCraftingMethod((ushort) (id % 500_000));
                }
            }

            public bool Equals(CraftingRecipeResult? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return this.Recipe.RowId.Equals(other.Recipe.RowId);
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((CraftingRecipeResult)obj);
            }

            public override int GetHashCode()
            {
                return this.Recipe.GetHashCode();
            }
        }

        private class GatheringItemResult : ISearchResult, IEquatable<GatheringItemResult> {
            public string CatName => "Gathering Item";
            public string Name { get; set; }
            public TextureWrap? Icon { get; set; }
            public bool CloseFinder => true;

            public GatheringItem Item { get; set; }

            public void Selected() {
                if (this.Item.Item > 0) {
                    GameStateCache.SearchForItemByGatheringMethod((ushort) (this.Item.Item % 500_000));
                }
            }

            public bool Equals(GatheringItemResult? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return this.Item.RowId.Equals(other.Item.RowId);
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((GatheringItemResult)obj);
            }

            public override int GetHashCode()
            {
                return this.Item.GetHashCode();
            }
        }

        private class GameSearchResult : ISearchResult
        {
            public string CatName => string.Empty;
            public string Name => "DN Farm";
            public TextureWrap? Icon => TexCache.GameIcon;
            public bool CloseFinder => true;

            public void Selected()
            {
                gameWindow.IsOpen = true;
            }
        }

        private static ISearchResult[]? results;

        public static ICallGateSubscriber<uint, byte, bool> TeleportIpc { get; private set; }
        public static ICallGateSubscriber<bool> ShowTeleportChatMessageIpc {get; private set; }

        public FindAnythingPlugin()
        {
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);

            CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open the Wotsit settings."
            });

            CommandManager.AddHandler("/bountifuldn", new CommandInfo((_, _) => gameWindow.Cheat())
            {
                HelpMessage = "Open the Wotsit settings.",
                ShowInHelp = false
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
            ShowTeleportChatMessageIpc = PluginInterface.GetIpcSubscriber<bool>("Teleport.ChatMessage");

            TexCache = TextureCache.Load(null, Data);
            SearchDatabase = SearchDatabase.Load();
            AetheryteManager = AetheryteManager.Load();

            windowSystem = new WindowSystem("wotsit");
            settingsWindow = new SettingsWindow(this) { IsOpen = false };
            gameWindow = new GameWindow { IsOpen = false };
            windowSystem.AddWindow(settingsWindow);
            windowSystem.AddWindow(gameWindow);
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

            if (Configuration.NotInCombat && CheckInCombat())
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
                        var mod2 = Configuration.ComboModifier2 == VirtualKey.NO_KEY || Input.IsDown(Configuration.ComboModifier2);
                        var key = Configuration.ComboKey == VirtualKey.NO_KEY || Input.IsDown(Configuration.ComboKey);

                        var wiki = Configuration.WikiComboKey != VirtualKey.NO_KEY && Input.IsDown(Configuration.WikiComboKey);

                        if (mod && mod2 && key)
                        {
                            OpenFinder();

                            if (wiki)
                            {
                                searchTerm = "?";
                            }

                            if (Configuration.PreventPassthrough)
                            {
                                UnsetKey(Configuration.ComboModifier);
                                UnsetKey(Configuration.ComboModifier2);
                                UnsetKey(Configuration.ComboKey);

                                if (wiki)
                                    UnsetKey(Configuration.WikiComboKey);
                            }
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private static void UnsetKey(VirtualKey key)
        {
            if ((int)key <= 0 || (int)key >= 240)
                return;

            Keys[key] = false;
        }

        private static bool CheckInDuty()
        {
            // Island Sanctuary
            if (ClientState.TerritoryType == 1055) return false;

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

        private static ISearchResult[]? UpdateSearchResults(string toSearch)
        {
            if (toSearch.IsNullOrEmpty() && searchMode != SearchMode.WikiSiteChoicer && searchMode != SearchMode.EmoteModeChoicer)
            {
                return null;
            }

#if DEBUG
            var sw = Stopwatch.StartNew();
#endif

            var term = toSearch.ToLower().Replace("'", string.Empty);
            var isInDuty = CheckInDuty();
            var isInEvent = CheckInEvent();
            var isInCombat = CheckInCombat();

            var cResults = new List<ISearchResult>();

            switch (searchMode)
            {
                case SearchMode.Top:
                {
                    foreach (var setting in Configuration.Order)
                    {
                        switch (setting)
                        {
                            case Configuration.SearchSetting.Duty:
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

                                    foreach (var contentRoulette in Data.GetExcelSheet<ContentRoulette>()!.Where(x => x.IsInDutyFinder)) // Also filter !row 7 + 10 here, but not in Lumina schemas yet
                                    {
                                        var text = SearchDatabase.GetString<ContentRoulette>(contentRoulette.RowId);

                                        if (text.Searchable.Contains(term))
                                        {
                                            var name = contentRoulette.Category.ToDalamudString().TextValue;
                                            if (name.IsNullOrWhitespace())
                                                name = text.Display;

                                            cResults.Add(new ContentRouletteSearchResult()
                                            {
                                                DataKey = (byte) contentRoulette.RowId,
                                                Name = name,
                                            });
                                        }

                                        if (cResults.Count > MAX_TO_SEARCH)
                                            break;
                                    }
                                }
                                break;
                            case Configuration.SearchSetting.Aetheryte:
                                if (Configuration.ToSearchV3.HasFlag(Configuration.SearchSetting.Aetheryte) && !isInDuty && !isInCombat)
                                {
                                    var marketBoardResults = new List<AetheryteEntry>();
                                    var strikingDummyResults = new List<AetheryteEntry>();
                                    foreach (var aetheryte in Aetherytes)
                                    {
                                        var aetheryteName = AetheryteManager.GetAetheryteName(aetheryte);
                                        var terriName = SearchDatabase.GetString<TerritoryType>(aetheryte.TerritoryId);
                                        if (aetheryteName.ToLower().Replace("'", string.Empty).Contains(term) || terriName.Searchable.Contains(term))
                                            cResults.Add(new AetheryteSearchResult
                                            {
                                                Name = aetheryteName,
                                                Data = aetheryte,
                                                Icon = TexCache.AetheryteIcon,
                                                TerriName = terriName.Display
                                            });

                                        if (Configuration.DoMarketBoardShortcut && "Closest Market Board".ToLower().Contains(term) && AetheryteManager.IsMarketBoardAetheryte(aetheryte.AetheryteId))
                                            marketBoardResults.Add(aetheryte);

                                        if (Configuration.DoStrikingDummyShortcut && "Closest Striking Dummy".ToLower().Contains(term) && AetheryteManager.IsStrikingDummyAetheryte(aetheryte.AetheryteId))
                                            strikingDummyResults.Add(aetheryte);

                                        if (cResults.Count > MAX_TO_SEARCH)
                                            break;
                                    }
                                    if (marketBoardResults.Count > 0)
                                    {
                                        var closestMarketBoard = marketBoardResults.OrderBy(a1 => a1.GilCost).First();
                                        var terriName = SearchDatabase.GetString<TerritoryType>(closestMarketBoard.TerritoryId);
                                        cResults.Add(new AetheryteSearchResult
                                        {
                                            Name = "Closest Market Board",
                                            Data = closestMarketBoard,
                                            Icon = TexCache.AetheryteIcon,
                                            TerriName = terriName.Display
                                        });
                                    }
                                    if (strikingDummyResults.Count > 0)
                                    {
                                        var closestStrikingDummy = strikingDummyResults.OrderBy(a1 => a1.GilCost).First();
                                        var terriName = SearchDatabase.GetString<TerritoryType>(closestStrikingDummy.TerritoryId);
                                        cResults.Add(new AetheryteSearchResult
                                        {
                                            Name = "Closest Striking Dummy",
                                            Data = closestStrikingDummy,
                                            Icon = TexCache.AetheryteIcon,
                                            TerriName = terriName.Display
                                        });
                                    }
                                }
                                break;
                            case Configuration.SearchSetting.MainCommand:
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
                                break;
                            case Configuration.SearchSetting.GeneralAction:
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
                                break;
                            case Configuration.SearchSetting.Emote:
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
                                break;
                            case Configuration.SearchSetting.PluginSettings:
                                if (Configuration.ToSearchV3.HasFlag(Configuration.SearchSetting.PluginSettings))
                                {
                                    DalamudReflector.RefreshPlugins();

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
                                break;
                            case Configuration.SearchSetting.Gearsets:
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
                                break;
                            case Configuration.SearchSetting.CraftingRecipes:
                                if (Configuration.ToSearchV3.HasFlag(Configuration.SearchSetting.CraftingRecipes)) {
                                    foreach (var recipeSearch in SearchDatabase.GetAll<Recipe>()) {
                                        if (recipeSearch.Value.Searchable.Contains(term))
                                        {
                                            var recipe = Data.GetExcelSheet<Recipe>()!.GetRow(recipeSearch.Key)!;
                                            var itemResult = recipe.ItemResult.Value!;

                                            TexCache.EnsureExtraIcon(itemResult.Icon);
                                            TexCache.ExtraIcons.TryGetValue(itemResult.Icon, out var tex);

                                            cResults.Add(new CraftingRecipeResult
                                            {
                                                Recipe = recipe,
                                                Name = recipeSearch.Value.Display,
                                                Icon = tex,
                                            });
                                        }

                                        if (cResults.Count > MAX_TO_SEARCH)
                                            break;
                                    }
                                }
                                break;
                            case Configuration.SearchSetting.GatheringItems:
                                if (Configuration.ToSearchV3.HasFlag(Configuration.SearchSetting.GatheringItems)) {
                                    var items = Data.GetExcelSheet<Item>()!;
                                    var gatheringItem = Data.GetExcelSheet<GatheringItem>()!;

                                    foreach (var gatherSearch in SearchDatabase.GetAll<GatheringItem>())
                                    {
                                        var gather = gatheringItem.GetRow(gatherSearch.Key)!;
                                        var item = items.GetRow((uint) gather.Item);

                                        if (item == null || item.RowId == 0) {
                                            continue;
                                        }

                                        if (gatherSearch.Value.Searchable.Contains(term)) {
                                            TexCache.EnsureExtraIcon(item.Icon);
                                            TexCache.ExtraIcons.TryGetValue(item.Icon, out var tex);

                                            cResults.Add(new GatheringItemResult()
                                            {
                                                Item = gather,
                                                Name = gatherSearch.Value.Display,
                                                Icon = tex,
                                            });
                                        }

                                        if (cResults.Count > MAX_TO_SEARCH)
                                            break;
                                    }
                                }
                                break;
                            case Configuration.SearchSetting.Mounts:
                                // This is nasty, should just use TerritoryIntendedUse...
                                var isInNoMountDuty = isInDuty;
                                var currentTerri = Data.GetExcelSheet<TerritoryType>()?
                                    .GetRow(ClientState.TerritoryType);

                                if (currentTerri != null && currentTerri.ContentFinderCondition.Row != 0)
                                {
                                    var type = currentTerri.ContentFinderCondition.Value.ContentType.Row;
                                    if (type == 26 || type == 29)
                                        isInNoMountDuty = false;
                                }

                                if (Configuration.ToSearchV3.HasFlag(Configuration.SearchSetting.Mounts) && !isInNoMountDuty && !isInCombat)
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
                                break;
                            case Configuration.SearchSetting.Minions:
                                if (Configuration.ToSearchV3.HasFlag(Configuration.SearchSetting.Minions) && !isInDuty && !isInCombat)
                                {
                                    foreach (var minion in Data.GetExcelSheet<Companion>()!)
                                    {
                                        if (!GameStateCache.UnlockedMinionKeys.Contains(minion.RowId))
                                            continue;

                                        if (minion.Singular.RawString.ToLower().Contains(term))
                                        {
                                            cResults.Add(new MinionResult
                                            {
                                                Minion = minion,
                                            });
                                        }
                                    }
                                }
                                break;
                            case Configuration.SearchSetting.MacroLinks:
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
                                break;
                            case Configuration.SearchSetting.Internal:
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
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }

                    if (Configuration.ToSearchV3.HasFlag(Configuration.SearchSetting.Maths))
                    {
                        var expression = new Expression(toSearch);

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
                                if (toSearch.Any(x => x is >= '0' and <= '9'))
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
                            if (toSearch.Any(x => x is >= '0' and <= '9'))
                                cResults.Add(new ExpressionResult
                                {
                                    Result = null,
                                    HasError = true
                                });
                        }

                        if ("dn farm".Contains(term))
                            cResults.Add(new GameSearchResult());
                    }
                }
                    break;

                case SearchMode.Wiki:
                {
                    if (term.StartsWith("?"))
                        term = term[1..];

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
                        Query = toSearch.StartsWith("?") ? toSearch[1..] : toSearch,
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

            if (!isInDuty && toSearch.StartsWith("/"))
            {
                cResults.Add(new ChatCommandSearchResult
                {
                    Command = toSearch,
                });
            }

#if DEBUG
            sw.Stop();
            PluginLog.Debug($"Took: {sw.ElapsedMilliseconds}ms");
#endif

            return cResults.ToArray();
        }

        public void Dispose()
        {
            PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
            PluginInterface.UiBuilder.Draw -= DrawUI;
            Framework.Update -= FrameworkOnUpdate;
            CommandManager.RemoveHandler(commandName);
            CommandManager.RemoveHandler("/bountifuldn");
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
            else if (Configuration.HistoryEnabled && history.Count > 0)
            {
                var historyResults = new List<ISearchResult>();
                var newHistory = new List<HistoryEntry>();
                
                PluginLog.Verbose("{Num} histories:", history.Count);

                foreach (var historyEntry in history)
                {
                    var searched = UpdateSearchResults(historyEntry.SearchTerm);
                    PluginLog.Verbose(" => {Name}, {Type}, {ResultsNow}, {Term}", historyEntry.Result?.CatName, historyEntry.Result?.GetType()?.FullName, searched?.Length, historyEntry.SearchTerm);
                    
                    
                    var first = searched?.FirstOrDefault(x => x.Equals(historyEntry.Result));
                    if (first == null)
                    {
                        PluginLog.Verbose("Couldn't find {Term} anymore, removing from history", historyEntry.SearchTerm);
                        continue;
                    }

                    newHistory.Add(historyEntry);
                    historyResults.Add(first);
                }

                results = historyResults.ToArray();
                history = newHistory;
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
            isHeld = false;
            wasTempSearchMode = false;
            results = UpdateSearchResults(searchTerm);
        }

        private const int MAX_ONE_PAGE = 10;
        private const int MAX_TO_SEARCH = 100;

        private int GetTickCount() => Environment.TickCount & Int32.MaxValue;

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

            if (!searchTerm.IsNullOrEmpty())
            {
                switch (searchTerm[0])
                {
                    case '?':
                        if (searchMode == SearchMode.Top)
                        {
                            searchMode = SearchMode.Wiki;
                            wasTempSearchMode = true;
                            PluginLog.Information("AutoSearchMode: Wiki");
                        }
                        break;
                    default:
                        if (wasTempSearchMode)
                        {
                            searchMode = SearchMode.Top;
                            wasTempSearchMode = false;
                            results = UpdateSearchResults(searchTerm);
                            PluginLog.Information("AutoSearchMode: Top");
                        }
                        break;
                }
            }

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
                results = UpdateSearchResults(searchTerm);
                selectedIndex = 0;
                framesSinceLastKbChange = 0;
                resetScroll = true;
            }

            ImGui.PopItemWidth();

            if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && !ImGui.IsAnyItemActive() && !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                ImGui.SetKeyboardFocusHere(-1);

            ImGui.SameLine();

            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.Text(FontAwesomeIcon.Search.ToIconString());
            ImGui.PopFont();

            if (!ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) || ImGui.IsKeyDown(ImGuiHelpers.VirtualKeyToImGuiKey(VirtualKey.ESCAPE)))
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
                    
                    //var isCtrl = ImGui.IsKeyDown(ImGuiHelpers.VirtualKeyToImGuiKey(Configuration.ComboModifier));
                    var modifierImGuiKey = ImGuiHelpers.VirtualKeyToImGuiKey(Configuration.ComboModifier);
                    if (Configuration.ComboModifier == VirtualKey.CONTROL) modifierImGuiKey = ImGuiKey.ModCtrl;
                    if (Configuration.ComboModifier == VirtualKey.MENU) modifierImGuiKey = ImGuiKey.ModAlt;
                    if (Configuration.ComboModifier == VirtualKey.SHIFT) modifierImGuiKey = ImGuiKey.ModShift;
                    
                    var isCtrl = ImGui.IsKeyDown(modifierImGuiKey);
                    var isDown = ImGui.IsKeyDown(ImGuiHelpers.VirtualKeyToImGuiKey(VirtualKey.DOWN));
                    var isUp = ImGui.IsKeyDown(ImGuiHelpers.VirtualKeyToImGuiKey(VirtualKey.UP));
                    var isPgUp = ImGui.IsKeyDown(ImGuiHelpers.VirtualKeyToImGuiKey(VirtualKey.PRIOR));
                    var isPgDn = ImGui.IsKeyDown(ImGuiHelpers.VirtualKeyToImGuiKey(VirtualKey.NEXT));

                    var numKeysPressed = new bool[10];
                    for (var i = 0; i < 9; i++)
                    {
                        numKeysPressed[i] = ImGui.IsKeyPressed(ImGuiKey._1 + i);
                    }

                    void CursorDown()
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
                    }

                    void CursorUp()
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
                    }

                    var scrollSpeedTicks = Configuration.Speed switch {
                        Configuration.ScrollSpeed.Slow => 120,
                        Configuration.ScrollSpeed.Medium => 65,
                        Configuration.ScrollSpeed.Fast => 30,
                        _ => throw new ArgumentOutOfRangeException()
                    };

                    const int holdTimeout = 120;
                    var ticks = GetTickCount();
                    var ticksSinceLast = ticks - lastButtonPressTicks;
                    if (isDown && !isHeld)
                    {
                        CursorDown();
                        lastButtonPressTicks = ticks;
                        isHeld = true;
                        isHeldTimeout = true;
                    }
                    else if (isDown && isHeld)
                    {
                        switch (isHeldTimeout)
                        {
                            case true when ticksSinceLast > holdTimeout:
                                isHeldTimeout = false;
                                break;
                            case false when ticksSinceLast > scrollSpeedTicks:
                                CursorDown();
                                lastButtonPressTicks = ticks;
                                break;
                        }
                    }
                    else if (isUp && !isHeld)
                    {
                        CursorUp();
                        lastButtonPressTicks = ticks;
                        isHeld = true;
                        isHeldTimeout = true;
                    }
                    else if (isUp && isHeld)
                    {
                        switch (isHeldTimeout)
                        {
                            case true when ticksSinceLast > holdTimeout:
                                isHeldTimeout = false;
                                break;
                            case false when ticksSinceLast > scrollSpeedTicks:
                                CursorUp();
                                lastButtonPressTicks = ticks;
                                break;
                        }
                    }
                    else if (!isDown && !isUp)
                    {
                        isHeld = false;
                        isHeldTimeout = false;
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

                        if (i < 9 && Configuration.QuickSelectKey != VirtualKey.NO_KEY)
                        {
                            ImGui.SameLine(size.X - (65 * ImGuiHelpers.GlobalScale));
                            ImGui.TextColored(ImGuiColors.DalamudGrey, (i + 1).ToString());
                        }

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

                    if (isCtrl && numKeysPressed.Any(x => x))
                    {
                        clickedIndex = Array.IndexOf(numKeysPressed, true);
                    }

                    if (ImGui.IsKeyPressed(ImGuiHelpers.VirtualKeyToImGuiKey(VirtualKey.RETURN)) || clickedIndex != -1)
                    {
                        var index = clickedIndex == -1 ? selectedIndex : clickedIndex;
                        
                        if (index < results.Length)
                        {
                            closeFinder = results[index].CloseFinder;
                            results[index].Selected();

                            // results can be null here, as the wiki mode choicer nulls it when selected
                            if (results != null && searchMode == SearchMode.Top)
                            {
                                if (!history.Any(x => x.Result == results[index]))
                                {
                                    history.Insert(0, new HistoryEntry
                                    {
                                        Result = results[index],
                                        SearchTerm = searchTerm,
                                    });
                                }

                                if (history.Count > HistoryMax)
                                {
                                    history.RemoveAt(history.Count - 1);
                                }
                            }
                        }
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