using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using InstanceContent = Lumina.Excel.Sheets.InstanceContent;

namespace Dalamud.FindAnything;

public sealed unsafe class GameStateCache : IDisposable {
    public record Gearset(int Index, string Name, uint ClassJob);

    private bool checkUnlocks = true;

    public IReadOnlyList<uint> UnlockedDutyKeys { get; private set; } = [];
    public IReadOnlyList<uint> UnlockedEmoteKeys { get; private set; } = [];
    public IReadOnlyList<uint> UnlockedMountKeys { get; private set; } = [];
    public IReadOnlyList<uint> UnlockedMinionKeys { get; private set; } = [];
    public IReadOnlyList<uint> UnlockedCollectionKeys { get; private set; } = [];
    public IReadOnlyList<uint> UnlockedFashionAccessoryKeys { get; private set; } = [];
    public IReadOnlyList<uint> UnlockedFacewearKeys { get; private set; } = [];
    public IReadOnlyList<uint> UnlockedFacewearStyleKeys { get; private set; } = [];
    public IReadOnlyList<Gearset> Gearsets { get; private set; } = [];

    public GameStateCache() {
        Service.UnlockState.Unlock += OnUnlock;
    }

    public void Dispose() {
        Service.UnlockState.Unlock -= OnUnlock;
    }

    private void OnUnlock(RowRef rowRef) {
        checkUnlocks = true;
    }

    public void Refresh() {
        if (checkUnlocks) {
            Service.Log.Debug("Refreshing unlocks");
            RefreshUnlocks();
            checkUnlocks = false;
        }
        RefreshGearsets();
    }

    private void RefreshUnlocks() {
        UnlockedDutyKeys = Service.Data.GetExcelSheet<ContentFinderCondition>()
            .Where(x =>
                x.Content.GetValueOrDefault<InstanceContent>() is { } instanceContent
                && Service.UnlockState.IsInstanceContentUnlocked(instanceContent))
            .Select(x => x.RowId)
            .ToList();

        UnlockedEmoteKeys = Service.Data.GetExcelSheet<Emote>()
            .Where(e => Service.UnlockState.IsEmoteUnlocked(e))
            .Select(emote => emote.RowId)
            .ToList();

        UnlockedMountKeys = Service.Data.GetExcelSheet<Mount>()
            .Where(x => Service.UnlockState.IsMountUnlocked(x))
            .Select(x => x.RowId)
            .ToList();

        UnlockedMinionKeys = Service.Data.GetExcelSheet<Companion>()
            .Where(x => Service.UnlockState.IsCompanionUnlocked(x))
            .Select(x => x.RowId)
            .ToList();

        UnlockedFashionAccessoryKeys = Service.Data.GetExcelSheet<Ornament>()
            .Where(x => x.Icon is not (0 or 786)) // 786 is the invalid icon used for accessories which became glasses
            .Where(x => Service.UnlockState.IsOrnamentUnlocked(x))
            .Select(x => x.RowId)
            .ToList();

        UnlockedFacewearKeys = Service.Data.GetExcelSheet<Glasses>()
            .Where(x => Service.UnlockState.IsGlassesUnlocked(x))
            .Select(x => x.RowId)
            .ToList();

        UnlockedFacewearStyleKeys = Service.Data.GetExcelSheet<Glasses>()
            .Where(x => Service.UnlockState.IsGlassesUnlocked(x))
            .Select(x => x.Style.RowId)
            .Distinct()
            .ToList();

        UnlockedCollectionKeys = Service.Data.GetExcelSheet<McGuffin>()
            .Where(x => Service.UnlockState.IsMcGuffinUnlocked(x))
            .Select(x => x.RowId)
            .ToList();

        Service.Log.Verbose($"{UnlockedDutyKeys.Count} duties unlocked.");
        Service.Log.Verbose($"{UnlockedEmoteKeys.Count} emotes unlocked.");
    }

    public void RefreshGearsets() {
        var gsEntries = RaptureGearsetModule.Instance()->Entries;
        var gearsets = new List<Gearset>();
        for (var i = 0; i < gsEntries.Length; i++) {
            ref var gs = ref gsEntries[i];
            if ((gs.Flags & RaptureGearsetModule.GearsetFlag.Exists) != 0)
                gearsets.Add(new Gearset(i, gs.NameString, gs.ClassJob));
        }
        Gearsets = gearsets;
    }
}
