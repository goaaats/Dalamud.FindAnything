using Dalamud.FindAnything.Lookup;
using Dalamud.Interface.Textures;
using Lumina.Excel.Sheets;
using System;

namespace Dalamud.FindAnything.Modules;

public sealed class EmoteModule : SearchModule {
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.Emote;

  public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        if (gameState.IsInEvent()) return;

        foreach (var emote in FindAnythingPlugin.GameStateCache.UnlockedEmotes) {
            var entry = FindAnythingPlugin.SearchDatabase.Get<Emote>(emote.RowId);
            var slashCmd = emote.TextCommand.Value;

            var score = matcher.MatchesAny(
                entry.Searchable,
                slashCmd.Command.ToText(),
                slashCmd.Alias.ToText(),
                slashCmd.ShortCommand.ToText(),
                slashCmd.ShortAlias.ToText()
            );

            if (score > 0) {
                ctx.AddResult(new EmoteSearchResult {
                    Score = score * Weight,
                    Name = entry.Display,
                    SlashCommand = slashCmd.Command.ToText(),
                    Icon = FindAnythingPlugin.TexCache.GetIcon(emote.Icon)
                });
            }

            if (ctx.OverLimit()) break;
        }
    }

    public class EmoteSearchResult : ISearchResult {
        public string CatName {
            get {
                var cat = "Emote";
                if (FindAnythingPlugin.Configuration.ShowEmoteCommand)
                    cat += $" ({SlashCommand})";

                return cat;
            }
        }

        public required string Name { get; init; }
        public required ISharedImmediateTexture? Icon { get; init; }
        public required int Score { get; init; }
        public required string SlashCommand { get; init; }

        public object Key => SlashCommand;

        public bool CloseFinder => FindAnythingPlugin.Configuration.EmoteMode != Configuration.EmoteMotionMode.Ask;

        public Configuration.EmoteMotionMode MotionMode { get; set; } = FindAnythingPlugin.Configuration.EmoteMode;

        public void Selected() {
            if (MotionMode == Configuration.EmoteMotionMode.Ask) {
                EmoteModeLookup.SetBaseResult(this);
                FindAnythingPlugin.Instance.SwitchLookupType(LookupType.EmoteMode);
                return;
            }

            var cmd = SlashCommand;
            if (!cmd.StartsWith('/'))
                throw new Exception($"SlashCommand prop does not actually start with a slash: {SlashCommand}");

            if (MotionMode == Configuration.EmoteMotionMode.AlwaysMotion)
                cmd += " motion";

            Chat.ExecuteCommand(cmd);
        }
    }

    public enum EmoteModeChoice {
        Default,
        MotionOnly,
    }
}
