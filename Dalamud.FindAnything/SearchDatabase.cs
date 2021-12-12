using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        private SearchDatabase()
        {
            var data = new Dictionary<Type, IReadOnlyDictionary<uint, SearchEntry>>();
            InitData<ContentFinderCondition>(ref data, (r) => r.Name);
            InitData<TerritoryType>(ref data, (r) => r.PlaceName?.Value?.Name);
            InitData<Aetheryte>(ref data, (r) => r.PlaceName?.Value?.Name);
            InitData<MainCommand>(ref data, (r) => r.Name);

            SearchData = data;
        }

        private void InitData<T>(ref Dictionary<Type, IReadOnlyDictionary<uint, SearchEntry>> searchDb, Func<T, SeString?> rowToFind) where T : ExcelRow
        {
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
                        Searchable = textVal.ToLowerInvariant(),
                    });
                }

            }

            searchDb.Add(typeof(T), data);
        }

        public SearchEntry GetString<T>(uint row) where T : ExcelRow => SearchData[typeof(T)][row];

        public IReadOnlyDictionary<uint, SearchEntry> GetAll<T>() where T : ExcelRow => SearchData[typeof(T)];

        public static SearchDatabase Load() => new();
    }
}