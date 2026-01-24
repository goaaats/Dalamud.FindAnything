using Dalamud.Game.ClientState.Aetherytes;
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

    public ContentFinderCondition[] UnlockedDuties = [];
    public uint[] UnlockedDutyKeys = [];
    public Emote[] UnlockedEmotes = [];
    public Mount[] UnlockedMounts = [];
    public Companion[] UnlockedMinions = [];
    public Ornament[] UnlockedFashionAccessories = [];
    public Glasses[] UnlockedFacewear = [];
    public GlassesStyle[] UnlockedFacewearStyles = [];
    public McGuffin[] UnlockedCollectionItems = [];
    public Recipe[] UnlockedRecipes = [];
    public RecipeGroup[] UnlockedRecipeGroups = [];

    public Gearset[] Gearsets { get; private set; } = [];
    public IAetheryteEntry[] AetheryteEntries { get; private set; } = [];

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
        RefreshAetherytes();
    }

    private void RefreshUnlocks() {
        UnlockedDuties = Service.Data.GetExcelSheet<ContentFinderCondition>()
            .Where(x =>
                x.Content.GetValueOrDefault<InstanceContent>() is { } instanceContent
                && Service.UnlockState.IsInstanceContentUnlocked(instanceContent))
            .ToArray();

        UnlockedDutyKeys = UnlockedDuties.Select(x => x.RowId)
            .ToArray();

        UnlockedEmotes = Service.Data.GetExcelSheet<Emote>()
            .Where(x => x.Order != 0)
            .Where(Service.UnlockState.IsEmoteUnlocked)
            .ToArray();

        UnlockedMounts = Service.Data.GetExcelSheet<Mount>()
            .Where(Service.UnlockState.IsMountUnlocked)
            .ToArray();

        UnlockedMinions = Service.Data.GetExcelSheet<Companion>()
            .Where(Service.UnlockState.IsCompanionUnlocked)
            .ToArray();

        UnlockedFashionAccessories = Service.Data.GetExcelSheet<Ornament>()
            .Where(x => x.Icon is not (0 or 786)) // 786 is the invalid icon used for accessories which became glasses
            .Where(Service.UnlockState.IsOrnamentUnlocked)
            .ToArray();

        UnlockedFacewear = Service.Data.GetExcelSheet<Glasses>()
            .Where(Service.UnlockState.IsGlassesUnlocked)
            .ToArray();

        UnlockedFacewearStyles = UnlockedFacewear
            .Select(x => x.Style.ValueNullable)
            .OfType<GlassesStyle>()
            .DistinctBy(x => x.RowId)
            .ToArray();

        UnlockedCollectionItems = Service.Data.GetExcelSheet<McGuffin>()
            .Where(Service.UnlockState.IsMcGuffinUnlocked)
            .ToArray();

        UnlockedRecipes = Service.Data.GetExcelSheet<Recipe>()
            .Where(x => x.ItemResult.RowId != 0)
            .Where(Service.UnlockState.IsRecipeUnlocked)
            .ToArray();

        UnlockedRecipeGroups = UnlockedRecipes
            .Where(Service.UnlockState.IsRecipeUnlocked)
            .Where(x => x.ItemResult.IsValid)
            .GroupBy(x => x.ItemResult.RowId)
            .Select(x => {
                var recipes = x.ToArray();
                return new RecipeGroup(recipes[0].ItemResult.Value, recipes);
            })
            .ToArray();

        Service.Log.Verbose($"{UnlockedDuties.Length} duties unlocked.");
        Service.Log.Verbose($"{UnlockedEmotes.Length} emotes unlocked.");
    }

    public void RefreshGearsets() {
        var gsEntries = RaptureGearsetModule.Instance()->Entries;
        var gearsets = new List<Gearset>();
        for (var i = 0; i < gsEntries.Length; i++) {
            ref var gs = ref gsEntries[i];
            if ((gs.Flags & RaptureGearsetModule.GearsetFlag.Exists) != 0)
                gearsets.Add(new Gearset(i, gs.NameString, gs.ClassJob));
        }
        Gearsets = gearsets.ToArray();
    }

    private void RefreshAetherytes() {
        AetheryteEntries = Service.Aetherytes.ToArray();
    }
}

public record RecipeGroup(Item Item, Recipe[] Recipes);
