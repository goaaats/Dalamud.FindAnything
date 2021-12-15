using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Data;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using ImGuiScene;

namespace Dalamud.FindAnything;

public class IpcSystem : IDisposable
{
    private readonly DataManager data;
    private readonly TextureCache texCache;
    private readonly ICallGateProvider<string, string, uint, string> cgRegister;
    private readonly ICallGateProvider<string, bool> cgUnregisterAll;
    private readonly ICallGateProvider<string, bool> cgInvoke;

    public Dictionary<string, List<IpcBinding>> TrackedIpcs;
    
    public class IpcBinding
    {
        public string Display { get; set; }
        public string Search { get; set; }
        public uint IconId { get; set; }
        public string Guid { get; set; }
    }

    public IpcSystem(DalamudPluginInterface pluginInterface, DataManager data, TextureCache texCache)
    {
        this.data = data;
        this.texCache = texCache;
        this.cgRegister = pluginInterface.GetIpcProvider<string, string, uint, string>("FA.Register");
        this.cgUnregisterAll = pluginInterface.GetIpcProvider<string, bool>("FA.UnregisterAll");
        this.cgInvoke = pluginInterface.GetIpcProvider<string, bool>("FA.Invoke");
        
        this.cgRegister.RegisterFunc(Register);
        this.cgUnregisterAll.RegisterFunc(Unregister);

        this.TrackedIpcs = new Dictionary<string, List<IpcBinding>>();
    }

    public void Invoke(string guid)
    {
        this.cgInvoke.SendMessage(guid);
    }

    private bool Unregister(string pluginInternalName)
    {
        if (this.TrackedIpcs.ContainsKey(pluginInternalName))
        {
            this.TrackedIpcs.Remove(pluginInternalName);
            
            PluginLog.Verbose($"[IPC] All IPCs unregistered: {pluginInternalName}");
            return true;
        }

        return false;
    }

    private string Register(string pluginInternalName, string searchDisplayName, uint iconId)
    {
        if (!this.TrackedIpcs.TryGetValue(pluginInternalName, out var ipcList))
        {
            ipcList = new List<IpcBinding>();
            this.TrackedIpcs.Add(pluginInternalName, ipcList);
        }

        var guid = Guid.NewGuid().ToString();
        
        this.texCache.EnsureExtraIcon(iconId);

        ipcList.Add(new IpcBinding
        {
            Display = searchDisplayName,
            Guid = guid,
            IconId = iconId,
            Search = searchDisplayName.ToLower(),
        });
        
        PluginLog.Verbose($"[IPC] Registered: {pluginInternalName} - {searchDisplayName} - {guid}");
        
        return guid;
    }

    public void Dispose()
    {
        this.cgRegister.UnregisterFunc();
        this.cgUnregisterAll.UnregisterFunc();

        foreach (var trackedIpc in this.TrackedIpcs)
        {
            Unregister(trackedIpc.Key);
        }
    }
}

