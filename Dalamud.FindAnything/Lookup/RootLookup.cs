using System;

namespace Dalamud.FindAnything.Lookup;

public sealed class RootLookup : ILookup {
    private ModuleLookup ModuleLookup { get; } = new();
    private WikiLookup WikiLookup { get; } = new();
    private WikiSiteLookup WikiSiteLookup { get; } = new();
    private EmoteModeLookup EmoteModeLookup { get; } = new();
    private FacewearLookup FacewearLookup { get; } = new();
    private CoordinateActionLookup CoordinateActionLookup { get; } = new();

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
            LookupType.Facewear => FacewearLookup,
            LookupType.CoordinateAction => CoordinateActionLookup,
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

    public LookupType GetBaseType() => baseSetting.Type;

    public LookupType GetActiveType() => overrideSetting?.Type ?? baseSetting.Type;

    private ILookup GetActiveLookup() => overrideSetting?.Lookup ?? baseSetting.Lookup;

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

public enum LookupType {
    Module,
    Wiki,
    WikiSite,
    EmoteMode,
    Facewear,
    CoordinateAction,
}

public record LookupSetting(LookupType Type, ILookup Lookup);
