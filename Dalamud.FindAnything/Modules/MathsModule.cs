using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using NCalc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Dalamud.FindAnything.Modules;

public sealed class MathsModule : SearchModule
{
    private object? lastAcceptedExpressionResult;

    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.Maths;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        CheckExpression(ctx);
        CheckOthers(ctx, matcher);
    }

    public MathsModule() {
        Expression.CacheEnabled = true;
        Task.Run(() => new Expression("1+1").Evaluate());  // Warm up evaluator, takes like 100ms
    }

    private void CheckExpression(SearchContext ctx) {
        var expression = new Expression(ctx.Criteria.CleanString);

        expression.EvaluateFunction += delegate(string name, FunctionArgs args) {
            switch (name) {
                case "lexp":
                    if (args.Parameters.Length == 1) {
                        var num = (int)args.EvaluateParameters()[0];
                        args.Result = MathAux.GetNeededExpForLevel((short)num);
                        args.HasResult = true;
                        // Serilog.Log.Information($"exp called with {num} was {args.Result}", []);
                    } else if (args.Parameters.Length == 0) {
                        args.Result = MathAux.GetNeededExpForCurrentLevel();
                        args.HasResult = true;
                    }

                    break;
                case "cexp":
                    if (args.Parameters.Length == 0) {
                        args.Result = MathAux.GetCurrentExp();
                        args.HasResult = true;
                    }

                    break;
                case "expleft":
                    if (args.Parameters.Length == 0) {
                        args.Result = MathAux.GetExpLeft();
                        args.HasResult = true;
                    }

                    break;
                case "lvl":
                    if (args.Parameters.Length == 0) {
                        args.Result = MathAux.GetLevel();
                        args.HasResult = true;
                    }

                    break;
                default:
                    args.Result = null;
                    args.HasResult = false;
                    break;
            }
        };

        expression.EvaluateParameter += delegate(string sender, ParameterArgs args) {
            if (sender == "ans") {
                args.Result = lastAcceptedExpressionResult ?? 0;
                args.HasResult = true;
            } else if (FindAnythingPlugin.Configuration.MathConstants.TryGetValue(sender, out var constant)) {
                args.Result = constant;
                args.HasResult = true;
            } else {
                args.Result = null;
                args.HasResult = false;
            }
        };

        if (!expression.HasErrors()) {
            try {
                var result = expression.Evaluate();
                if (result is not 0)
                    ctx.AddResult(ExpressionResult.Value(this, result));
            } catch (ArgumentException ex) {
                // Serilog.Log.Verbose(ex, "Expression evaluate error", []);
                if (ctx.Criteria.CleanString.Any(x => x is >= '0' and <= '9'))
                    ctx.AddResult(ExpressionResult.Error());
            }
        } else {
            // Serilog.Log.Verbose("Expression parse error: " + expression.Error, []);
            if (ctx.Criteria.CleanString.Any(x => x is >= '0' and <= '9'))
                ctx.AddResult(ExpressionResult.Error());
        }
    }

    private void CheckOthers(SearchContext ctx, FuzzyMatcher matcher) {
        var score = matcher.Matches("dn farm");
        if (score > 0) {
            ctx.AddResult(new GameSearchResult { Score = score * DefaultWeight });
        }
    }

    private class ExpressionResult : ISearchResult
    {
        public string CatName => string.Empty;
        public string Name => !HasError ? $" = {Result}" : " = ERROR";
        public ISharedImmediateTexture Icon => FindAnythingPlugin.TexCache.MathsIcon;
        public int Score => 0;
        public required MathsModule? Module { get; init; }
        public required object? Result { get; init; }
        public required bool HasError { get; init; }

        public static ExpressionResult Value(MathsModule module, object result) {
            return new ExpressionResult { Module = module, Result = result, HasError = false };
        }

        public static ExpressionResult Error() {
            return new ExpressionResult { Module = null, Result = null, HasError = true };
        }

        public object Key => Name;

        public void Selected() {
            if (this is { Module: { } module, Result: { } result }) {
                module.lastAcceptedExpressionResult = result;
                ImGui.SetClipboardText(result.ToString());
            }
        }
    }

    private class GameSearchResult : ISearchResult
    {
        public string CatName => string.Empty;
        public string Name => "DN Farm";
        public ISharedImmediateTexture Icon => FindAnythingPlugin.TexCache.GameIcon;
        public required int Score { get; init; }

        public object Key => true;

        public void Selected() {
            FindAnythingPlugin.Instance.OpenGame();
        }
    }
}

public static class MathAux
{
    public static int GetNeededExpForLevel(short level) {
        var paramGrow = Service.Data.GetExcelSheet<ParamGrow>();
        if (paramGrow.Count < level) {
            return 0;
        }

        return paramGrow.GetRow((uint)level).ExpToNext;
    }

    public static int GetNeededExpForCurrentLevel() {
        if (!Service.PlayerState.IsLoaded)
            return 0;

        if (!Service.PlayerState.IsLoaded)
            return 0;

        return GetNeededExpForLevel(Service.PlayerState.Level);
    }

    public static unsafe int GetCurrentExp() {
        if (!Service.PlayerState.IsLoaded)
            return 0;

        if (Service.PlayerState.ClassJob.ValueNullable is not { } classJob)
            return 0;

        return UIState.Instance()->PlayerState.ClassJobExperience[classJob.ExpArrayIndex];
    }

    public static int GetExpLeft() {
        if (!Service.PlayerState.IsLoaded)
            return 0;

        return GetNeededExpForCurrentLevel() - GetCurrentExp();
    }

    public static int GetLevel() {
        if (!Service.PlayerState.IsLoaded)
            return 0;

        return Service.PlayerState.Level;
    }
}
