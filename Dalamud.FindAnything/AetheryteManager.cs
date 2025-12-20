using Dalamud.Game;
using Dalamud.Game.ClientState.Aetherytes;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Dalamud.FindAnything;

public class AetheryteManager
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

    public AetheryteManager(ClientLanguage clientLanguage) {
        aetheryteNames = ParseAetherytes(clientLanguage);
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
        foreach (var row in FindAnythingPlugin.Data.GetExcelSheet<Aetheryte>(language)) {
            var name = row.PlaceName.ValueNullable?.Name.ToText();
            if (string.IsNullOrEmpty(name))
                continue;
            dict[row.RowId] = name;
        }
        return dict.ToFrozenDictionary();
    }
}