using System;
using System.Collections.Generic;

namespace Dalamud.FindAnything.Lookup;

public sealed class RootLookup : ILookup
{
    private ModuleLookup ModuleLookup { get; } = new();
    private WikiLookup WikiLookup { get; } = new();
    private WikiSiteLookup WikiSiteLookup { get; } = new();
    private EmoteModeLookup EmoteModeLookup { get; } = new();

    private LookupSetting baseSetting;
    private LookupSetting? overrideSetting;

    public RootLookup() {
        baseSetting = new LookupSetting(LookupType.Module, ModuleLookup);
    }

    public ILookup GetLookupForType(LookupType lookupType) {
        return lookupType switch {
            LookupType.Module => ModuleLookup,
            LookupType.Wiki => WikiLookup,
            LookupType.WikiSite => WikiSiteLookup,
            LookupType.EmoteMode => EmoteModeLookup,
            _ => throw new ArgumentOutOfRangeException($"Unknown lookup ty[e: {lookupType}"),
        };
    }

    public void SetBase(LookupType lookupType) {
        baseSetting = new LookupSetting(lookupType, GetLookupForType(lookupType));
    }

    public void SetOverride(LookupType lookupType) {
        overrideSetting = new LookupSetting(lookupType, GetLookupForType(lookupType));
    }

    public void ClearOverride() {
        overrideSetting = null;
    }

    public LookupType GetBase() => baseSetting.Type;

    private ILookup GetActiveLookup() {
        return overrideSetting != null ? overrideSetting.Lookup : baseSetting.Lookup;
    }

    public string GetPlaceholder() {
        return GetActiveLookup().GetPlaceholder();
    }

    public void OnOpen() {
        GetActiveLookup().OnOpen();
    }

    public LookupResult Lookup(SearchCriteria criteria) {
#if DEBUG
        var sw = System.Diagnostics.Stopwatch.StartNew();
#endif
        var result = GetActiveLookup().Lookup(criteria);
#if DEBUG
        sw.Stop();
        Service.Log.Debug($"Took: {sw.ElapsedMilliseconds}ms (lookup: {GetActiveLookup().GetType().Name})");
#endif
        return result;
    }

    public void OnSelected(SearchCriteria criteria, ISearchResult result) {
        GetActiveLookup().OnSelected(criteria, result);
    }
}

public enum LookupType
{
    Module,
    Wiki,
    WikiSite,
    EmoteMode,
}

public record LookupSetting(LookupType Type, ILookup Lookup);
