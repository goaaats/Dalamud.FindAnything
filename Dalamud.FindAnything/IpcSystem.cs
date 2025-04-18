﻿using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dalamud.FindAnything;

public class IpcSystem : IDisposable
{
    private readonly IDataManager data;
    private readonly TextureCache texCache;
    private readonly ICallGateProvider<string, string, uint, string> cgRegister;
    private readonly ICallGateProvider<string, string, string, uint, string> cgRegisterWithSearch;
    private readonly ICallGateProvider<string, string, bool> cgUnregisterOne;
    private readonly ICallGateProvider<string, bool> cgUnregisterAll;
    private readonly ICallGateProvider<string, bool> cgInvoke;
    private readonly ICallGateProvider<bool> cgIsAvailable;
    private bool isReady;

    public Dictionary<string, List<IpcBinding>> TrackedIpcs;
    
    public class IpcBinding
    {
        public string Display { get; set; }
        public string Search { get; set; }
        public uint IconId { get; set; }
        public string Guid { get; set; }
    }

    public IpcSystem(IDalamudPluginInterface pluginInterface, IDataManager data, TextureCache texCache)
    {
        this.data = data;
        this.texCache = texCache;
        this.cgRegister = pluginInterface.GetIpcProvider<string, string, uint, string>("FA.Register");
        this.cgRegisterWithSearch = pluginInterface.GetIpcProvider<string, string, string, uint, string>("FA.RegisterWithSearch");
        this.cgUnregisterOne = pluginInterface.GetIpcProvider<string, string, bool>("FA.UnregisterOne");
        this.cgUnregisterAll = pluginInterface.GetIpcProvider<string, bool>("FA.UnregisterAll");
        this.cgInvoke = pluginInterface.GetIpcProvider<string, bool>("FA.Invoke");
        this.cgIsAvailable = pluginInterface.GetIpcProvider<bool>("FA.IsAvailable");
        
        this.cgRegister.RegisterFunc(Register);
        this.cgRegisterWithSearch.RegisterFunc(Register);
        this.cgUnregisterOne.RegisterFunc(UnregisterOne);
        this.cgUnregisterAll.RegisterFunc(UnregisterAll);
        this.cgIsAvailable.RegisterFunc(IsAvailable);

        this.TrackedIpcs = new Dictionary<string, List<IpcBinding>>();

        FindAnythingPlugin.Log.Verbose("[IPC] Firing FA.Available.");
        var cgAvailable = pluginInterface.GetIpcProvider<bool>("FA.Available");
        cgAvailable.SendMessage();
        isReady = true;
    }

    private bool IsAvailable()
    {
        return isReady;
    }


    public void Invoke(string guid)
    {
        this.cgInvoke.SendMessage(guid);
    }
    
    private bool UnregisterOne(string pluginInternalName, string guid)
    {
        if (this.TrackedIpcs.TryGetValue(pluginInternalName, out var ipcsList))
        {
            var toDelete = ipcsList.FirstOrDefault(x => x.Guid == guid);
            if (toDelete == null)
                return false;

            ipcsList.Remove(toDelete);
        }

        return false;
    }

    private bool UnregisterAll(string pluginInternalName)
    {
        if (this.TrackedIpcs.ContainsKey(pluginInternalName))
        {
            this.TrackedIpcs.Remove(pluginInternalName);
            
            FindAnythingPlugin.Log.Verbose($"[IPC] All IPCs unregistered: {pluginInternalName}");
            return true;
        }

        return false;
    }
    
    private string Register(string pluginInternalName, string searchDisplayName, uint iconId)
    {
        return Register(pluginInternalName, searchDisplayName, searchDisplayName, iconId);
    }

    private string Register(string pluginInternalName, string searchDisplayName, string searchValue, uint iconId)
    {
        if (!this.TrackedIpcs.TryGetValue(pluginInternalName, out var ipcList))
        {
            ipcList = new List<IpcBinding>();
            this.TrackedIpcs.Add(pluginInternalName, ipcList);
        }

        var guid = Guid.NewGuid().ToString();

        ipcList.Add(new IpcBinding
        {
            Display = searchDisplayName,
            Guid = guid,
            IconId = iconId,
            Search = searchValue.Downcase(normalizeKana: true)
        });
        
        FindAnythingPlugin.Log.Verbose($"[IPC] Registered: {pluginInternalName} - {searchDisplayName} - {guid}");
        
        return guid;
    }

    public void Dispose()
    {
        isReady = false;
        this.cgRegister.UnregisterFunc();
        this.cgRegisterWithSearch.UnregisterFunc();
        this.cgUnregisterOne.UnregisterFunc();
        this.cgUnregisterAll.UnregisterFunc();
        this.cgIsAvailable.UnregisterFunc();

        foreach (var trackedIpc in this.TrackedIpcs)
        {
            UnregisterAll(trackedIpc.Key);
        }
    }
}

