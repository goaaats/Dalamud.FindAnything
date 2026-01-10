using Dalamud.FindAnything.Lookup;
using Dalamud.Game.ClientState.Aetherytes;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using LuminaMap = Lumina.Excel.Sheets.Map;
using TerritoryIntendedUseEnum = FFXIVClientStructs.FFXIV.Client.Enums.TerritoryIntendedUse;

namespace Dalamud.FindAnything.Modules;

public sealed class CoordinatesModule : SearchModule {
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.Coordinates;

    private readonly TerritoryDatabase territoryDatabase = new();

    public bool ShowAll => true;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        if (CoordUtils.Parse(ctx.Criteria.MatchString) is not { } parsed)
            return;

        var localMatcher = new FuzzyMatcher(parsed.Remainder, ctx.Criteria.MatchMode);
        var scoreMulti = ctx.Criteria.MatchMode == MatchMode.FuzzyParts ? 3 : 1;

        var results = new List<CoordinatesSearchResult>();

        // Check database
        foreach (var entry in territoryDatabase.Entries) {
            if (!CoordUtils.CanUseMapLink(entry.TerritoryType))
                continue;

            var score = localMatcher.Matches(entry.Searchable);
            if (score > 0) {
                results.Add(new CoordinatesSearchResult {
                    Score = score * scoreMulti * Weight,
                    Coords = Coordinates.FromInput(entry.TerritoryType, entry.TerritoryType.Map.Value, parsed.X, parsed.Y),
                });
            }
        }

        // Check current territory/map
        if (CoordUtils.GetCurrentTerritoryMap() is { TerritoryType: var territoryType, Map: var map }) {
            if (parsed.Remainder.Length != 0) {
                var score = localMatcher.Matches(CoordUtils.GetSearchableName(territoryType, map));
                if (score > 0) {
                    results.RemoveAll(r => r.Coords.TerritoryType.RowId == territoryType.RowId);
                    results.Add(new CoordinatesSearchResult {
                        Score = score * scoreMulti * 2 * Weight,
                        Coords = Coordinates.FromInput(territoryType, map, parsed.X, parsed.Y),
                    });
                }
            } else {
                results.Add(new CoordinatesSearchResult {
                    Score = int.MaxValue,
                    Coords = Coordinates.FromInput(territoryType, map, parsed.X, parsed.Y),
                });
            }
        }

        if (results.Count == 0)
            return;

        if (ShowAll) {
            ctx.AddResultRange(results);
        } else {
            ctx.AddResult(results.OrderByDescending(r => r.Score).First());
        }
    }

    public class CoordinatesSearchResult : ISearchResult {
        public string CatName => string.Empty;
        public string Name => $"\xE05E {Coords.MapName} (X: {Coords.X}, Y: {Coords.Y})";
        public ISharedImmediateTexture Icon => FindAnythingPlugin.TexCache.ChatIcon;
        public required int Score { get; init; }
        public required Coordinates Coords { get; init; }

        public object Key => Coords;
        public bool CloseFinder => false;

        public void Selected() {
            CoordinateActionLookup.SetBaseResult(this);
            FindAnythingPlugin.Instance.SwitchLookupType(LookupType.CoordinateAction);
        }

    }
}

public class TerritoryDatabase {
    public readonly Entry[] Entries = Load();

    private static Entry[] Load() {
        var list = new List<Entry>();

        var seenTerritoryPlaceNames = new HashSet<string>();
        foreach (var territoryType in Service.Data.GetExcelSheet<TerritoryType>()) {
            if (territoryType.Map.ValueNullable is not { } map || map.RowId == 0)
                continue;

            if (territoryType.TerritoryIntendedUse.ValueNullable is not { } intendedUse)
                continue;

            var useEnum = (TerritoryIntendedUseEnum)intendedUse.RowId;
            if (useEnum is TerritoryIntendedUseEnum.OpeningArea) // Has duplicates of Town entries but with different names
                continue;

            if (territoryType.PlaceName.ValueNullable?.Name.ToString() is not { } placeName)
                continue;

            if (!seenTerritoryPlaceNames.Add(placeName))
                continue;

            var name = CoordUtils.GetSearchableName(territoryType, map);
            list.Add(new Entry(name, territoryType));
        }

        return list.ToArray();
    }

    public record Entry(string Searchable, TerritoryType TerritoryType);
}

public static partial class CoordUtils {
    [GeneratedRegex(@"(\d+\.?\d*)")]
    private static partial Regex CoordsRegex();

    [GeneratedRegex(@"(?:^|(?<=\s))[0-9XYZxyz\W-[\s'-]]+(?:$|(?=\s))")]
    private static partial Regex IslandsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private const NumberStyles CoordStyle = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    public static ParseResult? Parse(string arg) {
        var coordinates = CoordsRegex().Matches(arg);
        if (coordinates is not [{ Value: var sx }, { Value: var sy }, ..]) {
            return null;
        }

        var remainder = IslandsRegex().Replace(arg, string.Empty);
        remainder = WhitespaceRegex().Replace(remainder, " ").Trim();

        return new ParseResult(
            decimal.Parse(sx, CoordStyle, CultureInfo.InvariantCulture),
            decimal.Parse(sy, CoordStyle, CultureInfo.InvariantCulture),
            remainder
        );
    }

    public record ParseResult(decimal X, decimal Y, string Remainder);

    public static void PlaceFlag(Coordinates coordinates) {
        var mapLinkPayload = coordinates.MapLinkPayload();

        Service.GameGui.OpenMapWithMapLink(mapLinkPayload);
        GetClosestAetheryte(mapLinkPayload);
    }

    public static void PrintToChat(Coordinates coordinate) {
        Service.ChatGui.Print(new XivChatEntry { Message = coordinate.SeStringLink() });
    }

