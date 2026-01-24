using Dalamud.FindAnything.Modules;
using Dalamud.Game.ClientState.Aetherytes;
using Dalamud.Interface.Textures;

namespace Dalamud.FindAnything.Lookup;

public class CoordinateActionLookup : ILookup {
    private static CoordinatesModule.CoordinatesSearchResult? _baseResult;

    public static void SetBaseResult(CoordinatesModule.CoordinatesSearchResult result) {
        _baseResult = result;
    }

    public string GetPlaceholder() {
        if (_baseResult is { } result) {
            return $"Choose action for \"{result.Name}\"...";
        }

        return "";
    }

    public LookupResult Lookup(SearchCriteria criteria) {
        var ctx = new SearchContext(criteria);

        if (_baseResult is not { } baseResult) {
            return LookupResult.Empty;
        }

        ctx.AddResult(new CoordinateActionResult {
            Score = 1,
            BaseResult = baseResult,
            TeleportDestination = null,
        });

        if (CoordUtils.GetClosestAetheryte(baseResult.Coords.MapLinkPayload()) is { } closestAetheryte) {
            ctx.AddResult(new CoordinateActionResult {
                Score = 1,
                BaseResult = baseResult,
                TeleportDestination = closestAetheryte,
            });
        }

        return LookupResult.Bag(ctx.Results);
    }

    private class CoordinateActionResult : ISearchResult {
        public string CatName => string.Empty;
        public string Name => GetActionName();

        public ISharedImmediateTexture Icon => TeleportDestination is not null
            ? FindAnythingPlugin.TexCache.GetIcon(60453)
            : FindAnythingPlugin.TexCache.GetIcon(60561);

        public required int Score { get; init; }

        public object Key => (BaseResult.Key, TeleportDestination is not null);

        public required CoordinatesModule.CoordinatesSearchResult BaseResult { get; init; }
        public required IAetheryteEntry? TeleportDestination { get; init; }

        private string GetActionName() {
            if (TeleportDestination is { } dest) {
                return $"Place flag and teleport to {dest.AetheryteData.ValueNullable?.PlaceName.ValueNullable?.Name ?? "nearest aetheryte"}";
            }
            return "Place flag";
        }

        public void Selected() {
            CoordUtils.PlaceFlag(BaseResult.Coords);
            CoordUtils.PrintToChat(BaseResult.Coords);
            if (TeleportDestination != null)
                FindAnythingPlugin.AetheryteManager.Teleport(TeleportDestination);
        }
    }
}
