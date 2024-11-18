using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Game.ClientState.Aetherytes;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;

namespace Dalamud.FindAnything {
    public class AetheryteManager {
        public readonly Dictionary<uint, string> AetheryteNames = new(150);
        public readonly Dictionary<uint, string> TerritoryNames = new(80);
        private readonly Dictionary<(int, int), string> m_HouseNames = new(5);
        private string? m_AppartmentName;

        private uint[] m_EstateIds = { 0 };
        private readonly uint[] m_MarketBoardIds = {
            2, // New Gridania
            8, // Limsa Lominsa Lower Decks
            9, // Ul'Dah - Steps of Nald
            70, // Foundation
            111, // Kugane
            133, // Crystarium
            182, // Old Sharlayan
        };
        private readonly uint[] m_StrikingDummyIds = {
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
            181 // Base Omicron
        };

        private AetheryteManager()
        {
            var lang = FindAnythingPlugin.ClientState.ClientLanguage;
            SetupAetherytes(AetheryteNames, lang);
            SetupMaps(TerritoryNames, lang);
            SetupEstateIds(out m_EstateIds);
        }

        public static AetheryteManager Load() => new();

        public bool IsHousingAetheryte(uint id, byte plot, byte ward, byte subId) {
            if (plot != 0 || ward != 0 || subId != 0)
                return true;
            return m_EstateIds.Contains(id);
        }

        public bool IsMarketBoardAetheryte(uint id)
        {
            return m_MarketBoardIds.Contains(id);
        }
        
        public bool IsStrikingDummyAetheryte(uint id)
        {
            return m_StrikingDummyIds.Contains(id);
        }

        public string GetAetheryteName(IAetheryteEntry info) {
            if (info.IsApartment)
                return m_AppartmentName ??= GetAppartmentName();
            if (info.IsSharedHouse) {
                if (m_HouseNames.TryGetValue((info.Ward, info.Plot), out var house))
                    return house;
                house = GetSharedHouseName(info.Ward, info.Plot);
                m_HouseNames.Add((info.Ward, info.Plot), house);
                return house;
            }

            return AetheryteNames.TryGetValue(info.AetheryteId, out var name) ? name : "NO_DATA";
        }

        private static unsafe string GetAppartmentName() {
            var tm = UIModule.Instance()->GetRaptureTextModule();
            var sp = tm->GetAddonText(8518);
            var name = Marshal.PtrToStringUTF8(new IntPtr(sp)) ?? string.Empty;
            return FindAnythingPlugin.PluginInterface.Sanitizer.Sanitize(name);
        }

        private static unsafe string GetSharedHouseName(int ward, int plot) {
            if (ward > 30) return $"SHARED_HOUSE_W{ward}_P{plot}";
            var tm = UIModule.Instance()->GetRaptureTextModule();
            var sp = tm->FormatAddonText2IntInt(8519, ward, plot);
            return Marshal.PtrToStringUTF8(new IntPtr(sp)) ?? $"SHARED_HOUSE_W{ward}_P{plot}";
        }

        private static void SetupEstateIds(out uint[] array) {
            var list = new List<uint>(10);
            var sheet = FindAnythingPlugin.Data.GetExcelSheet<Aetheryte>(ClientLanguage.English)!;
            foreach (var aetheryte in sheet) {
                if (aetheryte.PlaceName.RowId is 1145 or 1160)
                    list.Add(aetheryte.RowId);
            }
            array = list.ToArray();
        }

        private static void SetupAetherytes(IDictionary<uint, string> dict, ClientLanguage language) {
            var sheet = FindAnythingPlugin.Data.GetExcelSheet<Aetheryte>(language)!;
            dict.Clear();
            foreach (var row in sheet) {
                var name = row.PlaceName.ValueNullable?.Name.ToDalamudString().TextValue;
                if (string.IsNullOrEmpty(name))
                    continue;
                name = FindAnythingPlugin.PluginInterface.Sanitizer.Sanitize(name);
                dict[row.RowId] = name;
            }
        }

        private static void SetupMaps(IDictionary<uint, string> dict, ClientLanguage language) {
            var sheet = FindAnythingPlugin.Data.GetExcelSheet<Aetheryte>(language)!;
            dict.Clear();
            foreach (var row in sheet) {
                var name = row.Territory.ValueNullable?.PlaceName.ValueNullable?.Name.ToDalamudString().TextValue;
                if (string.IsNullOrEmpty(name))
                    continue;
                if (row is not { IsAetheryte: true }) continue;
                name = FindAnythingPlugin.PluginInterface.Sanitizer.Sanitize(name);
                dict[row.RowId] = name;
            }
        }
    }
}