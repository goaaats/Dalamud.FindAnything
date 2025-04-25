using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using System.Collections.Generic;
using System.Linq;

namespace Dalamud.FindAnything;

public unsafe class GameStateCache
{
    public struct Gearset
    {
        public int Slot { get; set; }
        public string Name { get; set; }
        public uint ClassJob { get; set; }
    }

    public IReadOnlyList<ContentFinderCondition> UnlockedDuties { get; private set; }
    public IReadOnlyList<Emote> UnlockedEmotes { get; private set; }
    public IReadOnlyList<Mount> UnlockedMounts { get; private set; }
    public IReadOnlyList<Companion> UnlockedMinions { get; private set; }
    public IReadOnlyList<Ornament> UnlockedFashionAccessories { get; set; }
    public IReadOnlyList<McGuffin> UnlockedCollections { get; set; }
    public IReadOnlyList<MainCommand> UnlockedMainCommands { get; set; }
    public IReadOnlyList<Gearset> Gearsets { get; private set; }
    
    internal void OpenRecipe(uint recipeId) => AgentRecipeNote.Instance()->OpenRecipeByRecipeId(recipeId);
    internal void SearchForItemByCraftingMethod(uint itemId) => AgentRecipeNote.Instance()->SearchRecipeByItemId(itemId);

    internal void SearchForItemByGatheringMethod(ushort itemId) => AgentGatheringNote.Instance()->OpenGatherableByItemId(itemId);

    private GameStateCache()
    {
        // Nothing to do here =D
        // MidoriKami removed all the sigs, sooooo yeah, happy fun times.
    }

    public void Refresh()
    {
        UnlockedDuties = FindAnythingPlugin.Data.GetExcelSheet<ContentFinderCondition>()!
            .Where(row => UIState.IsInstanceContentUnlocked(row.Content.RowId))
            .ToList();

        UnlockedEmotes = FindAnythingPlugin.Data.GetExcelSheet<Emote>()!
            .Where(row => row.Order != 0 && AgentEmote.Instance()->CanUseEmote((ushort)row.RowId))
            .ToList();

        UnlockedMounts = FindAnythingPlugin.Data.GetExcelSheet<Mount>()!
            .Where(row => ActionManager.Instance()->GetActionStatus(ActionType.Mount, row.RowId) != 0)
            .ToList();

        UnlockedMinions = FindAnythingPlugin.Data.GetExcelSheet<Companion>()!
            .Where(row => ActionManager.Instance()->GetActionStatus(ActionType.Companion, row.RowId) != 0)
            .ToList();

        UnlockedFashionAccessories = FindAnythingPlugin.Data.GetExcelSheet<Ornament>()!
            .Where(row => PlayerState.Instance()->IsOrnamentUnlocked(row.RowId))
            .ToList();

        UnlockedCollections = FindAnythingPlugin.Data.GetExcelSheet<McGuffin>()!
            .Where(row => row.UIData.Value is { RowId: > 0 } && PlayerState.Instance()->IsMcGuffinUnlocked(row.RowId))
            .ToList();

        UnlockedMainCommands = FindAnythingPlugin.Data.GetExcelSheet<MainCommand>()!
            .Where(row => UIModule.Instance()->IsMainCommandUnlocked(row.RowId))
            .ToList();
        
        var gsEntries = RaptureGearsetModule.Instance()->Entries;
        var gearsets = new List<Gearset>();
        for (var i = 0; i < gsEntries.Length; i++)
        {
            var gs = gsEntries[i];

            if (!gs.Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists))
                continue;

            gearsets.Add(new Gearset
            {
                Slot = i + 1,
                ClassJob = gs.ClassJob,
                Name = gs.NameString,
            });
        }

        Gearsets = gearsets;

        FindAnythingPlugin.Log.Verbose($"{UnlockedDuties.Count} duties unlocked.");
        FindAnythingPlugin.Log.Verbose($"{UnlockedEmotes.Count} emotes unlocked.");
    }

    public static GameStateCache Load() => new GameStateCache();
}