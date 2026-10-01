using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Caliburn.Micro;
using Humanizer;
using JetBrains.Annotations;
using LiteDbExplorer.Core;

namespace LiteDbExplorer.Modules.DbQuery
{
    [Export(typeof(QueryResultViewModel))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class QueryResultViewModel : Screen
    {
        private readonly IDatabaseInteractions _databaseInteractions;
        private Stopwatch _stopwatch;

        [ImportingConstructor]
        public QueryResultViewModel(IDatabaseInteractions databaseInteractions)
        {
            _databaseInteractions = databaseInteractions;
        }

        public IDisposable StartTime()
        {
            _stopwatch = Stopwatch.StartNew();
            return Disposable.Create(() =>
            {
                _stopwatch?.Stop();
                ElapsedTime = _stopwatch.Elapsed;
            });
        }

        public void SetResult(string displayName, string rawQuery, QueryResult queryResult)
        {
            DisplayName = displayName;
            RawQuery = rawQuery;
            QueryResult = queryResult;
        }

        public QueryResult QueryResult { get; private set; }

        public string RawQuery { get; set; } = string.Empty;

        public TimeSpan? ElapsedTime { get; private set; }

        [UsedImplicitly]
        public string ResultSetCountInfo => !QueryResult.HasValue ? "No records" : "record".ToQuantity(QueryResult.Count);

        [UsedImplicitly]
        public string ElapsedTimeInfo => ElapsedTime.HasValue ? $"Query time: {ElapsedTime.Value.Humanize(3, null, TimeUnit.Minute)}" : string.Empty;

        [UsedImplicitly]
        public int ContentMaxLength { get; private set; } = 10000;

        [UsedImplicitly]
        public async Task ExportJson()
        {
            await _databaseInteractions.ExportAs(this, QueryResult, DisplayName);
        }

        [UsedImplicitly]
        public bool CanExportJson => QueryResult != null && QueryResult.HasValue;
    }
}