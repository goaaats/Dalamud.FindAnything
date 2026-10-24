using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;

namespace Dalamud.FindAnything;

public struct SearchEntry {
    public string Searchable;
    public string Display;
}

public class SearchDatabaseLoader(Normalizer normalizer) {
    private readonly Dictionary<Type, FrozenDictionary<uint, SearchEntry>> loaderData = [];

    private void Load<T>(Func<T, ReadOnlySeString?> extractor) where T : struct, IExcelRow<T> {
        var data = new Dictionary<uint, SearchEntry>();
        foreach (var row in Service.Data.GetExcelSheet<T>()) {
            if (extractor(row) is not { } ross || ross.IsEmpty)
                continue;

            var text = ross.ToText();
            data.Add(row.RowId, new SearchEntry {
                Display = text,
                Searchable = normalizer.Searchable(text),
            });
        }
        loaderData.Add(typeof(T), data.ToFrozenDictionary());
    }

    public void LoadBase() {
        Load<ContentFinderCondition>(r => r.Name);
        Load<ContentRoulette>(r => r.Name);
        Load<TerritoryType>(r => r.PlaceName.ValueNullable?.Name);
        Load<ExtraCommand>(r => r.Name);
        Load<GeneralAction>(r => r.Name);
        Load<Emote>(r => r.Name);
        Load<Quest>(r => r.Name);
        Load<Item>(r => r.Name);
        Load<Recipe>(r => {
            if (r.ItemResult.ValueNullable is { RowId: not 0 } itemResult)
                return itemResult.Name;
            return null;

        });
        Load<GatheringItem>(r => {
            if (r.Item.GetValueOrDefault<Item>() is { RowId: not 0 } item)
                return item.Name;
            return null;
        });
        Load<Mount>(r => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(r.Singular.ToText()));
    }

    public FrozenDictionary<Type, FrozenDictionary<uint, SearchEntry>> LoadMainThread() {
        Load<MainCommand>(r => {
            if (r.Icon == 0)
                return null;
            return Service.SeStringEvaluator.EvaluateActStr(ActionKind.MainCommand, r.RowId, Service.ClientState.ClientLanguage);
        });
        Load<Companion>(r => Service.SeStringEvaluator.EvaluateObjStr(ObjectKind.Companion, r.RowId, Service.ClientState.ClientLanguage));

        return loaderData.ToFrozenDictionary();
    }
}

public class SearchDatabase(Normalizer normalizer) {
    private FrozenDictionary<Type, FrozenDictionary<uint, SearchEntry>> SearchData { get; set; } =
        FrozenDictionary<Type, FrozenDictionary<uint, SearchEntry>>.Empty;

    private SearchDatabaseLoader? loader;

    public void InitBase() {
        loader = new SearchDatabaseLoader(normalizer);
        loader.LoadBase();
    }

    public void InitMainThread() {
        if (!ThreadSafety.IsMainThread) {
            Service.Log.Error($"{nameof(SearchDatabase)}.{nameof(InitMainThread)} called from non-main thread");
            return;
        }

        if (loader is null) {
            Service.Log.Error($"{nameof(loader)} was missing when calling {nameof(InitMainThread)}");
            return;
        }

        SearchData = loader.LoadMainThread();
        loader = null;
    }

    public SearchEntry Get<T>(uint row) where T : struct, IExcelRow<T> => SearchData[typeof(T)][row];

    public FrozenDictionary<uint, SearchEntry> GetAll<T>() where T : struct, IExcelRow<T> => SearchData[typeof(T)];
}
