using Dalamud.FindAnything.Game;
using Dalamud.FindAnything.Lookup;
using Dalamud.FindAnything.Settings;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using System.Diagnostics.CodeAnalysis;

namespace Dalamud.FindAnything;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class FindAnythingPlugin : IDalamudPlugin
{
    private const string CommandName = "/wotsit";

    public static FindAnythingPlugin Instance { get; private set; } = null!;
    public static Configuration Configuration => ConfigManager.Config;
    public static ConfigManager ConfigManager { get; private set; } = null!;

    public static TextureCache TexCache { get; private set; } = null!;
    public static SearchDatabase SearchDatabase { get; private set; } = null!;
    public static GameStateCache GameStateCache { get; private set; } = null!;
    public static IpcSystem Ipc { get; private set; } = null!;
    public static Normalizer Normalizer { get; private set; } = null!;
    public static RootLookup RootLookup { get; private set; } = null!;

    private Finder Finder { get; }
    private FinderActivator FinderActivator { get; }
    private WindowSystem WindowSystem { get; }
    private SettingsWindow SettingsWindow { get; }
    private GameWindow GameWindow { get; }

    public FindAnythingPlugin(IDalamudPluginInterface pluginInterface) {
        pluginInterface.Create<Service>();
        Instance = this;

        ConfigManager = new ConfigManager();
        Normalizer = new Normalizer();

        TexCache = TextureCache.Load(Service.Data, Service.TextureProvider);
        SearchDatabase = SearchDatabase.Load(Normalizer);
        GameStateCache = GameStateCache.Load();
        Ipc = new IpcSystem(Normalizer);

        RootLookup = new RootLookup();

        // Finder

        Finder = new Finder(RootLookup, Normalizer);
        FinderActivator = new FinderActivator(Finder);

        // UI

        pluginInterface.UiBuilder.DisableCutsceneUiHide = true;
        pluginInterface.UiBuilder.DisableUserUiHide = true;
        pluginInterface.UiBuilder.OpenConfigUi += OpenSettings;

        WindowSystem = new WindowSystem("wotsit");

        SettingsWindow = new SettingsWindow { IsOpen = false };
        WindowSystem.AddWindow(SettingsWindow);

        GameWindow = new GameWindow { IsOpen = false };
        WindowSystem.AddWindow(GameWindow);

        pluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        // Commands

        Service.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) {
            HelpMessage = "Open the Wotsit settings.",
        });

        Service.CommandManager.AddHandler("/bountifuldn", new CommandInfo((_, _) => GameWindow.Cheat()) {
            HelpMessage = "Open the Wotsit settings.",
            ShowInHelp = false,
        });
    }

    public void Dispose() {
        Service.PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        Service.CommandManager.RemoveHandler(CommandName);
        Service.CommandManager.RemoveHandler("/bountifuldn");

        Ipc.Dispose();
        FinderActivator.Dispose();
        Finder.Dispose();
    }

    private void OnCommand(string command, string args) {
        SettingsWindow.IsOpen = true;

        if (args.Contains("reset")) {
            HintProvider.ResetHints();
        }
    }

    public void OpenSettings() {
        SettingsWindow.IsOpen = true;
    }

    public void OpenGame() {
        GameWindow.IsOpen = true;
    }

    public void SwitchLookupType(LookupType type) {
        Finder.SwitchLookupType(type);
    }

    public void UserError(string error) {
        Service.ChatGui.PrintError(error);
        Service.ToastGui.ShowError(error);
    }
}