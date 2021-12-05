using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Dalamud.Data;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Logging;
using Dalamud.Utility;
using ImGuiNET;
using ImGuiScene;
using Lumina.Excel.GeneratedSheets;

namespace SamplePlugin
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Find Anything";

        private const string commandName = "/pmycommand";

        private DalamudPluginInterface PluginInterface { get; init; }
        private CommandManager CommandManager { get; init; }
        private Configuration Configuration { get; init; }
        private Framework Framework { get; init; }
        private DataManager Data { get; init; }
        private KeyState Keys { get; init; }

        private bool finderOpen = false;
        private string searchTerm = string.Empty;
        private int selectedIndex = 0;

        private IReadOnlyDictionary<uint, string> instanceContentSearchData;

        private int framesSinceLastKbChange = 0;

        private class SearchResult
        {
            public enum ResultKind
            {
                ContentFinderCondition,
            }

            public string Name { get; set; }
            public TextureWrap? Icon { get; set; }
            public uint DataKey { get; set; }
            public ResultKind Kind { get; set; }
        }

        private SearchResult[]? results;

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager,
            Framework framework,
            DataManager data,
            KeyState keys)
        {
            this.PluginInterface = pluginInterface;
            this.CommandManager = commandManager;
            this.Framework = framework;
            this.Data = data;
            this.Keys = keys;

            this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.PluginInterface);

            this.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "A useful message to display in /xlhelp"
            });

            this.PluginInterface.UiBuilder.Draw += DrawUI;
            this.Framework.Update += FrameworkOnUpdate;

            this.PluginInterface.UiBuilder.DisableCutsceneUiHide = true;
            this.PluginInterface.UiBuilder.DisableUserUiHide = true;

            SetupData();
        }

        private void FrameworkOnUpdate(Framework framework)
        {
            if (Keys[VirtualKey.CONTROL] && Keys[VirtualKey.T])
            {
                finderOpen = true;
                PluginLog.Information("FindAnything opened!");
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
        }

        private void UpdateSearchResults()
        {
            if (searchTerm.IsNullOrEmpty())
            {
                results = null;
                return;
            }

            PluginLog.Information("Searching: " + searchTerm);

            var cResults = new List<SearchResult>();

            foreach (var cfc in instanceContentSearchData)
            {
                if (cfc.Value.Contains(searchTerm.ToLower()))
                {
                    cResults.Add(new SearchResult
                    {
                        Name = cfc.Value,
                        DataKey = cfc.Key,
                        Kind = SearchResult.ResultKind.ContentFinderCondition
                    });
                }

                if (cResults.Count > MAX_TO_SEARCH)
                    break;
            }

            results = cResults.ToArray();
            PluginLog.Information($"{results.Length} results.");
        }

        public void Dispose()
        {
            this.PluginInterface.UiBuilder.Draw -= DrawUI;
            this.Framework.Update -= FrameworkOnUpdate;
            this.CommandManager.RemoveHandler(commandName);
        }

        private void OnCommand(string command, string args)
        {
            // in response to the slash command, just display our main ui
            //this.PluginUi.Visible = true;
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

            if (results != null)
            {
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

                framesSinceLastKbChange++;

                for (var i = 0; i < results.Length; i++)
                {
                    var result = results[i];
                    ImGui.Selectable(result.Name, i == selectedIndex);
                }
            }

            ImGui.End();
        }
    }
}
