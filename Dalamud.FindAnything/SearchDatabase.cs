using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Dalamud.FindAnything;

public record struct SearchEntry(string Display, string Searchable);

public record struct MultiSearchEntry(string Display, string[] Searchables);

public sealed class SearchDatabase {
    public SearchTable<ContentFinderCondition> ContentFinderCondition { get; }
    public MultiSearchTable<ContentRoulette> ContentRoulette { get; }
    public SearchTable<TerritoryType> TerritoryType { get; }
    public SearchTable<ExtraCommand> ExtraCommand { get; }
    public SearchTable<GeneralAction> GeneralAction { get; }
    public SearchTable<Emote> Emote { get; }
    public SearchTable<Quest> Quest { get; }
    public SearchTable<Item> Item { get; }
    public SearchTable<Item> CraftableItem { get; }
    public SearchTable<Recipe> Recipe { get; }
    public SearchTable<GatheringItem> GatheringItem { get; }

    public SearchTable<MainCommand> MainCommand { get; private set; } = SearchTable<MainCommand>.Empty;
    public SearchTable<Companion> Companion { get; private set; } = SearchTable<Companion>.Empty;
    public SearchTable<Mount> Mount { get; private set; } = SearchTable<Mount>.Empty;

    public SearchDatabase() {
        var normalizer = FindAnythingPlugin.Normalizer;

        ContentFinderCondition = Sheet<ContentFinderCondition>()
            .Display(r => r.Name);

        ContentRoulette = Sheet<ContentRoulette>()
            .Where(r => r.IsInDutyFinder)
            .Display(r => new MultiSearchEntry(
                r.Name.ToText(),
                [normalizer.Searchable(r.Category), normalizer.Searchable(r.Name)]
            ));

        TerritoryType = Sheet<TerritoryType>()
            .Display(r => r.PlaceName.ValueNullable?.Name);

        ExtraCommand = Sheet<ExtraCommand>()
            .Display(r => r.Name);

        GeneralAction = Sheet<GeneralAction>()
            .Display(r => r.Name);

        Emote = Sheet<Emote>()
            .Display(r => r.Name);

        Quest = Sheet<Quest>()
            .Display(r => r.Name);

        Item = Sheet<Item>()
            .Display(r => r.Name);

        CraftableItem = Sheet<Recipe>()
            .Where(r => r.ItemResult.RowId != 0)
            .SelectNotNull(r => r.ItemResult.ValueNullable)
            .DistinctBy(r => r.RowId)
            .Display(r => r.Name);

        Recipe = Sheet<Recipe>()
            .Where(r => r.ItemResult.RowId != 0)
            .Display(r => r.ItemResult.ValueNullable?.Name);

        GatheringItem = Sheet<GatheringItem>()
            .Where(r => r.Item.RowId != 0)
            .Display(r => r.Item.GetValueOrDefault<Item>()?.Name);
    }

    public void InitMainThread() {
        if (!ThreadSafety.IsMainThread) {
            Service.Log.Error($"{nameof(SearchDatabase)}.{nameof(InitMainThread)} called from non-main thread");
            return;
        }

        var lang = Service.ClientState.ClientLanguage;

        MainCommand = Sheet<MainCommand>()
            .Where(r => r.Icon != 0)
            .Display(r => Service.SeStringEvaluator.EvaluateActStr(ActionKind.MainCommand, r.RowId, lang));

        Companion = Sheet<Companion>()
            .Display(r => Service.SeStringEvaluator.EvaluateObjStr(ObjectKind.Companion, r.RowId, lang));

        Mount = Sheet<Mount>()
            .Display(r => Service.SeStringEvaluator.EvaluateActStr(ActionKind.Mount, r.RowId, lang));
    }

    private static ExcelSheet<T> Sheet<T>() where T : struct, IExcelRow<T> =>
        Service.Data.GetExcelSheet<T>();
}

public sealed class SearchTable<T> where T : struct, IExcelRow<T> {
    public static readonly SearchTable<T> Empty = new(FrozenDictionary<uint, SearchEntry>.Empty);

    private readonly FrozenDictionary<uint, SearchEntry> entries;

    internal SearchTable(FrozenDictionary<uint, SearchEntry> entries) => this.entries = entries;

    public SearchEntry this[uint rowId] => entries[rowId];
    public SearchEntry this[T row] => entries[row.RowId];

    public FrozenDictionary<uint, SearchEntry>.Enumerator GetEnumerator() => entries.GetEnumerator();
}

public sealed class MultiSearchTable<T> where T : struct, IExcelRow<T> {
    public static readonly MultiSearchTable<T> Empty = new(FrozenDictionary<uint, MultiSearchEntry>.Empty);

    private readonly FrozenDictionary<uint, MultiSearchEntry> entries;

    internal MultiSearchTable(FrozenDictionary<uint, MultiSearchEntry> entries) => this.entries = entries;

    public MultiSearchEntry this[uint rowId] => entries[rowId];
    public MultiSearchEntry this[T row] => entries[row.RowId];

    public FrozenDictionary<uint, MultiSearchEntry>.Enumerator GetEnumerator() => entries.GetEnumerator();
}

public static class SearchTableExtensions {
    private static SearchEntry CreateEntry(string text) =>
        new(text, FindAnythingPlugin.Normalizer.Searchable(text));

    extension<T>(IEnumerable<T> rows) where T : struct, IExcelRow<T> {
        public SearchTable<T> Display(Func<T, ReadOnlySeString?> display) {
            return rows
                .Display(r => display(r) is { IsEmpty: false } ross ? CreateEntry(ross.ToText()) : null);
        }

        public SearchTable<T> Display(Func<T, string?> display) {
            return rows
                .Display(r => display(r) is { Length: not 0 } str ? CreateEntry(str) : null);
        }

        public SearchTable<T> Display(Func<T, SearchEntry?> entry) {
            var data = new Dictionary<uint, SearchEntry>();
            foreach (var row in rows) {
                if (entry(row) is { } e)
                    data.Add(row.RowId, e);
            }
            return new SearchTable<T>(data.ToFrozenDictionary());
        }

        public MultiSearchTable<T> Display(Func<T, MultiSearchEntry?> entry) {
            var data = new Dictionary<uint, MultiSearchEntry>();
            foreach (var row in rows) {
                if (entry(row) is { } e)
                    data.Add(row.RowId, e);
            }
            return new MultiSearchTable<T>(data.ToFrozenDictionary());
        }
    }
}
