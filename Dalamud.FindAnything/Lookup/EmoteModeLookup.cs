using Dalamud.FindAnything.Modules;
using Dalamud.Interface.Textures;
using System;
using System.Linq;

namespace Dalamud.FindAnything.Lookup;

public class EmoteModeLookup : ILookup {
    private static ISearchResult? _baseResult;

    public static void SetBaseResult(ISearchResult result) {
        _baseResult = result;
    }

    public string GetPlaceholder() {
        if (_baseResult is EmoteModule.EmoteSearchResult emoteRes) {
            return $"Choose emote mode for \"{emoteRes.Name}\"...";
        }

        return "";
    }

    public LookupResult Lookup(SearchCriteria criteria) {
        return LookupResult.Bag(
            Enum.GetValues<EmoteModule.EmoteModeChoice>()
                .Select(choice => new EmoteModeChoicerResult { Score = 1, Choice = choice })
                .Cast<ISearchResult>()
                .ToList()
        );
    }

    private class EmoteModeChoicerResult : ISearchResult {
        public string CatName => string.Empty;

        public string Name => Choice switch {
            EmoteModule.EmoteModeChoice.Default => "Default Choice",
            EmoteModule.EmoteModeChoice.MotionOnly => "Only Motion",
            _ => throw new ArgumentOutOfRangeException(),
        };

        public ISharedImmediateTexture Icon => FindAnythingPlugin.TexCache.EmoteIcon;
        public required int Score { get; init; }
        public required EmoteModule.EmoteModeChoice Choice { get; init; }

        public object Key => Choice.ToString();

        public void Selected() {
            if (_baseResult is not EmoteModule.EmoteSearchResult emoteRes) {
                Service.Log.Error($"{nameof(_baseResult)} was not of type {nameof(EmoteModule.EmoteSearchResult)}, was: {_baseResult?.GetType().FullName}");
                return;
            }

            emoteRes.MotionMode = Choice switch {
                EmoteModule.EmoteModeChoice.Default => Configuration.EmoteMotionMode.Default,
                EmoteModule.EmoteModeChoice.MotionOnly => Configuration.EmoteMotionMode.AlwaysMotion,
                _ => throw new ArgumentOutOfRangeException($"Unknown EmoteModeChoice: {Choice}"),
            };

            emoteRes.Selected();
        }
    }
}
