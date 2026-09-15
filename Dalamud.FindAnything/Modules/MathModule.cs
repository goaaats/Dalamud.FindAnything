using Dalamud.Interface.Textures;
using NCalc;
using System.Globalization;
using System.Linq;

namespace Dalamud.FindAnything.Modules;

public sealed class MathModule : SearchModule {
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.Math;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        var expression = ctx.Criteria.CleanString;

        // Ignore searches without digits; they cannot be mathematical expressions.
        if (expression.Length < 3 || !expression.Any(char.IsDigit)) {
            return;
        }

        var evaluator = new Expression(expression);

        // Make the constants from the "Math constants" settings available inside the expression.
        foreach (var constant in FindAnythingPlugin.Configuration.Constants) {
            evaluator.Parameters.Add(constant.Key, constant.Value);
        }

        object? result;
        try {
            result = evaluator.Evaluate();
        } catch {
            return;
        }

        var formatted = FormatResult(result);
        if (formatted is not { } resultText) {
            return;
        }

        ctx.AddResult(new MathSearchResult {
            Score = int.MaxValue,
            Expression = expression,
            Result = resultText,
        });
    }

    private static string? FormatResult(object? value) {
        // "N0" with the invariant culture gives comma thousands separators
        // (e.g. 1234567 is displayed as "1,234,567") regardless of the user's locale.
        return value switch {
            decimal d => d.ToString("N0", CultureInfo.InvariantCulture),
            double dbl => dbl.ToString("N0", CultureInfo.InvariantCulture),
            float f => f.ToString("N0", CultureInfo.InvariantCulture),
            long l => l.ToString("N0", CultureInfo.InvariantCulture),
            int i => i.ToString("N0", CultureInfo.InvariantCulture),
            _ => null,
        };
    }

    private class MathSearchResult : ISearchResult {
        public string CatName => "Math";
        public string Name => $"{Expression} = {Result}";
        public ISharedImmediateTexture Icon => FindAnythingPlugin.TexCache.MathIcon;
        public required int Score { get; init; }
        public required string Expression { get; init; }
        public required string Result { get; init; }

        public object Key => Expression;

        public void Selected() {
            Service.ChatGui.Print($"{Expression} = {Result}");
        }
    }
}
