using System;
using System.Collections.Generic;
using Dalamud.Game;
using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace Dalamud.FindAnything
{
    internal class SearchDatabase
    {
        public struct SearchEntry
        {
            public string Searchable;
            public string Display;
        }

        private IReadOnlyDictionary<Type, IReadOnlyDictionary<uint, SearchEntry>> SearchData { get; init; }
        private bool NormalizeKana { get; }

        private SearchDatabase(ClientLanguage lang)
        {
            NormalizeKana = lang == ClientLanguage.Japanese;
            
            var data = new Dictionary<Type, IReadOnlyDictionary<uint, SearchEntry>>();
            InitData<ContentFinderCondition>(ref data, (r) => r.Name);
            InitData<ContentRoulette>(ref data, (r) => r.Name);
            InitData<TerritoryType>(ref data, (r) => r.PlaceName.ValueNullable?.Name);
            InitData<Aetheryte>(ref data, (r) => r.PlaceName.ValueNullable?.Name);
            InitData<MainCommand>(ref data, (r) => r.Name);
            InitData<GeneralAction>(ref data, (r) => r.Name);
            InitData<Emote>(ref data, (r) => r.Name);
            InitData<Quest>(ref data, (r) => r.Name);
            InitData<Item>(ref data, (r) => r.Name);
            InitData<Recipe>(ref data, r =>
            {
                var itemResult = r.ItemResult.ValueNullable;

                if (itemResult == null || itemResult.Value.RowId == 0) {
                    return null;
                }

                return itemResult.Value.Name;
            });

            var item = FindAnythingPlugin.Data.GetExcelSheet<Item>()!;

            InitData<GatheringItem>(ref data, r =>
            {
                var itemResult = item.GetRowOrDefault(r.Item.RowId);

                if (itemResult == null || itemResult.Value.RowId == 0) {
                    return null;
                }

                return itemResult.Value.Name;
            });

            SearchData = data;
        }

        private void InitData<T>(ref Dictionary<Type, IReadOnlyDictionary<uint, SearchEntry>> searchDb, Func<T, ReadOnlySeString?> rowToFind) where T : struct, IExcelRow<T>
        {
            var normalizeKana = NormalizeKana;
            var data = new Dictionary<uint, SearchEntry>();
            foreach (var excelRow in FindAnythingPlugin.Data.GetExcelSheet<T>())
            {
                if (rowToFind.Invoke(excelRow) is { } result)
                {
                    var textVal = result.ExtractText();
                    if (textVal.IsNullOrEmpty())
                        continue;

                    try {
                        if (excelRow is MainCommand && textVal.StartsWith(" ")) {
                            var macroText = result.ToString();
                            if (macroText.StartsWith("<if([gnum75>0")) {
                                textVal = macroText.Split(")")[0].Split(",")[2] + textVal;
                            }
                        }
                    }
                    catch (Exception e) {
                        FindAnythingPlugin.Log.Warning(e, "Failed to parse (assumed) Controller/Gamepad Settings main command");
                    }

                    data.Add(excelRow.RowId, new SearchEntry
                    {
                        Display = textVal,
                        Searchable = GetSearchableText(textVal, normalizeKana)
                    });
                }

            }

            searchDb.Add(typeof(T), data);
        }

        public static string GetSearchableText(string input, bool normalizeKana) => input.Downcase(normalizeKana).Replace("'", string.Empty);

        public SearchEntry GetString<T>(uint row) where T : struct, IExcelRow<T> => SearchData[typeof(T)][row];

        public IReadOnlyDictionary<uint, SearchEntry> GetAll<T>() where T : struct, IExcelRow<T> => SearchData[typeof(T)];

        public static SearchDatabase Load(ClientLanguage lang) => new(lang);
    }
}