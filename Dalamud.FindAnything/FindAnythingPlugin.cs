using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Web;
using Dalamud.Data;
using Dalamud.FindAnything;
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
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using ImGuiNET;
using ImGuiScene;
using Lumina.Excel.GeneratedSheets;
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

        public static TextureCache TexCache { get; private set; }
        private static SearchDatabase SearchDatabase { get; set; }
        private static AetheryteManager AetheryteManager { get; set; }

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
        }

        private static SearchMode searchMode = SearchMode.Top;
        private static WikiSearchResult? wikiSiteChoicerResult;

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
                wikiSiteChoicerResult = this;
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
                TeamCraft,
            }
            
            public SiteChoice Site { get; set; }
            
            public bool CloseFinder => true;
            
            private static void OpenWikiPage(string input)
            {
                var name = input.Replace(' ', '_');
                name = name.Replace('–', '-');
                Util.OpenLink($"https://ffxiv.gamerescape.com/wiki/{name}?useskin=Vector");
            }
            
            public void Selected()
            {
                if (wikiSiteChoicerResult == null)
                    throw new Exception("wikiSiteChoicerResult was null!");

                switch (Site)
                {
                    case SiteChoice.GamerEscape:
                    {
                        OpenWikiPage(wikiSiteChoicerResult.Name);
                    }
                        break;
                    case SiteChoice.GarlandTools:
                    {
                        switch (wikiSiteChoicerResult.DataCat)
                        {
                            case WikiSearchResult.DataCategory.Instance:
                                Util.OpenLink($"https://garlandtools.org/db/#instance/{wikiSiteChoicerResult.DataKey}");
                                break;
                            case WikiSearchResult.DataCategory.Quest:
                                Util.OpenLink($"https://garlandtools.org/db/#quest/{wikiSiteChoicerResult.DataKey}");
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                        break;
                    case SiteChoice.TeamCraft:
                    {
                        Util.OpenLink($"https://ffxivteamcraft.com/db/en/item/{wikiSiteChoicerResult.DataKey}");
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
            public string CatName => "Aetherytes";
            public string Name { get; set; }
            public TextureWrap? Icon { get; set; }
            public AetheryteEntry Data { get; set; }
            
            public bool CloseFinder => true;

            public void Selected()
            {
                var didTeleport = false;
                try
                {
                    didTeleport = TeleportIpc.InvokeFunc(Data.AetheryteId, Data.SubIndex);
                }
                catch (IpcNotReadyError)
                {
                    PluginLog.Error("Teleport IPC not found.");
                    didTeleport = false;
                }

                if (!didTeleport)
                {
                    var error = "To use Aetherytes within Wotsit, you must install the \"Teleporter\" plugin.";
                    ChatGui.PrintError(error);
                    ToastGui.ShowError(error);
                }
                else
                {
                    ChatGui.Print($"Teleporting to {Name}...");
                }
            }
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
        }

        private void FrameworkOnUpdate(Framework framework)
        {
            if (Keys[VirtualKey.ESCAPE])
            {
                CloseFinder();
            }
            else
            {
                switch (Configuration.Open)
                {
                    case Configuration.OpenMode.ShiftShift:
                        var shiftDown = Keys[Configuration.ShiftShiftKey];

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
                        if (Keys[Configuration.ComboModifier] && Keys[Configuration.ComboKey])
                        {
                            OpenFinder();
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private static void UpdateSearchResults()
        {
            if (searchTerm.IsNullOrEmpty() && searchMode != SearchMode.WikiSiteChoicer)
            {
                results = null;
                return;
            }

            PluginLog.Information("Searching: " + searchTerm);
            var term = searchTerm.ToLower();

            var cResults = new List<ISearchResult>();

            switch (searchMode)
            {
                case SearchMode.Top:
                {
                    if (Configuration.ToSearch.HasFlag(Configuration.SearchSetting.Aetheryte))
                    {
                        if (!(Condition[ConditionFlag.BoundByDuty] || Condition[ConditionFlag.BoundByDuty56] ||
                              Condition[ConditionFlag.BoundByDuty95]) || Condition[ConditionFlag.Occupied] ||
                            Condition[ConditionFlag.OccupiedInCutSceneEvent])
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
                                        Icon = TexCache.AetheryteIcon
                                    });

                                if (cResults.Count > MAX_TO_SEARCH)
                                    break;
                            }
                        }
                    }

                    if (Configuration.ToSearch.HasFlag(Configuration.SearchSetting.MainCommand))
                    {
                        foreach (var mainCommand in SearchDatabase.GetAll<MainCommand>())
                        {
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

                    if (Configuration.ToSearch.HasFlag(Configuration.SearchSetting.GeneralAction))
                    {
                        foreach (var generalAction in SearchDatabase.GetAll<GeneralAction>())
                        {
                            // Skip invalid entries, jump, etc
                            if (generalAction.Key is 2 or 3 or 1 or 0 or 11 or 26 or 27 or 16 or 17)
                                continue;

                            if (generalAction.Value.Searchable.Contains(term))
                                cResults.Add(new GeneralActionSearchResult
                                {
                                    Name = generalAction.Value.Display,
                                    Icon = TexCache.GeneralActionIcons[generalAction.Key]
                                });
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
                }
                    break;

                case SearchMode.Wiki:
                {
                    foreach (var cfc in SearchDatabase.GetAll<ContentFinderCondition>())
                    {
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
                }
                    break;

                case SearchMode.WikiSiteChoicer:
                {
                    cResults.Add(new WikiSiteChoicerResult
                    {
                        Site = WikiSiteChoicerResult.SiteChoice.GamerEscape
                    });
                    
                    cResults.Add(new WikiSiteChoicerResult
                    {
                        Site = WikiSiteChoicerResult.SiteChoice.GarlandTools
                    });

                    if (wikiSiteChoicerResult.DataCat == WikiSearchResult.DataCategory.Item)
                    {
                        cResults.Add(new WikiSiteChoicerResult
                        {
                            Site = WikiSiteChoicerResult.SiteChoice.TeamCraft
                        });
                    }
                }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            results = cResults.ToArray();
            PluginLog.Information($"{results.Length} results.");
        }

        public void Dispose()
        {
            PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
            PluginInterface.UiBuilder.Draw -= DrawUI;
            Framework.Update -= FrameworkOnUpdate;
            CommandManager.RemoveHandler(commandName);
            xivCommon.Dispose();

            TexCache.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            settingsWindow.IsOpen = true;
        }

        private void OpenFinder()
        {
#if !DEBUG
            if (ClientState.LocalPlayer == null)
                return;
#endif
            
            finderOpen = true;
        }

        private void CloseFinder()
        {
            finderOpen = false;
            searchTerm = string.Empty;
            selectedIndex = 0;
            searchMode = SearchMode.Top;
            wikiSiteChoicerResult = null;
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
            var mainViewportSize = ImGuiHelpers.MainViewport.Size;
            var mainViewportMiddle = mainViewportSize / 2;
            var startPos = ImGuiHelpers.MainViewport.Pos + (mainViewportMiddle - (size / 2));

            startPos.Y -= 200;

            if (results != null)
                size.Y += Math.Min(results.Length, MAX_ONE_PAGE) * 21;

            ImGui.SetNextWindowPos(startPos);
            ImGui.SetNextWindowSize(size);
            ImGui.SetNextWindowSizeConstraints(size, new Vector2(size.X, size.Y + 400));

            ImGui.Begin("###findeverything", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

            ImGui.PushItemWidth(size.X - 45);

            var searchHint = searchMode switch
            {
                SearchMode.Top => "Type to search...",
                SearchMode.Wiki => "Search in wikis...",
                SearchMode.WikiSiteChoicer => "Choose site...",
                _ => throw new ArgumentOutOfRangeException()
            };

            if (ImGui.InputTextWithHint("###findeverythinginput", searchHint, ref searchTerm, 1000,
                    ImGuiInputTextFlags.NoUndoRedo))
            {
                UpdateSearchResults();
                selectedIndex = 0;
                framesSinceLastKbChange = 0;
            }

            ImGui.PopItemWidth();

            if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && !ImGui.IsAnyItemActive() && !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                ImGui.SetKeyboardFocusHere(0);

            ImGui.SameLine();

            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.Text(FontAwesomeIcon.Search.ToIconString());
            ImGui.PopFont();

            if (!ImGui.IsWindowFocused() || ImGui.IsKeyDown((int) VirtualKey.ESCAPE))
            {
                closeFinder = true;
            }

            var textSize = ImGui.CalcTextSize("poop");

            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 4));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, new Vector2(4, 4));

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

                    for (var i = 0; i < results.Length; i++)
                    {
                        var result = results[i];
                        ImGui.Selectable($"{result.Name}", i == selectedIndex, ImGuiSelectableFlags.None, new Vector2(childSize.X, textSize.Y));

                        var thisTextSize = ImGui.CalcTextSize(result.Name);

                        ImGui.SameLine(thisTextSize.X + 4);

                        ImGui.TextColored(ImGuiColors.DalamudGrey, result.CatName);

                        if (result.Icon != null)
                        {
                            ImGui.SameLine(size.X - 50);
                            ImGui.Image(result.Icon.ImGuiHandle, new Vector2(16, 16));
                        }
                    }

                    if (selectedIndex > 1)
                    {
                        ImGui.SetScrollY((selectedIndex - 1) * (textSize.Y + 4));
                    }
                    else
                    {
                        ImGui.SetScrollY(0);
                    }

                    if (ImGui.IsKeyPressed((int) VirtualKey.RETURN))
                    {
                        closeFinder = results[selectedIndex].CloseFinder;
                        results[selectedIndex].Selected();
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