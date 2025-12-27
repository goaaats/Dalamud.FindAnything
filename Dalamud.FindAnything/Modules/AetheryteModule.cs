using Dalamud.Game;
using Dalamud.Game.ClientState.Aetherytes;
using Dalamud.Interface.Textures;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.STD;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Dalamud.FindAnything.Modules;

public sealed class AetheryteModule : SearchModule
{
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

        foreach (var aetheryte in Service.Aetherytes) {
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
            if (aetheryteManager.IsMarketBoardAetheryte(aetheryte.AetheryteId)) {
                if (FindAnythingPlugin.Configuration.AetheryteShortcuts.HasFlag(Configuration.AetheryteAdditionalShortcut.MarketBoard) && marketScore > 0)
                    marketBoardResults.Add(aetheryte);

                if (FindAnythingPlugin.Configuration.AetheryteShortcuts.HasFlag(Configuration.AetheryteAdditionalShortcut.InnRoom) && innScore > 0) {
                    if (!FindAnythingPlugin.Configuration.AetheryteInnRoomShortcutExcludeLimsa || aetheryte.TerritoryId != 128)
                        innRoomResults.Add(aetheryte);
                }
            }

            dummyScore = matcher.Matches(normalizer.SearchableAscii("Closest Striking Dummy"));
            if (FindAnythingPlugin.Configuration.AetheryteShortcuts.HasFlag(Configuration.AetheryteAdditionalShortcut.StrikingDummy) &&
                dummyScore > 0 && aetheryteManager.IsStrikingDummyAetheryte(aetheryte.AetheryteId)) {
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

    internal class AetheryteSearchResult : ISearchResult
    {
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

internal class AetheryteManager
{
    private static readonly uint[] MarketBoardIds = [
        2, // New Gridania
        8, // Limsa Lominsa Lower Decks
        9, // Ul'Dah - Steps of Nald
        70, // Foundation
        111, // Kugane
        133, // Crystarium
        182, // Old Sharlayan
        216, // Tulliyolal
    ];

    private static readonly uint[] StrikingDummyIds = [
        3, // New Gridania
        17, // Horizon
        23, // Camp Dragonhead
        52, // Summerford Farms
        71, // Falcon's Nest
        76, // Tailfeather
        98, // Castrum Oriens
        102, // Porta Praetoria
        107, // Namai
        137, // Stilltide
        140, // Mord Souq
        147, // The Ondo Cups
        166, // The Archeion
        169, // Yedlihmad
        181, // Base Omicron
        200, // Wachunpelo (Urqopatcha)
        202, // Ok'hanu (Kozma'uka)
        213, // Leynode Mnemo (Living Memory)
    ];

    private readonly FrozenDictionary<uint, string> aetheryteNames;
    private readonly Dictionary<(int, int), string> houseNames = new();
    private string? apartmentName;

    public AetheryteManager() {
        aetheryteNames = ParseAetherytes(Service.ClientState.ClientLanguage);
    }

    public bool IsMarketBoardAetheryte(uint id) {
        return MarketBoardIds.Contains(id);
    }

    public bool IsStrikingDummyAetheryte(uint id) {
        return StrikingDummyIds.Contains(id);
    }

    public string GetAetheryteName(IAetheryteEntry info) {
        if (info.IsApartment) {
            return apartmentName ??= GetApartmentName();
        }

        if (info.IsSharedHouse) {
            if (houseNames.TryGetValue((info.Ward, info.Plot), out var house))
                return house;
            house = GetSharedHouseName(info.Ward, info.Plot);
            houseNames.Add((info.Ward, info.Plot), house);
            return house;
        }

        return aetheryteNames.GetValueOrDefault(info.AetheryteId, "NO_DATA");
    }

    private unsafe string GetApartmentName() {
        var sp = RaptureTextModule.Instance()->GetAddonText(8518);
        return sp.ToString();
    }

    private unsafe string GetSharedHouseName(int ward, int plot) {
        if (ward > 30) return $"SHARED_HOUSE_W{ward}_P{plot}";
        var sp = RaptureTextModule.Instance()->FormatAddonText2IntInt(8519, ward, plot);
        return sp.ToString();
    }

    private FrozenDictionary<uint, string> ParseAetherytes(ClientLanguage language) {
        var dict = new Dictionary<uint, string>();
        foreach (var row in Service.Data.GetExcelSheet<Aetheryte>(language)) {
            var name = row.PlaceName.ValueNullable?.Name.ToText();
            if (string.IsNullOrEmpty(name))
                continue;
            dict[row.RowId] = name;
        }
        return dict.ToFrozenDictionary();
    }
}

internal class Teleporter(AetheryteManager aetheryteManager)
{
    private readonly IpcTeleporter ipcTeleporter = new(Service.PluginInterface);

    private enum TeleportResult
    {
        Success,
        BadState,
        BadDestination,
    }

    public void Teleport(IAetheryteEntry entry) {
        try {
            var (result, showSuccessMessage) = ipcTeleporter.Teleport(entry);
            ShowTeleportResult(entry, result, showSuccessMessage);
            return;
        } catch (IpcNotReadyError) {
            Service.Log.Verbose("\"Teleporter\" IPC not found. Using built-in teleport.");
        }

        ShowTeleportResult(entry, BasicTeleporter.Teleport(entry), true);
    }

    private void ShowTeleportResult(IAetheryteEntry entry, TeleportResult result, bool showSuccessMessage) {
        if (result != TeleportResult.Success) {
            FindAnythingPlugin.Instance.UserError(result switch {
                TeleportResult.BadState => "Cannot teleport in this situation.",
                TeleportResult.BadDestination => "Cannot teleport to that destination.",
                _ => "Teleport failed.",
            });
        } else if (showSuccessMessage) {
            Service.ChatGui.Print($"Teleported to {aetheryteManager.GetAetheryteName(entry)}!");
        }
    }

    private class IpcTeleporter
    {
        private readonly ICallGateSubscriber<uint, byte, bool> teleportIpc;
        private readonly ICallGateSubscriber<bool> showTeleportChatMessageIpc;

        public IpcTeleporter(IDalamudPluginInterface pluginInterface) {
            teleportIpc = pluginInterface.GetIpcSubscriber<uint, byte, bool>("Teleport");
            showTeleportChatMessageIpc = pluginInterface.GetIpcSubscriber<bool>("Teleport.ChatMessage");
        }

        public (TeleportResult, bool) Teleport(IAetheryteEntry entry) {
            if (teleportIpc.InvokeFunc(entry.AetheryteId, entry.SubIndex)) {
                return (TeleportResult.Success, showTeleportChatMessageIpc.InvokeFunc());
            }

            return (TeleportResult.BadState, false);
        }
    }

    private static class BasicTeleporter
    {
        private static unsafe bool UpdateList() {
            if (Control.GetLocalPlayer() == null)
                return false;

            try {
                var tp = Telepo.Instance();
                return tp->UpdateAetheryteList() != null;
            } catch (Exception ex) {
                Service.Log.Error(ex, "Error while updating the Aetheryte list for built-in teleport");
                return false;
            }
        }

        private static unsafe StdVector<TeleportInfo> GetList() {
            return Telepo.Instance()->TeleportList;
        }

        public static unsafe TeleportResult Teleport(IAetheryteEntry entry) {
            if (!UpdateList())
                return TeleportResult.BadState;

            if (!GetList().Exists(tp => tp.AetheryteId == entry.AetheryteId && tp.SubIndex == entry.SubIndex))
                return TeleportResult.BadDestination;

            if (ActionManager.Instance()->GetActionStatus(ActionType.Action, 5) != 0)
                return TeleportResult.BadState;

            if (Telepo.Instance()->Teleport(entry.AetheryteId, entry.SubIndex)) {
                return TeleportResult.Success;
            }

            return TeleportResult.BadState;
        }
    }
}