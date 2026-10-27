using Dalamud.Game.ClientState.Aetherytes;
using Dalamud.Interface.Textures;
using System.Collections.Generic;
using System.Linq;

namespace Dalamud.FindAnything.Modules;

public sealed class AetheryteModule : SearchModule {
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.Aetheryte;

    private readonly AetheryteManager aetheryteManager;
    private readonly Teleporter teleporter;

    private readonly string closestMarketSearchable;
    private readonly string closestInnSearchable;
    private readonly string closestDummySearchable;

    private const uint AetheryteIconId = 66417;
    private const uint InnRoomIconId = 66419;

    public AetheryteModule() {
        aetheryteManager = new AetheryteManager();
        teleporter = new Teleporter(aetheryteManager);

        closestMarketSearchable = FindAnythingPlugin.Normalizer.SearchableAscii("Closest Market Board");
        closestInnSearchable = FindAnythingPlugin.Normalizer.SearchableAscii("Closest Inn Room");
        closestDummySearchable = FindAnythingPlugin.Normalizer.SearchableAscii("Closest Striking Dummy");
    }

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        if (gameState.IsInDuty() || gameState.IsInCombat()) return;

        var marketBoardResults = new List<IAetheryteEntry>();
        var strikingDummyResults = new List<IAetheryteEntry>();
        var innRoomResults = new List<IAetheryteEntry>();
        var marketScore = 0;
        var dummyScore = 0;
        var innScore = 0;

        foreach (var aetheryte in FindAnythingPlugin.GameStateCache.AetheryteEntries) {
            var aetheryteName = aetheryteManager.GetAetheryteName(aetheryte);
            var territoryTypeSearchEntry = FindAnythingPlugin.SearchDatabase.TerritoryType[aetheryte.TerritoryId];
            var score = matcher.MatchesAny(
                normalizer.Searchable(aetheryteName),
                territoryTypeSearchEntry.Searchable
            );

            if (score > 0) {
                ctx.AddResult(new AetheryteSearchResult {
                    Score = score * Weight,
                    Name = aetheryteName,
                    Data = aetheryte,
                    IconId = AetheryteIconId,
                    TerriName = territoryTypeSearchEntry.Display,
                    Teleporter = teleporter,
                });
            }

            marketScore = matcher.Matches(closestMarketSearchable);
            innScore = matcher.Matches(closestInnSearchable);
            if (AetheryteManager.IsMarketBoardAetheryte(aetheryte.AetheryteId)) {
                if (FindAnythingPlugin.Configuration.AetheryteShortcuts.HasFlag(Configuration.AetheryteAdditionalShortcut.MarketBoard) && marketScore > 0)
                    marketBoardResults.Add(aetheryte);

                if (FindAnythingPlugin.Configuration.AetheryteShortcuts.HasFlag(Configuration.AetheryteAdditionalShortcut.InnRoom) && innScore > 0) {
                    if (!FindAnythingPlugin.Configuration.AetheryteInnRoomShortcutExcludeLimsa || aetheryte.TerritoryId != 128)
                        innRoomResults.Add(aetheryte);
                }
            }

            dummyScore = matcher.Matches(closestDummySearchable);
            if (FindAnythingPlugin.Configuration.AetheryteShortcuts.HasFlag(Configuration.AetheryteAdditionalShortcut.StrikingDummy) && dummyScore > 0 && AetheryteManager.IsStrikingDummyAetheryte(aetheryte.AetheryteId)) {
                strikingDummyResults.Add(aetheryte);
            }

            if (ctx.OverLimit()) break;
        }

        if (marketBoardResults.Count > 0) {
            var closestMarketBoard = marketBoardResults.OrderBy(a1 => a1.GilCost).First();
            ctx.AddResult(new AetheryteSearchResult {
                Score = marketScore * Weight,
                Name = "Closest Market Board",
                Data = closestMarketBoard,
                IconId = AetheryteIconId,
                TerriName = FindAnythingPlugin.SearchDatabase.TerritoryType[closestMarketBoard.TerritoryId].Display,
                Teleporter = teleporter,
            });
        }

        if (strikingDummyResults.Count > 0) {
            var closestStrikingDummy = strikingDummyResults.OrderBy(a1 => a1.GilCost).First();
            ctx.AddResult(new AetheryteSearchResult {
                Score = dummyScore * Weight,
                Name = "Closest Striking Dummy",
                Data = closestStrikingDummy,
                IconId = AetheryteIconId,
                TerriName = FindAnythingPlugin.SearchDatabase.TerritoryType[closestStrikingDummy.TerritoryId].Display,
                Teleporter = teleporter,
            });
        }

        if (innRoomResults.Count > 0) {
            var closestInnRoom = innRoomResults.OrderBy(a1 => a1.GilCost).First();
            ctx.AddResult(new AetheryteSearchResult {
                Score = innScore * Weight,
                Name = "Closest Inn Room",
                Data = closestInnRoom,
                IconId = InnRoomIconId,
                TerriName = FindAnythingPlugin.SearchDatabase.TerritoryType[closestInnRoom.TerritoryId].Display,
                Teleporter = teleporter,
            });
        }
    }

    internal class AetheryteSearchResult : ISearchResult {
        private string GetCatName() {
            var name = "Aetherytes";
            if (FindAnythingPlugin.Configuration.DoAetheryteGilCost) {
                name += $" ({TerriName} - {Data.GilCost} Gil)";
            } else {
                name += $" ({TerriName})";
            }
            return name;
        }

        public string CatName => field ??= GetCatName();
        public required string Name { get; init; }
        public ISharedImmediateTexture Icon => field ??= Utils.GetIcon(IconId);
        public required uint IconId { get; init; }
        public required int Score { get; init; }
        public required IAetheryteEntry Data { get; init; }
        public required string TerriName { get; init; }
        public required Teleporter Teleporter { get; init; }

        public object Key => (Data.AetheryteId, Data.SubIndex);

        public void Selected() {
            Teleporter.Teleport(Data);
        }
    }
}
