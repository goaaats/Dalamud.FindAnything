using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Dalamud.FindAnything;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
public class Service
{
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; set; }
    [PluginService] public static ICommandManager CommandManager { get; set; }
    [PluginService] public static IFramework Framework { get; set; }
    [PluginService] public static IDataManager Data { get; set; }
    [PluginService] public static IKeyState Keys { get; set; }
    [PluginService] public static IClientState ClientState { get; set; }
    [PluginService] public static IChatGui ChatGui { get; set; }
    [PluginService] public static IToastGui ToastGui { get; set; }
    [PluginService] public static ICondition Condition { get; set; }
    [PluginService] public static IAetheryteList Aetherytes { get; set; }
    [PluginService] public static ITextureProvider TextureProvider { get; set; }
    [PluginService] public static IPluginLog Log { get; set; }
    [PluginService] public static INotificationManager Notifications { get; set; }
    [PluginService] public static ISeStringEvaluator SeStringEvaluator { get; set; }
    [PluginService] public static IPlayerState PlayerState { get; set; }
}