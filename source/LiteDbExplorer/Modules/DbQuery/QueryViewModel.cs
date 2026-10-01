using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Caliburn.Micro;
using CSharpFunctionalExtensions;
using JetBrains.Annotations;
using LiteDbExplorer.Core;
using LiteDbExplorer.Framework;
using LiteDbExplorer.Modules.Help;
using LiteDbExplorer.Modules.Shared;
using LiteDbExplorer.Wpf.Framework;
using LiteDbExplorer.Wpf.Framework.Shell;
using MaterialDesignThemes.Wpf;

namespace LiteDbExplorer.Modules.DbQuery
{

    [Export(typeof(QueryViewModel))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public sealed class QueryViewModel : DocumentConductorOneActive<RunQueryContext>, INavigationTarget<RunQueryContext>, IQueryHistoryHandler
    {
        private static int _queryRefCount;
        private readonly IApplicationInteraction _applicationInteraction;
        private readonly IQueryViewsProvider _queryViewsProvider;
        private readonly IQueryHistoryProvider _queryHistoryProvider;
        private IQueryView _view;
        private DatabaseReference _currentDatabase;

        [ImportingConstructor]
        public QueryViewModel(
            IApplicationInteraction applicationInteraction, 
            IQueryViewsProvider queryViewsProvider,
            IQueryHistoryProvider queryHistoryProvider)
        {
            _applicationInteraction = applicationInteraction;
            _queryViewsProvider = queryViewsProvider;
            _queryHistoryProvider = queryHistoryProvider;

            DisplayName = "Query";

            RunQueryCommand = new AsyncCommand(RunAllRawQuery, () => CanRunQuery);

            RunSelectedQueryCommand = new AsyncCommand(RunSelectedQuery, ()=> CanRunSelectedQuery);

            OpenHelpCommand = new AsyncCommand(OpenHelp);

            QueryHistoryView = IoC.Get<QueryHistoryViewModel>();

            QueryHistoryView.Parent = this;

            QueryHistoryView.FilterActiveDatabase = true;

            QueryHandlersMetadata = _queryViewsProvider.ListMetadata();

            CurrentQueryHandlerName = QueryHandlersMetadata.Select(p => p.Name).FirstOrDefault();
        }

        public override object IconContent => new PackIcon { Kind = PackIconKind.CodeGreaterThan };

        public ICommand RunQueryCommand { get; }

        public ICommand RunSelectedQueryCommand { get; }

        public ICommand OpenHelpCommand { get; }

        public QueryHistoryViewModel QueryHistoryView { get; }

        public IEnumerable<IQueryViewHandlerMetadata> QueryHandlersMetadata { get; }

        [UsedImplicitly]
        public ObservableCollection<DatabaseReference> Databases => Store.Current.Databases;

        public RunQueryContext InitialQueryContext { get; set; }

        public DatabaseReference CurrentDatabase
        {
            get => _currentDatabase;
            set
            {
                _currentDatabase = value;
                QueryHistoryView?.SetActiveDatabase(CurrentDatabase);
                _view?.UpdateCodeCompletion(CurrentDatabase);
            }
        }

        public QueryReference QueryReference { get; set; }

        public string RawQuery { get; set; } = string.Empty;

        public string RawQuerySelected { get; set; } = string.Empty;

        [UsedImplicitly]
        public bool CanRunQuery => CurrentDatabase != null && !string.IsNullOrWhiteSpace(RawQuery);

        [UsedImplicitly]
        public bool CanRunSelectedQuery => CurrentDatabase != null && 
                                           !string.IsNullOrWhiteSpace(RawQuerySelected) &&
                                           RawQuerySelected.Trim().StartsWith("db.", StringComparison.OrdinalIgnoreCase);

        [UsedImplicitly]
        public bool CanExportResult => false;

        [UsedImplicitly]
        public bool ShowHistory { get; set; }

        [UsedImplicitly]
        public string CurrentQueryHandlerName { get; set; }

        public override void Init(RunQueryContext item)
        {
            if (item == null)
            {
                Execute.OnUIThread(async () => await TryCloseAsync(false));
                return;
            }

            _queryRefCount++;

            InitialQueryContext = item;

            CurrentDatabase = item.DatabaseReference;

            QueryReference = item.QueryReference;

            DisplayName = $"Query {_queryRefCount}";

            if (item.QueryReference != null)
            {
                RawQuery = item.QueryReference.GetRawQuery();
            }
        }

        protected override async void OnViewLoaded(object view)
        {
            _view = view as IQueryView;

            _view?.SelectEnd();

            if (InitialQueryContext?.RunOnStart == true)
            {
                await Task.Delay(250);
                await RunAllRawQuery();
            }

            _view?.UpdateCodeCompletion(CurrentDatabase);
        }

        public override void NotifyOfPropertyChange(string propertyName = null)
        {
            base.NotifyOfPropertyChange(propertyName);

            if (propertyName == nameof(CurrentDatabase))
            {
                SetDisplay(CurrentDatabase);
            }
        }

        private void SetDisplay(DatabaseReference databaseReference)
        {
            GroupId = databaseReference?.InstanceId;
            GroupDisplayName = databaseReference?.Name;
            GroupDisplayVisibility = GroupDisplayVisibility.AlwaysVisible;
        }

        [UsedImplicitly]
        public async Task NewQuery()
        {
            await _applicationInteraction.OpenQuery(new RunQueryContext(CurrentDatabase, QueryReference));
        }

        [UsedImplicitly]
        public async Task RunAllRawQuery()
        {
            await RunQuery(RawQuery);
        }

        [UsedImplicitly]
        public async Task RunSelectedQuery()
        {
            if (string.IsNullOrWhiteSpace(RawQuerySelected))
            {
                return;
            }

            await RunQuery(RawQuerySelected);
        }

        public async Task OpenHelp()
        {
            var documentSet = IoC.Get<IDocumentSet>();
            var context = new GithubWikiMarkdownDocContext("Using Shell - Command reference", @"mbdavid", @"LiteDB", @"Shell");
            await documentSet.OpenDocument<MarkdownDocViewModel, IMarkdownDocContext>(context);
        }

        private async Task RunQuery(string query)
        {
            ActiveItem = null;
            Items.Clear();

            if (string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            var maybeQueryHandler = _queryViewsProvider.FindHandler(CurrentQueryHandlerName);
            if (maybeQueryHandler.HasNoValue)
            {
                _applicationInteraction.ShowAlert("Unable to get query handler.");
                return;
            }

            var items = await maybeQueryHandler.Value.Value.RunQuery(CurrentDatabase, _queryHistoryProvider, query);

            Items.AddRange(items);

            ActiveItem = Items.FirstOrDefault();
        }

        public Task<Result> InsertQuery(RawQueryHistory item)
        {
            if (item != null)
            {
                var database = Databases.FirstOrDefault(p => p.Location.Equals(item.DatabaseLocation, StringComparison.Ordinal));
                
                // TODO: Notify change current database
                if (database != null && database != CurrentDatabase)
                {
                    CurrentDatabase = database;
                }

                if (!string.IsNullOrEmpty(item.QueryHandlerName))
                {
                    CurrentQueryHandlerName = item.QueryHandlerName;
                }

                _view?.InsetDocumentText(item.RawQuery);
            }

            return Task.FromResult(Result.Success());
        }
    }
}