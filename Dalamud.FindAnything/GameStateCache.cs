using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Logging;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.GeneratedSheets;

namespace Dalamud.FindAnything;

public unsafe class GameStateCache
{
    public struct Gearset
    {
        public int Slot { get; set; }
        public string Name { get; set; }
        public uint ClassJob { get; set; }
    }

    public IReadOnlyList<uint> UnlockedDutyKeys { get; private set; }
    public IReadOnlyList<uint> UnlockedEmoteKeys { get; private set; }
    public IReadOnlyList<uint> UnlockedMountKeys { get; private set; }
    public IReadOnlyList<uint> UnlockedMinionKeys { get; private set; }
    public IReadOnlyList<Gearset> Gearsets { get; private set; }
    
    internal bool IsMinionUnlocked(uint minionId) => UIState.Instance()->IsCompanionUnlocked(minionId);

    internal void SearchForItemByCraftingMethod(ushort itemId) => AgentRecipeNote.Instance()->OpenRecipeByItemId(itemId);

    internal void SearchForItemByGatheringMethod(ushort itemId) => AgentGatheringNote.Instance()->OpenGatherableByItemId(itemId);

    private GameStateCache()
    {
        // Nothing to do here =D
        // MidoriKami removed all the sigs, sooooo yeah, happy fun times.
    }

    public void Refresh()
    {
        UnlockedDutyKeys = FindAnythingPlugin.Data.GetExcelSheet<ContentFinderCondition>()!.Where(x => UIState.IsInstanceContentUnlocked(x.Content)).Select(x => x.RowId).ToList();

        var emotes = new List<uint>();
        foreach (var emote in FindAnythingPlugin.Data.GetExcelSheet<Emote>()!.Where(x => x.Order != 0))
        {
            if (emote.UnlockLink == 0 || UIState.Instance()->IsUnlockLinkUnlockedOrQuestCompleted(emote.UnlockLink))
            {
                emotes.Add(emote.RowId);
            }
        }
        UnlockedEmoteKeys = emotes;

        UnlockedMountKeys = FindAnythingPlugin.Data.GetExcelSheet<Mount>()!.Where(x => PlayerState.Instance()->IsMountUnlocked(x.RowId)).Select(x => x.RowId).ToList();

        UnlockedMinionKeys = FindAnythingPlugin.Data.GetExcelSheet<Companion>()!.Where(x => IsMinionUnlocked(x.RowId)).Select(x => x.RowId).ToList();
        
        var gsModule = RaptureGearsetModule.Instance();
        
        var gearsets = new List<Gearset>();
        for (var i = 0; i < 100; i++)
        {
            var gs = gsModule->Gearset[i];

            if (gs == null || !gs->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists))
                continue;

            var name = MemoryHelper.ReadString(new IntPtr(gs->Name), 47);

            gearsets.Add(new Gearset
            {
                Slot = i + 1,
                ClassJob = gs->ClassJob,
                Name = name,
            });
        }

        Gearsets = gearsets;

        PluginLog.LogVerbose($"{UnlockedDutyKeys.Count} duties unlocked.");
        PluginLog.LogVerbose($"{UnlockedEmoteKeys.Count} emotes unlocked.");
    }

    public static GameStateCache Load() => new GameStateCache();
}