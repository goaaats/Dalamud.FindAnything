using System;
using System.Collections.Generic;

namespace Dalamud.FindAnything.Lookup;

public sealed class RootLookup : ILookup
{
    private ModuleLookup ModuleLookup { get; } = new();
    private WikiLookup WikiLookup { get; } = new();
    private WikiSiteLookup WikiSiteLookup { get; } = new();
    private EmoteModeLookup EmoteModeLookup { get; } = new();

    private ILookup activeLookup;
    private LookupType activeType;

    public RootLookup() {
        activeLookup = ModuleLookup;
        activeType = LookupType.Module;
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

    public void SetType(LookupType lookupType) {
        activeLookup = GetLookupForType(lookupType);
        activeType = lookupType;
    }

    public LookupType CurrentType() => activeType;

    private ILookup GetActiveLookup() {
        return FindAnythingPlugin.Finder.SearchState.OverrideLookupType == LookupType.Wiki ? WikiLookup : activeLookup;
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
        Service.Log.Debug($"Took: {sw.ElapsedMilliseconds}ms (lookup: {activeLookup.GetType().Name})");
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