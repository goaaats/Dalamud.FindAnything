using Dalamud.Game;
using Dalamud.Game.ClientState.Aetherytes;
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

namespace Dalamud.FindAnything;

public class AetheryteManager {
    private static readonly uint[] MarketBoardIds = [
        2,   // New Gridania
        8,   // Limsa Lominsa Lower Decks
        9,   // Ul'Dah - Steps of Nald
        70,  // Foundation
        111, // Kugane
        133, // Crystarium
        182, // Old Sharlayan
        216, // Tulliyolal
    ];

    private static readonly uint[] StrikingDummyIds = [
        3,   // New Gridania
        17,  // Horizon
        23,  // Camp Dragonhead
        52,  // Summerford Farms
        71,  // Falcon's Nest
        76,  // Tailfeather
        98,  // Castrum Oriens
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

    private readonly Teleporter teleporter;

    public AetheryteManager() {
        aetheryteNames = ParseAetherytes(Service.ClientState.ClientLanguage);
        teleporter = new Teleporter(this);
    }

    public void Teleport(IAetheryteEntry info) => teleporter.Teleport(info);

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

    public static bool IsMarketBoardAetheryte(uint id) {
        return MarketBoardIds.Contains(id);
    }

    public static bool IsStrikingDummyAetheryte(uint id) {
        return StrikingDummyIds.Contains(id);
    }

    private static unsafe string GetApartmentName() {
        var sp = RaptureTextModule.Instance()->GetAddonText(8518);
        return sp.ToString();
    }

    private static unsafe string GetSharedHouseName(int ward, int plot) {
        if (ward > 30) return $"SHARED_HOUSE_W{ward}_P{plot}";
        var sp = RaptureTextModule.Instance()->FormatAddonText2IntInt(8519, ward, plot);
        return sp.ToString();
    }

    private static FrozenDictionary<uint, string> ParseAetherytes(ClientLanguage language) {
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

internal class Teleporter(AetheryteManager aetheryteManager) {
    private readonly IpcTeleporter ipcTeleporter = new(Service.PluginInterface);

    private enum TeleportResult {
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

        ShowTeleportResult(entry, StaticTeleporter.Teleport(entry), true);
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

    private class IpcTeleporter {
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

    private static class StaticTeleporter {
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
