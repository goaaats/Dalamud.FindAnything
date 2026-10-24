using Dalamud.FindAnything.Game;
using Dalamud.FindAnything.Lookup;
using Dalamud.FindAnything.Settings;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Dalamud.FindAnything;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class FindAnythingPlugin : IAsyncDalamudPlugin {
    private const string CommandName = "/wotsit";

    public static FindAnythingPlugin Instance { get; private set; } = null!;
    public static Configuration Configuration => ConfigManager.Config;
    public static ConfigManager ConfigManager { get; private set; } = null!;

    public static TextureCache TexCache { get; private set; } = null!;
    public static SearchDatabase SearchDatabase { get; private set; } = null!;
    public static GameStateCache GameStateCache { get; private set; } = null!;
    public static IpcSystem Ipc { get; private set; } = null!;
    public static AetheryteManager AetheryteManager { get; set; } = null!;
    public static Normalizer Normalizer { get; private set; } = null!;
    public static RootLookup RootLookup { get; private set; } = null!;

    private Finder Finder { get; set; } = null!;
    private FinderActivator FinderActivator { get; set; } = null!;
    private WindowSystem WindowSystem { get; set; } = null!;
    private SettingsWindow SettingsWindow { get; set; } = null!;
    private GameWindow GameWindow { get; set; } = null!;

    public FindAnythingPlugin(IDalamudPluginInterface pluginInterface) {
        pluginInterface.Create<Service>();
        Instance = this;
    }

    public async Task LoadAsync(CancellationToken cancellationToken) {
        Normalizer = new Normalizer();
        ConfigManager = new ConfigManager();

        TexCache = TextureCache.Load(Service.Data, Service.TextureProvider);
        SearchDatabase = new SearchDatabase(Normalizer);
        GameStateCache = new GameStateCache();
        Ipc = new IpcSystem(Normalizer);

        AetheryteManager = new AetheryteManager();

        RootLookup = new RootLookup();

        // Finder

        Finder = new Finder(RootLookup, Normalizer);
        FinderActivator = new FinderActivator(Finder);

        // UI

        Service.PluginInterface.UiBuilder.DisableCutsceneUiHide = true;
        Service.PluginInterface.UiBuilder.DisableUserUiHide = true;
        Service.PluginInterface.UiBuilder.OpenConfigUi += OpenSettings;

        WindowSystem = new WindowSystem("wotsit");

        SettingsWindow = new SettingsWindow { IsOpen = false };
        WindowSystem.AddWindow(SettingsWindow);

        GameWindow = new GameWindow { IsOpen = false };
        WindowSystem.AddWindow(GameWindow);

        WindowSystem.AddWindow(Finder);

        Service.PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        // Commands

        Service.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) {
            HelpMessage = "Open the Wotsit settings.",
        });

        Service.CommandManager.AddHandler("/bountifuldn", new CommandInfo((_, _) => GameWindow.Cheat()) {
            HelpMessage = "Open the Wotsit settings.",
            ShowInHelp = false,
        });

        // Init

        SearchDatabase.InitBase();

        await Service.Framework.Run(() => {
            SearchDatabase.InitMainThread();
        }, cancellationToken);

        // Notify availability after load

        _ = Service.Framework.RunOnTick(() => {
            Ipc.NotifyAvailable();
        }, delayTicks: 0, cancellationToken: cancellationToken);
    }

    public ValueTask DisposeAsync() {
        Service.PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        Service.CommandManager.RemoveHandler(CommandName);
        Service.CommandManager.RemoveHandler("/bountifuldn");

        FinderActivator.Dispose();

        GameStateCache.Dispose();
        Ipc.Dispose();

        return ValueTask.CompletedTask;
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
