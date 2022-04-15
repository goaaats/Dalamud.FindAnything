using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Logging;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using Framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace Dalamud.FindAnything;

public unsafe class GameStateCache
{
    // E8 ?? ?? ?? ?? 3C 01 74 1C
    [return:MarshalAs(UnmanagedType.U1)]
    private delegate byte IsDutyUnlockedDelegate(uint contentId);

    private readonly IsDutyUnlockedDelegate? isDutyUnlocked;

    // E8 ?? ?? ?? ?? 84 C0 74 A4
    private delegate byte IsEmoteUnlockedDelegate(UIState* uiState, uint emoteId, byte unk);

    private readonly IsEmoteUnlockedDelegate? isEmoteUnlocked;

    [return:MarshalAs(UnmanagedType.U1)]
    private delegate byte IsMountUnlockedDelegate(IntPtr mountBitmask, uint mountId);
    private readonly IsMountUnlockedDelegate? isMountUnlocked;
    private IntPtr mountBitmask;
    private byte* minionBitmask = null;

    private delegate void SearchForItemByCraftingMethodDelegate(AgentInterface* agent, ushort itemId);
    private readonly SearchForItemByCraftingMethodDelegate? searchForItemByCraftingMethod;

    private delegate void SearchForItemByGatheringMethodDelegate(AgentInterface* agent, ushort itemId);
    private readonly SearchForItemByGatheringMethodDelegate searchForItemByGatheringMethod;

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

    internal bool IsDutyUnlocked(uint contentId) {
        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.LookingForGroup);
        return this.isDutyUnlocked(contentId) > 0;
    }

    internal bool IsMountUnlocked(uint mountId) {
        if (this.mountBitmask == IntPtr.Zero) {
            return false;
        }

        return this.isMountUnlocked(this.mountBitmask, mountId) > 0;
    }

    internal bool IsMinionUnlocked(uint minionId) {
        if (this.minionBitmask == null) {
            return false;
        }

        return ((1 << ((int) minionId & 7)) & this.minionBitmask[minionId >> 3]) > 0;
    }

    internal void SearchForItemByCraftingMethod(ushort itemId) {
        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.RecipeNote);
        this.searchForItemByCraftingMethod(agent, itemId);
    }

    internal void SearchForItemByGatheringMethod(ushort itemId) {
        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.GatheringNote);
        this.searchForItemByGatheringMethod(agent, itemId);
    }

    private GameStateCache()
    {
        if (FindAnythingPlugin.TargetScanner.TryScanText("E8 ?? ?? ?? ?? 3C 01 74 1C", out var dutyUnlockedPtr)) {
            PluginLog.Verbose($"dutyUnlockedPtr: {dutyUnlockedPtr:X}");
            this.isDutyUnlocked = Marshal.GetDelegateForFunctionPointer<IsDutyUnlockedDelegate>(dutyUnlockedPtr);
        }

        if (FindAnythingPlugin.TargetScanner.TryScanText("E8 ?? ?? ?? ?? 84 C0 74 A4", out var emoteUnlockedPtr)) {
            PluginLog.Verbose($"emoteUnlockedPtr: {emoteUnlockedPtr:X}");
            this.isEmoteUnlocked = Marshal.GetDelegateForFunctionPointer<IsEmoteUnlockedDelegate>(emoteUnlockedPtr);
        }

        FindAnythingPlugin.TargetScanner.TryGetStaticAddressFromSig("48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 74 5C 8B CB E8", out this.mountBitmask);
        PluginLog.Verbose($"mountBitmask: {this.mountBitmask:X}");

        if (FindAnythingPlugin.TargetScanner.TryScanText("E8 ?? ?? ?? ?? 84 C0 74 5C 8B CB", out var mountUnlockedPtr)) {
            PluginLog.Verbose($"mountUnlockedPtr: {mountUnlockedPtr:X}");
            this.isMountUnlocked = Marshal.GetDelegateForFunctionPointer<IsMountUnlockedDelegate>(mountUnlockedPtr);
        }

        if (FindAnythingPlugin.TargetScanner.TryScanText("E8 ?? ?? ?? ?? EB 7A 48 83 F8 06", out var searchCraftingPtr)) {
            PluginLog.Verbose($"searchCraftingPtr: {searchCraftingPtr:X}");
            this.searchForItemByCraftingMethod = Marshal.GetDelegateForFunctionPointer<SearchForItemByCraftingMethodDelegate>(searchCraftingPtr);
        }

        if (FindAnythingPlugin.TargetScanner.TryScanText("E8 ?? ?? ?? ?? EB 38 48 83 F8 07", out var searchGatheringPtr)) {
            PluginLog.Verbose($"searchGatheringPtr: {searchGatheringPtr:X}");
            this.searchForItemByGatheringMethod = Marshal.GetDelegateForFunctionPointer<SearchForItemByGatheringMethodDelegate>(searchGatheringPtr);
        }

        if (FindAnythingPlugin.TargetScanner.TryGetStaticAddressFromSig("48 8D 0D ?? ?? ?? ?? 0F B6 04 08 84 D0 75 10 B8 ?? ?? ?? ?? 48 8B 5C 24", out var minionBitmaskPtr)) {
            PluginLog.Verbose($"minionBitmaskPtr: {minionBitmaskPtr:X}");
            this.minionBitmask = (byte*) minionBitmaskPtr;
        }
    }

    public void Refresh()
    {
        if (this.isDutyUnlocked != null)
        {
            UnlockedDutyKeys = FindAnythingPlugin.Data.GetExcelSheet<ContentFinderCondition>()!.Where(x => IsDutyUnlocked(x.Content)).Select(x => x.RowId).ToList();
        }

        if (this.isEmoteUnlocked != null)
        {
            var emotes = new List<uint>();
            foreach (var emote in FindAnythingPlugin.Data.GetExcelSheet<Emote>()!.Where(x => x.Order != 0))
            {
                if (emote.UnlockLink == 0 || this.isEmoteUnlocked(UIState.Instance(), emote.UnlockLink, 1) > 0)
                {
                    emotes.Add(emote.RowId);
                }
            }
            UnlockedEmoteKeys = emotes;
        }

        if (this.isMountUnlocked != null)
        {
            UnlockedMountKeys = FindAnythingPlugin.Data.GetExcelSheet<Mount>()!.Where(x => IsMountUnlocked(x.RowId)).Select(x => x.RowId).ToList();
        }

        if (this.minionBitmask != null)
        {
            UnlockedMinionKeys = FindAnythingPlugin.Data.GetExcelSheet<Companion>()!.Where(x => IsMinionUnlocked(x.RowId)).Select(x => x.RowId).ToList();
        }

        var gsModule = RaptureGearsetModule.Instance();
        var cj = FindAnythingPlugin.Data.GetExcelSheet<ClassJob>()!;
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