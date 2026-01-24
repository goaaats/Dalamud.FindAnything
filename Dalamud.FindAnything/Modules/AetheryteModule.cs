using Dalamud.Game.ClientState.Aetherytes;
using Dalamud.Interface.Textures;
using Lumina.Excel.Sheets;
using System.Collections.Generic;
using System.Linq;

namespace Dalamud.FindAnything.Modules;

public sealed class AetheryteModule : SearchModule {
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.Aetheryte;

    private readonly AetheryteManager aetheryteManager;
    private readonly Teleporter teleporter;

    public AetheryteModule() {
        aetheryteManager = new AetheryteManager();
        teleporter = new Teleporter(aetheryteManager);
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
            var terriName = FindAnythingPlugin.SearchDatabase.GetString<TerritoryType>(aetheryte.TerritoryId);
            var score = matcher.MatchesAny(
                normalizer.Searchable(aetheryteName),
                terriName.Searchable
            );

            if (score > 0) {
                ctx.AddResult(new AetheryteSearchResult {
                    Score = score * Weight,
                    Name = aetheryteName,
                    Data = aetheryte,
                    Icon = FindAnythingPlugin.TexCache.AetheryteIcon,
                    TerriName = terriName.Display,
                    Teleporter = teleporter,
                });
            }

            marketScore = matcher.Matches(normalizer.SearchableAscii("Closest Market Board"));
            innScore = matcher.Matches(normalizer.SearchableAscii("Closest Inn Room"));
            if (AetheryteManager.IsMarketBoardAetheryte(aetheryte.AetheryteId)) {
                if (FindAnythingPlugin.Configuration.AetheryteShortcuts.HasFlag(Configuration.AetheryteAdditionalShortcut.MarketBoard) && marketScore > 0)
                    marketBoardResults.Add(aetheryte);

                if (FindAnythingPlugin.Configuration.AetheryteShortcuts.HasFlag(Configuration.AetheryteAdditionalShortcut.InnRoom) && innScore > 0) {
                    if (!FindAnythingPlugin.Configuration.AetheryteInnRoomShortcutExcludeLimsa || aetheryte.TerritoryId != 128)
                        innRoomResults.Add(aetheryte);
                }
            }

            dummyScore = matcher.Matches(normalizer.SearchableAscii("Closest Striking Dummy"));
            if (FindAnythingPlugin.Configuration.AetheryteShortcuts.HasFlag(Configuration.AetheryteAdditionalShortcut.StrikingDummy) &&
                dummyScore > 0 && AetheryteManager.IsStrikingDummyAetheryte(aetheryte.AetheryteId)) {
                strikingDummyResults.Add(aetheryte);
            }

            if (ctx.OverLimit()) break;
        }

        if (marketBoardResults.Count > 0) {
            var closestMarketBoard = marketBoardResults.OrderBy(a1 => a1.GilCost).First();
            var terriName = FindAnythingPlugin.SearchDatabase.GetString<TerritoryType>(closestMarketBoard.TerritoryId);
            ctx.AddResult(new AetheryteSearchResult {
                Score = marketScore * Weight,
                Name = "Closest Market Board",
                Data = closestMarketBoard,
                Icon = FindAnythingPlugin.TexCache.AetheryteIcon,
                TerriName = terriName.Display,
                Teleporter = teleporter,
            });
        }

        if (strikingDummyResults.Count > 0) {
            var closestStrikingDummy = strikingDummyResults.OrderBy(a1 => a1.GilCost).First();
            var terriName = FindAnythingPlugin.SearchDatabase.GetString<TerritoryType>(closestStrikingDummy.TerritoryId);
            ctx.AddResult(new AetheryteSearchResult {
                Score = dummyScore * Weight,
                Name = "Closest Striking Dummy",
                Data = closestStrikingDummy,
                Icon = FindAnythingPlugin.TexCache.AetheryteIcon,
                TerriName = terriName.Display,
                Teleporter = teleporter,
            });
        }
        if (innRoomResults.Count > 0) {
            var closestInnRoom = innRoomResults.OrderBy(a1 => a1.GilCost).First();
            var terriName = FindAnythingPlugin.SearchDatabase.GetString<TerritoryType>(closestInnRoom.TerritoryId);
            ctx.AddResult(new AetheryteSearchResult {
                Score = innScore * Weight,
                Name = "Closest Inn Room",
                Data = closestInnRoom,
                Icon = FindAnythingPlugin.TexCache.InnRoomIcon,
                TerriName = terriName.Display,
                Teleporter = teleporter,
            });
        }
    }

    internal class AetheryteSearchResult : ISearchResult {
        public string CatName {
            get {
                var name = "Aetherytes";
                if (FindAnythingPlugin.Configuration.DoAetheryteGilCost) {
                    name += $" ({TerriName} - {Data.GilCost} Gil)";
                } else {
                    name += $" ({TerriName})";
                }

                return name;
            }
        }

        public required string Name { get; init; }
        public required ISharedImmediateTexture? Icon { get; init; }
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
