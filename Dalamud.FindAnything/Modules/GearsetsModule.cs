using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Dalamud.FindAnything.Modules;

public sealed class GearsetsModule : SearchModule
{
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.Gearsets;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        if (gameState.IsInCombat()) return;

        var cj = Service.Data.GetExcelSheet<ClassJob>();
        foreach (var gearset in FindAnythingPlugin.GameStateCache.Gearsets) {
            var cjRow = cj.GetRow(gearset.ClassJob);

            var score = matcher.MatchesAny(
                normalizer.Searchable(gearset.Name),
                normalizer.Searchable(cjRow.Name),
                normalizer.SearchableAscii(cjRow.Abbreviation),
                ClassJobRolesMap[gearset.ClassJob]
            );
            if (score > 0) {
                ctx.AddResult(new GearsetSearchResult {
                    Score = score * Weight,
                    Gearset = gearset,
                });
            }
        }
    }

    private class GearsetSearchResult : ISearchResult
    {
        public string CatName => "Gearset";
        public string Name => Gearset.Name;
        public ISharedImmediateTexture? Icon => FindAnythingPlugin.TexCache.ClassJobIcons[Gearset.ClassJob];
        public required int Score { get; init; }
        public required GameStateCache.Gearset Gearset { get; init; }

        public object Key => Gearset.Index;

        public unsafe void Selected() {
            RaptureGearsetModule.Instance()->EquipGearset(Gearset.Index);
        }
    }

    public static readonly FrozenDictionary<uint, string> ClassJobRolesMap = new Dictionary<uint, string> {
        { 0, "adventurer" },
        { 1, "tank" },
        { 2, "melee dps" },
        { 3, "tank" },
        { 4, "melee dps" },
        { 5, "ranged dps" },
        { 6, "healer" },
        { 7, "ranged dps" },

        { 8, "doh" },
        { 9, "doh" },
        { 10, "doh" },
        { 11, "doh" },
        { 12, "doh" },
        { 13, "doh" },
        { 14, "doh" },
        { 15, "doh" },
        { 16, "dol" },
        { 17, "dol" },
        { 18, "dol" },

        { 19, "tank" },
        { 20, "melee dps" },
        { 21, "tank" },
        { 22, "melee dps" },
        { 23, "ranged dps" },
        { 24, "healer" },
        { 25, "ranged dps" },
        { 26, "ranged dps" },
        { 27, "ranged dps" },
        { 28, "healer" },
        { 29, "melee dps" },
        { 30, "melee dps" },
        { 31, "ranged dps" },
        { 32, "tank" },
        { 33, "healer" },
        { 34, "melee dps" },
        { 35, "ranged dps" },
        { 36, "duck" },
        { 37, "tank" },
        { 38, "ranged dps" },
        { 39, "melee dps" },
        { 40, "healer" },
        { 41, "melee dps" },
        { 42, "ranged dps" }
    }.ToFrozenDictionary();
}