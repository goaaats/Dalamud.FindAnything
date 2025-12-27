using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Dalamud.FindAnything;

public class SearchDatabase
{
    public struct SearchEntry
    {
        public string Searchable;
        public string Display;
    }

    private FrozenDictionary<Type, FrozenDictionary<uint, SearchEntry>> SearchData { get; }
    private readonly Normalizer normalizer;

    private SearchDatabase(Normalizer normalizer) {
        this.normalizer = normalizer;

        var data = new Dictionary<Type, FrozenDictionary<uint, SearchEntry>>();
        InitData<ContentFinderCondition>(ref data, r => r.Name);
        InitData<ContentRoulette>(ref data, r => r.Name);
        InitData<TerritoryType>(ref data, r => r.PlaceName.ValueNullable?.Name);
        InitData<Aetheryte>(ref data, r => r.PlaceName.ValueNullable?.Name);
        InitData<MainCommand>(ref data, r => {
            if (r.Icon == 0)
                return null;
            return r.Name;
        });
        InitData<ExtraCommand>(ref data, r => r.Name);
        InitData<GeneralAction>(ref data, r => r.Name);
        InitData<Emote>(ref data, r => r.Name);
        InitData<Quest>(ref data, r => r.Name);
        InitData<Item>(ref data, r => r.Name);
        InitData<Recipe>(ref data, r => {
            if (r.ItemResult.ValueNullable is not { } itemResult || itemResult.RowId == 0)
                return null;
            return itemResult.Name;
        });

        var itemSheet = Service.Data.GetExcelSheet<Item>();
        InitData<GatheringItem>(ref data, r => {
            if (itemSheet.GetRowOrDefault(r.Item.RowId) is not { } itemResult || itemResult.RowId == 0)
                return null;
            return itemResult.Name;
        });

        SearchData = data.ToFrozenDictionary();
    }

    private void InitData<T>(ref Dictionary<Type, FrozenDictionary<uint, SearchEntry>> searchDb, Func<T, ReadOnlySeString?> rowToFind) where T : struct, IExcelRow<T> {
        var data = new Dictionary<uint, SearchEntry>();
        foreach (var excelRow in Service.Data.GetExcelSheet<T>()) {
            if (rowToFind.Invoke(excelRow) is { } result) {
                var textVal = result.ToText();
                if (textVal.IsNullOrEmpty())
                    continue;

                try {
                    if (excelRow is MainCommand && textVal.StartsWith(" ")) {
                        var macroText = result.ToMacroString(); // Using ToMacroString on purpose to grab macro details below
                        if (macroText.StartsWith("<if([gnum75>0")) {
                            textVal = macroText.Split(")")[0].Split(",")[2] + textVal;
                        }
                        // NOTE: Above can be replaced with below, but then it needs to be on the main thread...
                        // textVal = Service.SeStringEvaluator.EvaluateActStr(ActionKind.MainCommand, excelRow.RowId, Service.ClientState.ClientLanguage);
                    }
                } catch (Exception e) {
                    Service.Log.Warning(e, "Failed to parse (assumed) Controller/Gamepad Settings main command");
                }

                data.Add(excelRow.RowId, new SearchEntry {
                    Display = textVal,
                    Searchable = normalizer.Searchable(textVal),
                });
            }
        }

        searchDb.Add(typeof(T), data.ToFrozenDictionary());
    }

    public static string GetSearchableText(string input, bool normalizeKana) => input.Downcase(normalizeKana).Replace("'", string.Empty);

    public SearchEntry GetString<T>(uint row) where T : struct, IExcelRow<T> => SearchData[typeof(T)][row];

    public IReadOnlyDictionary<uint, SearchEntry> GetAll<T>() where T : struct, IExcelRow<T> => SearchData[typeof(T)];

    public static SearchDatabase Load(Normalizer normalizer) => new(normalizer);
}