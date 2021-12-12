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
        public Configuration Configuration { get; set; }
        [PluginService] public static Framework Framework { get; private set; }
        [PluginService] public static DataManager Data { get; private set; }
        [PluginService] public static KeyState Keys { get; private set; }
        [PluginService] public static ClientState ClientState { get; private set; }
        [PluginService] public static ChatGui ChatGui { get; private set; }
        [PluginService] public static ToastGui ToastGui { get; private set; }
        [PluginService] public static Dalamud.Game.ClientState.Conditions.Condition Condition { get; private set; }
        [PluginService] public static AetheryteList Aetheryes { get; private set; }

        public static TextureCache TexCache { get; private set; }
        private SearchDatabase SearchDatabase { get; init; }
        private AetheryteManager AetheryteManager { get; init; }

        private bool finderOpen = false;
        private string searchTerm = string.Empty;
        private int selectedIndex = 0;

        private int framesSinceLastKbChange = 0;
        private int framesSinceButtonPress = 0;

        private int timeSinceLastShift = 0;
        private bool shiftArmed = false;
        private bool shiftOk = false;

        private WindowSystem windowSystem;
        private static SettingsWindow settingsWindow;

        private static XivCommonBase xivCommon;

        private interface ISearchResult
        {
            public string CatName { get; }
            public string Name { get; }
            public TextureWrap? Icon { get; }

            public void Selected();
        }

        private class ContentFinderConditionSearchResult : ISearchResult
        {
            public string CatName => "Duties";
            public string Name { get; set; }
            public TextureWrap? Icon { get; set; }
            public uint DataKey { get; set; }

            private static void OpenWikiPage(string input)
            {
                var name = input.Replace(' ', '_');
                Util.OpenLink($"https://ffxiv.gamerescape.com/wiki/{name}?useskin=Vector");
            }

            public void Selected()
            {
                var row = Data.GetExcelSheet<ContentFinderCondition>()!.GetRow(DataKey);
                OpenWikiPage(row!.Name.ToDalamudString().TextValue);
            }
        }

        private class AetheryteSearchResult : ISearchResult
        {
            public string CatName => "Aetherytes";
            public string Name { get; set; }
            public TextureWrap? Icon { get; set; }
            public AetheryteEntry Data { get; set; }

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

            public unsafe void Selected()
            {
                FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->GetUiModule()->ExecuteMainCommand(CommandId);
            }
        }

        private class InternalSearchResult : ISearchResult
        {
            public string CatName => "Wotsit";

            public string Name => GetNameForKind(this.Kind);

            public TextureWrap? Icon => Kind switch
            {
                _ => TexCache.PluginInstallerIcon,
            };

            public enum InternalSearchResultKind
            {
                Settings,
                DalamudPlugins,
                DalamudSettings,
            }

            public InternalSearchResultKind Kind { get; set; }

            public static string GetNameForKind(InternalSearchResultKind kind) => kind switch {
                InternalSearchResultKind.Settings => "Wotsit Settings",
                InternalSearchResultKind.DalamudPlugins => "Dalamud Plugin Installer",
                InternalSearchResultKind.DalamudSettings => "Dalamud Settings",
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
                }
            }
        }

        public class GeneralActionSearchResult : ISearchResult
        {
            public string CatName => "General Actions";
            public string Name { get; set;  }
            public TextureWrap? Icon { get; set; }

            public void Selected()
            {
                var message = $"/gaction \"{Name}\"";
                xivCommon.Functions.Chat.SendMessage(message);
            }
        }

        private ISearchResult[]? results;

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

        private void UpdateSearchResults()
        {
            if (searchTerm.IsNullOrEmpty())
            {
                results = null;
                return;
            }

            PluginLog.Information("Searching: " + searchTerm);
            var term = searchTerm.ToLower();

            var cResults = new List<ISearchResult>();

            if (Configuration.ToSearch.HasFlag(Configuration.SearchSetting.Duty))
            {
                foreach (var cfc in SearchDatabase.GetAll<ContentFinderCondition>())
                {
                    if (cfc.Value.Searchable.Contains(term))
                    {
                        cResults.Add(new ContentFinderConditionSearchResult()
                        {
                            Name = cfc.Value.Display,
                            DataKey = cfc.Key,
                            Icon = TexCache.WikiIcon,
                        });
                    }

                    if (cResults.Count > MAX_TO_SEARCH)
                        break;
                }
            }

            if (Configuration.ToSearch.HasFlag(Configuration.SearchSetting.Aetheryte))
            {
                if (!(Condition[ConditionFlag.BoundByDuty] || Condition[ConditionFlag.BoundByDuty56] ||
                      Condition[ConditionFlag.BoundByDuty95]) || Condition[ConditionFlag.Occupied] || Condition[ConditionFlag.OccupiedInCutSceneEvent])
                {
                    foreach (var aetheryte in Aetheryes)
                    {
                        var aetheryteName = AetheryteManager.GetAetheryteName(aetheryte);
                        var terriName = SearchDatabase.GetString<TerritoryType>(aetheryte.TerritoryId);
                        if (aetheryteName.ToLower().Contains(term) || terriName.Searchable.Contains(term))
                        {
                            cResults.Add(new AetheryteSearchResult
                            {
                                Name = aetheryteName,
                                Data = aetheryte,
                                Icon = TexCache.AetheryteIcon,
                            });
                        }

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
                    {
                        cResults.Add(new GeneralActionSearchResult
                        {
                            Name = generalAction.Value.Display,
                            Icon = TexCache.GeneralActionIcons[generalAction.Key]
                        });
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

            results = cResults.ToArray();
            PluginLog.Information($"{results.Length} results.");
        }

        public void Dispose()
        {
            PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
            PluginInterface.UiBuilder.Draw -= DrawUI;
            Framework.Update -= FrameworkOnUpdate;
            CommandManager.RemoveHandler(commandName);

            TexCache.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            settingsWindow.IsOpen = true;
        }

        private void OpenFinder()
        {
            finderOpen = true;
        }

        private void CloseFinder()
        {
            finderOpen = false;
            searchTerm = string.Empty;
            selectedIndex = 0;
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

            if (ImGui.InputTextWithHint("###findeverythinginput", "Type to search...", ref searchTerm, 1000,
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

                    if (ImGui.IsKeyDown((int) VirtualKey.RETURN))
                    {
                        results[selectedIndex].Selected();
                        closeFinder = true;
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