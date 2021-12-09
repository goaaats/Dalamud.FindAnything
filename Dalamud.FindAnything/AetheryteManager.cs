using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Lumina.Excel.GeneratedSheets;
using SamplePlugin;

namespace TeleporterPlugin.Managers {
    public static class AetheryteManager {
        public static readonly Dictionary<uint, string> AetheryteNames = new(150);
        public static readonly Dictionary<uint, string> TerritoryNames = new(80);
        private static readonly Dictionary<(int, int), string> m_HouseNames = new(5);
        private static string? m_AppartmentName;

        public static readonly List<TeleportInfo> AvailableAetherytes = new(80);

        private static uint[] m_EstateIds = { 0 };

        public static void Load()
        {
            var lang = ClientLanguage.English;
            SetupAetherytes(AetheryteNames, lang);
            SetupMaps(TerritoryNames, lang);
            SetupEstateIds(out m_EstateIds);
        }

        public static bool IsHousingAetheryte(uint id, byte plot, byte ward, byte subId) {
            if (plot != 0 || ward != 0 || subId != 0)
                return true;
            return m_EstateIds.Contains(id);
        }

        public static bool TryFindAetheryteByMapName(string mapName, bool matchPartial, out TeleportInfo info) {
            UpdateAvailableAetherytes();
            info = new TeleportInfo();
            foreach (var (aetheryteId, territoryName) in TerritoryNames) {
                var result = matchPartial && territoryName.Contains(mapName, StringComparison.OrdinalIgnoreCase);
                if (!result && !territoryName.Equals(mapName, StringComparison.OrdinalIgnoreCase))
                    continue;
                foreach (var aetheryte in AvailableAetherytes) {
                    if (aetheryte.AetheryteId != aetheryteId)
                        continue;
                    info = aetheryte;
                    return true;
                }
            }
            return false;
        }

        public static bool TryFindAetheryteByName(string name, bool matchPartial, out TeleportInfo info) {
            UpdateAvailableAetherytes();
            info = new TeleportInfo();
            foreach (var tpInfo in AvailableAetherytes) {
                var aetheryteName = GetAetheryteName(tpInfo);

                var result = matchPartial && aetheryteName.Contains(name, StringComparison.OrdinalIgnoreCase);
                if(!result && !aetheryteName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    continue;
                info = tpInfo;
                return true;
            }
            return false;
        }

        public static unsafe bool UpdateAvailableAetherytes() {
            if (Plugin.ClientState.LocalPlayer == null)
                return false;
            try {
                var tp = Telepo.Instance();
                if (tp->UpdateAetheryteList() == null) 
                    return false;
                AvailableAetherytes.Clear();
                for (ulong i = 0; i < tp->TeleportList.Size(); i++)
                    AvailableAetherytes.Add(tp->TeleportList.Get(i));
                return true;
            } catch(Exception ex) {
                AvailableAetherytes.Clear();
                PluginLog.LogError(ex, "Error while Updating the Aetheryte List");
            }
            return false;
        }

        public static string GetAetheryteName(TeleportInfo info) {
            if (info.IsAppartment)
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
            var tm = Framework.Instance()->GetUiModule()->GetRaptureTextModule();
            var sp = tm->GetAddonText(8518);
            var name = Marshal.PtrToStringUTF8(new IntPtr(sp)) ?? string.Empty;
            return Plugin.PluginInterface.Sanitizer.Sanitize(name);
        }

        private static unsafe string GetSharedHouseName(int ward, int plot) {
            if (ward > 30) return $"SHARED_HOUSE_W{ward}_P{plot}";
            var tm = Framework.Instance()->GetUiModule()->GetRaptureTextModule();
            var sp = tm->FormatAddonText2(8519, ward, plot);
            return Marshal.PtrToStringUTF8(new IntPtr(sp)) ?? $"SHARED_HOUSE_W{ward}_P{plot}";
        }

        private static void SetupEstateIds(out uint[] array) {
            var list = new List<uint>(10);
            var sheet = Plugin.Data.GetExcelSheet<Aetheryte>(ClientLanguage.English)!;
            foreach (var aetheryte in sheet) {
                if (aetheryte.PlaceName.Row is 1145 or 1160)
                    list.Add(aetheryte.RowId);
            }
            array = list.ToArray();
        }

        private static void SetupAetherytes(IDictionary<uint, string> dict, ClientLanguage language) {
            var sheet = Plugin.Data.GetExcelSheet<Aetheryte>(language)!;
            dict.Clear();
            foreach (var row in sheet) {
                var name = row.PlaceName.Value?.Name?.ToString();
                if (string.IsNullOrEmpty(name))
                    continue;
                name = Plugin.PluginInterface.Sanitizer.Sanitize(name);
                dict[row.RowId] = name;
            }
        }

        private static void SetupMaps(IDictionary<uint, string> dict, ClientLanguage language) {
            var sheet = Plugin.Data.GetExcelSheet<Aetheryte>(language)!;
            dict.Clear();
            foreach (var row in sheet) {
                var name = row.Territory.Value?.PlaceName.Value?.Name?.ToString();
                if (string.IsNullOrEmpty(name))
                    continue;
                if (row is not { IsAetheryte: true }) continue;
                name = Plugin.PluginInterface.Sanitizer.Sanitize(name);
                dict[row.RowId] = name;
            }
        }
    }
}