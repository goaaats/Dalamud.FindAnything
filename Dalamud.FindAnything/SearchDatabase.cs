using System;
using System.Collections.Generic;
using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Lumina.Text;

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
            InitData<TerritoryType>(ref data, (r) => r.PlaceName?.Value?.Name);
            InitData<Aetheryte>(ref data, (r) => r.PlaceName?.Value?.Name);
            InitData<MainCommand>(ref data, (r) => r.Name);
            InitData<GeneralAction>(ref data, (r) => r.Name);
            InitData<Emote>(ref data, (r) => r.Name);
            InitData<Quest>(ref data, (r) => r.Name);
            InitData<Item>(ref data, (r) => r.Name);
            InitData<Recipe>(ref data, r =>
            {
                var itemResult = r.ItemResult.Value;

                if (itemResult == null || itemResult.RowId == 0) {
                    return null;
                }

                return itemResult.Name;
            });

            var item = FindAnythingPlugin.Data.GetExcelSheet<Item>()!;

            InitData<GatheringItem>(ref data, r =>
            {
                var itemResult = item.GetRow((uint)r.Item);

                if (itemResult == null || itemResult.RowId == 0) {
                    return null;
                }

                return itemResult.Name;
            });

            SearchData = data;
        }

        private void InitData<T>(ref Dictionary<Type, IReadOnlyDictionary<uint, SearchEntry>> searchDb, Func<T, SeString?> rowToFind) where T : ExcelRow
        {
            var normalizeKana = NormalizeKana;
            var data = new Dictionary<uint, SearchEntry>();
            foreach (var excelRow in FindAnythingPlugin.Data.GetExcelSheet<T>()!)
            {
                var result = rowToFind.Invoke(excelRow);
                if (result != null)
                {
                    var textVal = result.ToDalamudString().TextValue;
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

        public SearchEntry GetString<T>(uint row) where T : ExcelRow => SearchData[typeof(T)][row];

        public IReadOnlyDictionary<uint, SearchEntry> GetAll<T>() where T : ExcelRow => SearchData[typeof(T)];

        public static SearchDatabase Load(ClientLanguage lang) => new(lang);
    }
}