    public static IAetheryteEntry? GetClosestAetheryte(MapLinkPayload mapLink) {
        var linkPosition = new Vector2(mapLink.XCoord, mapLink.YCoord);
        var candidateAetherytes = Service.Aetherytes
            .Where(e => e.AetheryteData is { ValueNullable: { IsAetheryte: true } aetheryte } && aetheryte.Map.RowId == mapLink.Map.RowId)
            .ToArray();

        var closestDistance = float.MaxValue;
        IAetheryteEntry? closestAetheryteEntry = null;

        foreach (var markers in Service.Data.GetSubrowExcelSheet<MapMarker>())
        foreach (var marker in markers) {
            if (marker.DataType != 3) continue;
            foreach (var aetheryte in candidateAetherytes) {
                if (aetheryte.AetheryteData.RowId == marker.DataKey.RowId) {
                    var markerPosition = new Vector2(MarkerToMap(marker.X, mapLink.Map.Value.SizeFactor), MarkerToMap(marker.Y, mapLink.Map.Value.SizeFactor));
                    var distance = Vector2.Distance(linkPosition, markerPosition);
                    Service.Log.Debug($"Found aetheryte {aetheryte.AetheryteData.Value.PlaceName.Value.Name} ({markerPosition.X}, {markerPosition.Y}) with distance {distance}");
                    if (distance < closestDistance) {
                        closestDistance = distance;
                        closestAetheryteEntry = aetheryte;
                    }
                }
            }
        }

        return closestAetheryteEntry;
    }

    private static float MarkerToMap(int pos, float scale) {
        var num = scale / 100f;
        var rawPosition = (int)((float)(pos - 1024.0) / num * 1000f);
        return RawToMap(rawPosition, scale);
    }

    private static float RawToMap(int pos, float scale) {
        var num = scale / 100f;
        return (float)((pos / 1000f * num + 1024.0) / 2048.0 * 41.0 / num + 1.0);
    }

    public static string GetSearchableName(TerritoryType territoryType, LuminaMap map) {
        return FindAnythingPlugin.Normalizer.Searchable(GetDisplayName(territoryType, map));
    }

    public static string GetDisplayName(TerritoryType territoryType, LuminaMap map) {
        var territoryName = territoryType.PlaceName.ValueNullable?.Name ?? "";
        var mapNameSe = map.PlaceName.ValueNullable?.Name ?? "";

        if (territoryName == mapNameSe)
            return territoryName.ToString();

        var mapNameNoArticleSe = map.PlaceName.ValueNullable?.NameNoArticle;
        if (territoryName == mapNameNoArticleSe)
            return territoryName.ToString();

        var placeName = territoryName.ToString();
        var mapName = mapNameSe.ToString();

        if (string.IsNullOrWhiteSpace(mapName))
            return territoryName.ToString();

        return $"{placeName} [{mapName}]";
    }

    public static bool CanUseMapLink(TerritoryType territoryType) {
        return CanUseMapLink(territoryType.RowId, territoryType.Map.RowId);
    }

    private static unsafe bool CanUseMapLink(uint territoryTypeId, uint mapId) {
        var agentMap = AgentMap.Instance();
        if (agentMap->CurrentMapId == mapId)
            return true;

        var overrideMapId = TerritoryInfo.Instance()->MapIdOverride;
        if (overrideMapId != 0 && overrideMapId == mapId)
            return true;

        if (Service.Data.Excel.GetSheet<LuminaMap>().GetRowOrDefault(mapId) is not { } map || map.RowId == 0)
            return false;

        if (map.PriorityUI != 0)
            return true;

        if (agentMap->CurrentTerritoryId == territoryTypeId)
            return true;

        return false;
    }

    public static unsafe TerritoryMap? GetCurrentTerritoryMap() {
        var agentMap = AgentMap.Instance();

        var currentTerritoryId = agentMap->CurrentTerritoryId;

        var currentMapId = TerritoryInfo.Instance()->MapIdOverride;
        if (currentMapId == 0)
            currentMapId = agentMap->CurrentMapId;

        if (currentTerritoryId == 0 || currentMapId == 0)
            return null;

        if (Service.Data.Excel.GetSheet<TerritoryType>().GetRowOrDefault(currentTerritoryId) is not { } territoryType)
            return null;

        if (Service.Data.Excel.GetSheet<LuminaMap>().GetRowOrDefault(currentMapId) is not { } map)
            return null;

        return new TerritoryMap(territoryType, map);
    }

    public record TerritoryMap(TerritoryType TerritoryType, LuminaMap Map);
}

public record Coordinates {
    public required TerritoryType TerritoryType { get; init; }
    public required LuminaMap Map { get; init; }
    public required decimal X { get; init; }
    public required decimal Y { get; init; }

    public static Coordinates FromInput(TerritoryType territoryType, LuminaMap map, decimal x, decimal y) {
        return new Coordinates {
            TerritoryType = territoryType,
            Map = map,
            X = x,
            Y = y,
        };
    }

    public ReadOnlySeString MapName =>
        // $"[{TerritoryType.RowId}/{Map.RowId}] <{(TerritoryType.Map.ValueNullable?.PriorityUI ?? 0) != 0}=>{CoordUtils.CanUseMapLink(TerritoryType)}> {TerritoryType.Name} {CoordUtils.GetDisplayName(TerritoryType)}";
        $"{CoordUtils.GetDisplayName(TerritoryType, Map)}";

    public MapLinkPayload MapLinkPayload() => new(TerritoryType.RowId, Map.RowId, (float)X, (float)Y);

    public SeString SeStringLink() => SeString.CreateMapLink(TerritoryType.RowId, Map.RowId, (float)X, (float)Y);
}
