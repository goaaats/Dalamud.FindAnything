using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;

namespace Dalamud.FindAnything;

public unsafe class GameStateCache
{
    public record Gearset(int Index, string Name, uint ClassJob);

    public IReadOnlyList<uint> UnlockedDutyKeys { get; private set; } = [];
    public IReadOnlyList<uint> UnlockedEmoteKeys { get; private set; } = [];
    public IReadOnlyList<uint> UnlockedMountKeys { get; private set; } = [];
    public IReadOnlyList<uint> UnlockedMinionKeys { get; private set; } = [];
    public IReadOnlyList<uint> UnlockedCollectionKeys { get; private set; } = [];
    public IReadOnlyList<uint> UnlockedFashionAccessoryKeys { get; private set; } = [];
    public IReadOnlyList<Gearset> Gearsets { get; private set; } = [];

    private GameStateCache()
    {
        // Nothing to do here =D
        // MidoriKami removed all the sigs, sooooo yeah, happy fun times.
    }

    public void Refresh()
    {
        var playerState = PlayerState.Instance();
        var uiState = UIState.Instance();

        UnlockedDutyKeys = Service.Data.GetExcelSheet<ContentFinderCondition>()
            .Where(x => UIState.IsInstanceContentUnlocked(x.Content.RowId))
            .Select(x => x.RowId)
            .ToList();

        UnlockedEmoteKeys = Service.Data.GetExcelSheet<Emote>()
            .Where(e =>
                e.Order != 0
                && (e.UnlockLink == 0 || uiState->IsUnlockLinkUnlockedOrQuestCompleted(e.UnlockLink)))
            .Select(emote => emote.RowId)
            .ToList();

        UnlockedMountKeys = Service.Data.GetExcelSheet<Mount>()
            .Where(x => playerState->IsMountUnlocked(x.RowId))
            .Select(x => x.RowId)
            .ToList();

        UnlockedMinionKeys = Service.Data.GetExcelSheet<Companion>()
            .Where(x => uiState->IsCompanionUnlocked(x.RowId))
            .Select(x => x.RowId)
            .ToList();

        UnlockedFashionAccessoryKeys = Service.Data.GetExcelSheet<Ornament>()
            .Where(x => playerState->IsOrnamentUnlocked(x.RowId))
            .Select(x => x.RowId)
            .ToList();

        UnlockedCollectionKeys = Service.Data.GetExcelSheet<McGuffin>()
            .Where(x =>
                x.UIData.ValueNullable is { RowId: > 0 }
                && playerState->IsMcGuffinUnlocked(x.RowId))
            .Select(x => x.RowId)
            .ToList();

        var gsEntries = RaptureGearsetModule.Instance()->Entries;
        var gearsets = new List<Gearset>();
        for (var i = 0; i < gsEntries.Length; i++) {
            ref var gs = ref gsEntries[i];
            if ((gs.Flags & RaptureGearsetModule.GearsetFlag.Exists) != 0)
                gearsets.Add(new Gearset(i, gs.NameString, gs.ClassJob));
        }
        Gearsets = gearsets;

        Service.Log.Verbose($"{UnlockedDutyKeys.Count} duties unlocked.");
        Service.Log.Verbose($"{UnlockedEmoteKeys.Count} emotes unlocked.");
    }

    public static GameStateCache Load() => new GameStateCache();
}