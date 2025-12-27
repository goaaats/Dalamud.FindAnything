using Dalamud.Interface.Textures;
using System;
using System.Linq;

namespace Dalamud.FindAnything;

public static class HintProvider
{
    private static readonly Configuration.HintKind StartHintLevel = Enum.GetValues<Configuration.HintKind>().First();
    private static readonly Configuration.HintKind NoHintLevel = Enum.GetValues<Configuration.HintKind>().Last() + 1;

    public static HintResult? NextHint() {
        if (FindAnythingPlugin.Configuration.HintLevel == NoHintLevel)
            return null;

        var nextHint = FindAnythingPlugin.Configuration.HintLevel++;
        FindAnythingPlugin.ConfigManager.Save();

        Service.Log.Information($"Hint: {nextHint}");
        return new HintResult {
            HintLevel = nextHint,
        };
    }

    public static void ResetHints() {
        FindAnythingPlugin.Configuration.HintLevel = StartHintLevel;
        FindAnythingPlugin.ConfigManager.Save();
    }

    public class HintResult : ISearchResult
    {
        public string CatName => string.Empty;

        public string Name => HintLevel switch {
            Configuration.HintKind.HintTyping => "Just start typing to search!",
            Configuration.HintKind.HintEnter => "Press enter to select results!",
            Configuration.HintKind.HintUpDown => "Press the up and down buttons to scroll!",
            Configuration.HintKind.HintTeleport => "Search for aetherytes or zone names!",
            Configuration.HintKind.HintEmoteDuty => "Search for emotes or duties!",
            Configuration.HintKind.HintGameCmd => "Search for game commands, like timers!",
            Configuration.HintKind.HintChatCmd => "Run chat commands by typing them here!",
            Configuration.HintKind.HintMacroLink => "Link macros to search in \"wotsit settings\"!",
            Configuration.HintKind.HintGearset => "Search for names of gearsets, jobs or roles!",
            Configuration.HintKind.HintMath => "Type mathematical expressions into the search bar!",
            _ => throw new ArgumentOutOfRangeException($"Unknown HintKind: {HintLevel}"),
        };

        public ISharedImmediateTexture Icon => FindAnythingPlugin.TexCache.HintIcon;
        public int Score => 0;
        public required Configuration.HintKind HintLevel { get; init; }

        public object Key => HintLevel;
        public bool CloseFinder => false;

        public void Selected() {
            // ignored
        }
    }
}