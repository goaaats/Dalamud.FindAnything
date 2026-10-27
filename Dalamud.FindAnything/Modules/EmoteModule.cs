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
            var emoteEntry = FindAnythingPlugin.SearchDatabase.Emote[emote];
            var slashCmd = emote.TextCommand.Value;

            var score = matcher.MatchesAny(
                emoteEntry.Searchable,
                slashCmd.Command.ToText(),
                slashCmd.Alias.ToText(),
                slashCmd.ShortCommand.ToText(),
                slashCmd.ShortAlias.ToText()
            );

            if (score > 0) {
                ctx.AddResult(new EmoteSearchResult {
                    Score = score * Weight,
                    Name = emoteEntry.Display,
                    Emote = emote,
                });
            }

            if (ctx.OverLimit()) break;
        }
    }

    public class EmoteSearchResult : ISearchResult {
        public string CatName => field ??= FindAnythingPlugin.Configuration.ShowEmoteCommand
                ? $"Emote ({SlashCommand})"
                : "Emote";
        public required string Name { get; init; }
        public ISharedImmediateTexture Icon => field ??= Utils.GetIcon(Emote.Icon);
        public required int Score { get; init; }
        public required Emote Emote { get; init; }

        public object Key => Emote.RowId;

        public bool CloseFinder => FindAnythingPlugin.Configuration.EmoteMode != Configuration.EmoteMotionMode.Ask;

        public Configuration.EmoteMotionMode MotionMode { get; set; } = FindAnythingPlugin.Configuration.EmoteMode;

        private string? SlashCommand => Emote.TextCommand.ValueNullable?.Command.ToText();

        public void Selected() {
            if (MotionMode == Configuration.EmoteMotionMode.Ask) {
                EmoteModeLookup.SetBaseResult(this);
                FindAnythingPlugin.Instance.SwitchLookupType(LookupType.EmoteMode);
                return;
            }

            var cmd = SlashCommand;
            if (cmd is null || !cmd.StartsWith('/'))
                throw new Exception($"Emote {Emote.RowId} has missing or invalid slash command ({cmd})");

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
