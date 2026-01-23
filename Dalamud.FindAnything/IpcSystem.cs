using Dalamud.Plugin.Ipc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dalamud.FindAnything;

public sealed class IpcSystem : IDisposable {
    private readonly Normalizer normalizer;
    private readonly ICallGateProvider<string, string, uint, string> cgRegister;
    private readonly ICallGateProvider<string, string, string, uint, string> cgRegisterWithSearch;
    private readonly ICallGateProvider<string, string, bool> cgUnregisterOne;
    private readonly ICallGateProvider<string, bool> cgUnregisterAll;
    private readonly ICallGateProvider<string, bool> cgInvoke;
    private readonly ICallGateProvider<bool> cgIsAvailable;
    private bool isReady;

    public Dictionary<string, List<IpcBinding>> TrackedIpcs;

    public class IpcBinding {
        public required string Display { get; init; }
        public required string Search { get; init; }
        public required uint IconId { get; init; }
        public required string Guid { get; init; }
    }

    public IpcSystem(Normalizer normalizer) {
        this.normalizer = normalizer.WithKana(true);

        cgRegister = Service.PluginInterface.GetIpcProvider<string, string, uint, string>("FA.Register");
        cgRegisterWithSearch = Service.PluginInterface.GetIpcProvider<string, string, string, uint, string>("FA.RegisterWithSearch");
        cgUnregisterOne = Service.PluginInterface.GetIpcProvider<string, string, bool>("FA.UnregisterOne");
        cgUnregisterAll = Service.PluginInterface.GetIpcProvider<string, bool>("FA.UnregisterAll");
        cgInvoke = Service.PluginInterface.GetIpcProvider<string, bool>("FA.Invoke");
        cgIsAvailable = Service.PluginInterface.GetIpcProvider<bool>("FA.IsAvailable");

        cgRegister.RegisterFunc(Register);
        cgRegisterWithSearch.RegisterFunc(Register);
        cgUnregisterOne.RegisterFunc(UnregisterOne);
        cgUnregisterAll.RegisterFunc(UnregisterAll);
        cgIsAvailable.RegisterFunc(IsAvailable);

        TrackedIpcs = new Dictionary<string, List<IpcBinding>>();

        Service.Log.Verbose("[IPC] Firing FA.Available.");
        var cgAvailable = Service.PluginInterface.GetIpcProvider<bool>("FA.Available");
        cgAvailable.SendMessage();

        isReady = true;
    }

    private bool IsAvailable() {
        return isReady;
    }

    public void Invoke(string guid) {
        cgInvoke.SendMessage(guid);
    }

    private bool UnregisterOne(string pluginInternalName, string guid) {
        if (TrackedIpcs.TryGetValue(pluginInternalName, out var ipcsList)) {
            var toDelete = ipcsList.FirstOrDefault(x => x.Guid == guid);
            if (toDelete == null)
                return false;

            ipcsList.Remove(toDelete);
        }

        return false;
    }

    private bool UnregisterAll(string pluginInternalName) {
        if (TrackedIpcs.Remove(pluginInternalName)) {
            Service.Log.Verbose($"[IPC] All IPCs unregistered: {pluginInternalName}");
            return true;
        }

        return false;
    }

    private string Register(string pluginInternalName, string searchDisplayName, uint iconId) {
        return Register(pluginInternalName, searchDisplayName, searchDisplayName, iconId);
    }

    private string Register(string pluginInternalName, string searchDisplayName, string searchValue, uint iconId) {
        if (!TrackedIpcs.TryGetValue(pluginInternalName, out var ipcList)) {
            ipcList = [];
            TrackedIpcs.Add(pluginInternalName, ipcList);
        }

        var guid = Guid.NewGuid().ToString();

        ipcList.Add(new IpcBinding {
            Display = searchDisplayName,
            Guid = guid,
            IconId = iconId,
            Search = normalizer.Searchable(searchValue),
        });

        Service.Log.Verbose($"[IPC] Registered: {pluginInternalName} - {searchDisplayName} - {guid}");

        return guid;
    }

    public void Dispose() {
        isReady = false;
        cgRegister.UnregisterFunc();
        cgRegisterWithSearch.UnregisterFunc();
        cgUnregisterOne.UnregisterFunc();
        cgUnregisterAll.UnregisterFunc();
        cgIsAvailable.UnregisterFunc();

        foreach (var trackedIpc in TrackedIpcs) {
            UnregisterAll(trackedIpc.Key);
        }
    }
}
