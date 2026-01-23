using Dalamud.Interface.Textures;
using System;

namespace Dalamud.FindAnything.Modules;

public sealed class ChatCommandModule : SearchModule {
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.None;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        if (!gameState.IsInCombatDuty() && ctx.Criteria.CleanString.StartsWith('/')) {
            ctx.AddResult(new ChatCommandSearchResult {
                Score = int.MaxValue,
                Command = ctx.Criteria.CleanString,
            });
        }
    }

    private class ChatCommandSearchResult : ISearchResult {
        public string CatName => string.Empty;
        public string Name => $"Run chat command \"{Command}\"";
        public ISharedImmediateTexture Icon => FindAnythingPlugin.TexCache.ChatIcon;
        public required int Score { get; init; }
        public required string Command { get; init; }

        public object Key => Command;

        public void Selected() {
            if (!Command.StartsWith('/'))
                throw new Exception("Command in ChatCommandSearchResult didn't start with slash!");

            Chat.ExecuteCommand(Command);
        }
    }
}
