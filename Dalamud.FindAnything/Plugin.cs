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
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Gui;
using Dalamud.Game.Gui.Toast;
using Dalamud.Interface;
using Dalamud.Logging;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using ImGuiNET;
using ImGuiScene;
using Lumina.Excel.GeneratedSheets;
using TeleporterPlugin.Managers;

namespace SamplePlugin
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Find Anything";

        private const string commandName = "/pmycommand";

        public static DalamudPluginInterface PluginInterface { get; private set; }
        public static CommandManager CommandManager { get; private set; }
        public static Configuration Configuration { get; private set; }
        public static Framework Framework { get; private set; }
        public static DataManager Data { get; private set; }
        public static KeyState Keys { get; private set; }
        public static ClientState ClientState { get; private set; }
        public static ChatGui ChatGui { get; private set; }
        public static ToastGui ToastGui { get; private set; }
        public static Dalamud.Game.ClientState.Conditions.Condition Condition { get; private set; }

        private bool finderOpen = false;
        private string searchTerm = string.Empty;
        private int selectedIndex = 0;

        private IReadOnlyDictionary<uint, string> instanceContentSearchData;

        private int framesSinceLastKbChange = 0;

        private interface ISearchResult
        {
            public string Name { get; set; }
            public TextureWrap? Icon { get; set; }

            public void Selected();
        }

        private static void OpenWikiPage(string input)
        {
            var name = input.Replace(' ', '_');
            Util.OpenLink($"https://ffxiv.gamerescape.com/wiki/{name}?useskin=Vector");
        }

        private class ContentFinderConditionSearchResult : ISearchResult
        {
            public string Name { get; set; }
            public TextureWrap? Icon { get; set; }
            public uint DataKey { get; set; }

            public void Selected()
            {
                var row = Data.GetExcelSheet<ContentFinderCondition>()!.GetRow(DataKey);
                OpenWikiPage(row!.Name.ToDalamudString().TextValue);
            }
        }

        private class AetheryteSearchResult : ISearchResult
        {
            public string Name { get; set; }
            public TextureWrap? Icon { get; set; }
            public TeleportInfo Data { get; set; }

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
                    var error = "To use Aetherytes within Find Anything, you must install the \"Teleporter\" plugin.";
                    ChatGui.PrintError(error);
                    ToastGui.ShowError(error);
                }
                else
                {
                    ChatGui.Print($"Teleporting to {Name}...");
                }
            }
        }

        private ISearchResult[]? results;

        public static ICallGateSubscriber<uint, byte, bool> TeleportIpc { get; private set; }

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager,
            Framework framework,
            DataManager data,
            KeyState keys,
            ClientState state,
            Dalamud.Game.ClientState.Conditions.Condition cond,
            ChatGui chatGui,
            ToastGui toastGui)
        {
            PluginInterface = pluginInterface;
            CommandManager = commandManager;
            Framework = framework;
            Data = data;
            Keys = keys;
            ClientState = state;
            Condition = cond;
            ChatGui = chatGui;
            ToastGui = toastGui;

            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);

            CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "A useful message to display in /xlhelp"
            });

            PluginInterface.UiBuilder.Draw += DrawUI;
            Framework.Update += FrameworkOnUpdate;

            PluginInterface.UiBuilder.DisableCutsceneUiHide = true;
            PluginInterface.UiBuilder.DisableUserUiHide = true;

            TeleportIpc = PluginInterface.GetIpcSubscriber<uint, byte, bool>("Teleport");

            SetupData();
        }

        private void FrameworkOnUpdate(Framework framework)
        {
            if (Keys[VirtualKey.CONTROL] && Keys[VirtualKey.T])
            {
                OpenFinder();
            }
            else if (Keys[VirtualKey.ESCAPE])
            {
                CloseFinder();
            }
        }

        private void SetupData()
        {
            var ic = Data.GetExcelSheet<ContentFinderCondition>();
            instanceContentSearchData = ic.ToDictionary(x => x.RowId, x => x.Name.ToDalamudString().TextValue.ToLower());
            PluginLog.Information($"{instanceContentSearchData.Count} CFC");

            AetheryteManager.Load();
        }

        private void UpdateSearchResults()
        {
            if (searchTerm.IsNullOrEmpty())
            {
                results = null;
                return;
            }

            PluginLog.Information("Searching: " + searchTerm);

            var cResults = new List<ISearchResult>();

            foreach (var cfc in instanceContentSearchData)
            {
                if (cfc.Value.Contains(searchTerm.ToLower()))
                {
                    cResults.Add(new ContentFinderConditionSearchResult()
                    {
                        Name = cfc.Value,
                        DataKey = cfc.Key
                    });
                }

                if (cResults.Count > MAX_TO_SEARCH)
                    break;
            }

            if (!(Condition[ConditionFlag.BoundByDuty] || Condition[ConditionFlag.BoundByDuty56] ||
                  Condition[ConditionFlag.BoundByDuty95]))
            {
                foreach (var aetheryte in AetheryteManager.AvailableAetherytes)
                {
                    var aetheryteName = AetheryteManager.AetheryteNames[aetheryte.AetheryteId];
                    var terriName = Data.GetExcelSheet<TerritoryType>()!.GetRow(aetheryte.TerritoryId)!.PlaceName.Value!.Name!.ToDalamudString().TextValue;
                    if (aetheryteName.ToLower().Contains(searchTerm.ToLower()) || terriName.ToLower().Contains(searchTerm.ToLower()))
                    {
                        cResults.Add(new AetheryteSearchResult
                        {
                            Name = aetheryteName,
                            Data = aetheryte
                        });
                    }

                    if (cResults.Count > MAX_TO_SEARCH)
                        break;
                }
            }

            results = cResults.ToArray();
            PluginLog.Information($"{results.Length} results.");
        }

        public void Dispose()
        {
            PluginInterface.UiBuilder.Draw -= DrawUI;
            Framework.Update -= FrameworkOnUpdate;
            CommandManager.RemoveHandler(commandName);
        }

        private void OnCommand(string command, string args)
        {
            // in response to the slash command, just display our main ui
            //this.PluginUi.Visible = true;
        }

        private void OpenFinder()
        {
            AetheryteManager.UpdateAvailableAetherytes();
            finderOpen = true;
            PluginLog.Information("FindAnything opened!");
        }

        private void CloseFinder()
        {
            finderOpen = false;
            searchTerm = string.Empty;
            selectedIndex = 0;
            UpdateSearchResults();
            PluginLog.Information("FindAnything closed!");
        }

        private const int MAX_ONE_PAGE = 10;
        private const int MAX_TO_SEARCH = 100;

        private void DrawUI()
        {
            if (!finderOpen)
                return;

            ImGuiHelpers.ForceNextWindowMainViewport();

            var size = new Vector2(500, 40);
            var mainViewportSize = ImGuiHelpers.MainViewport.Size;
            var mainViewportMiddle = mainViewportSize / 2;
            var startPos = ImGuiHelpers.MainViewport.Pos + (mainViewportMiddle - (size / 2));

            startPos.Y -= 200;

            if (results != null)
                size.Y += Math.Min(results.Length, MAX_ONE_PAGE) * 20;

            ImGui.SetNextWindowPos(startPos);
            ImGui.SetNextWindowSize(size);
            ImGui.SetNextWindowSizeConstraints(size, new Vector2(size.X, size.Y + 400));

            ImGui.Begin("###findeverything", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

            ImGui.PushItemWidth(size.X - 45);

            if (ImGui.InputTextWithHint("###findeverythinginput", "Type to search...", ref searchTerm, 1000,
                    ImGuiInputTextFlags.NoUndoRedo))
            {
                UpdateSearchResults();
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
                CloseFinder();
            }

            var textSize = ImGui.CalcTextSize("poop");

            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 4));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, new Vector2(4, 4));

            if (results != null)
            {
                if (ImGui.BeginChild("###findAnythingScroller"))
                {
                    var childSize = ImGui.GetWindowSize();

                    if (ImGui.IsKeyDown((int) VirtualKey.DOWN) && selectedIndex != results.Length - 1 && framesSinceLastKbChange > 10)
                    {
                        selectedIndex++;
                        framesSinceLastKbChange = 0;
                    }
                    else if ((ImGui.IsKeyDown((int) VirtualKey.UP) && selectedIndex != 0) && framesSinceLastKbChange > 10)
                    {
                        selectedIndex--;
                        framesSinceLastKbChange = 0;
                    } 
                    else if (ImGui.IsKeyDown((int) VirtualKey.PRIOR) && framesSinceLastKbChange > 10)
                    {
                        selectedIndex = Math.Max(0, selectedIndex - MAX_ONE_PAGE);
                        framesSinceLastKbChange = 0;
                    }
                    else if (ImGui.IsKeyDown((int) VirtualKey.NEXT) && framesSinceLastKbChange > 10)
                    {
                        selectedIndex = Math.Min(results.Length, selectedIndex + MAX_ONE_PAGE);
                        framesSinceLastKbChange = 0;
                    }

                    framesSinceLastKbChange++;

                    for (var i = 0; i < results.Length; i++)
                    {
                        var result = results[i];
                        ImGui.Selectable($"{result.Name} ({result.GetType().Name})", i == selectedIndex, ImGuiSelectableFlags.None, new Vector2(childSize.X, textSize.Y));
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
                        CloseFinder();
                    }
                }

                ImGui.EndChild();
            }

            ImGui.PopStyleVar(2);

            ImGui.End();
        }
    }
}
