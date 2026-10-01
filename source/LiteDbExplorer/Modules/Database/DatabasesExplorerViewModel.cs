using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Caliburn.Micro;
using CSharpFunctionalExtensions;
using Enterwell.Clients.Wpf.Notifications;
using JetBrains.Annotations;
using LiteDbExplorer.Core;
using LiteDbExplorer.Framework;
using LiteDbExplorer.Modules.DbQuery;
using LiteDbExplorer.Modules.Shared;
using LiteDbExplorer.Presentation;

namespace LiteDbExplorer.Modules.Database
{
    [Export(typeof(IDocumentExplorer))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DatabasesExplorerViewModel : Screen, IDocumentExplorer, IErrorHandler
    {
        private readonly IDatabaseInteractions _databaseInteractions;
        private readonly IApplicationInteraction _applicationInteraction;
        private IDatabasesExplorerView _view;

        [ImportingConstructor]
        public DatabasesExplorerViewModel(
            IDatabaseInteractions databaseInteractions,
            IApplicationInteraction applicationInteraction, 
            IRecentDatabaseFilesProvider recentDatabaseFilesProvider)
        {
            _databaseInteractions = databaseInteractions;
            _applicationInteraction = applicationInteraction;

            PathDefinitions = recentDatabaseFilesProvider;

            CloseDatabaseCommand = new AsyncCommand<DatabaseReference>(CloseDatabase, CanCloseDatabase, this);
            EditDbPropertiesCommand = new AsyncCommand<DatabaseReference>(EditDbProperties, CanEditDbProperties, this);
            SaveDatabaseCopyAsCommand = new AsyncCommand<DatabaseReference>(SaveDatabaseCopyAs, CanSaveDatabaseCopyAs, this);
            AddFileCommand = new AsyncCommand<DatabaseReference>(AddFile, CanAddFile, this);
            AddCollectionCommand = new AsyncCommand<DatabaseReference>(AddCollection, CanAddCollection, this);
            RefreshDatabaseCommand = new AsyncCommand<DatabaseReference>(RefreshDatabase, CanRefreshDatabase, this);
            RevealInExplorerCommand = new AsyncCommand<DatabaseReference>(RevealInExplorer, CanRevealInExplorer, this);

            RenameCollectionCommand = new AsyncCommand<CollectionReference>(RenameCollection, CanRenameCollection, this);
            DropCollectionCommand = new AsyncCommand<CollectionReference>(DropCollection, CanDropCollection, this);
            ExportCollectionCommand = new AsyncCommand<CollectionReference>(ExportCollection, CanExportCollection, this);
            
            ImportDataCommand = new AsyncCommand(ImportData, CanImportData, this);

            OpenRecentItemCommand = new AsyncCommand<RecentDatabaseFileInfo>(OpenRecentItem);

            NodeDefaulActionCommand = new AsyncCommand<object>(NodeDefaultAction);

            Store.Current.Databases.CollectionChanged += OnDatabasesCollectionChanged;
        }

        public IRecentDatabaseFilesProvider PathDefinitions { get; }

        public ICommand CloseDatabaseCommand { get; }

        public ICommand EditDbPropertiesCommand { get; }

        public ICommand SaveDatabaseCopyAsCommand { get; }

        public ICommand AddFileCommand { get; }

        public ICommand AddCollectionCommand { get; }

        public ICommand RefreshDatabaseCommand { get; }

        public ICommand RevealInExplorerCommand { get; }

        public ICommand RenameCollectionCommand { get; }

        public ICommand DropCollectionCommand { get; }

        public ICommand ExportCollectionCommand { get; }

        public ICommand ImportDataCommand { get; }

        public ICommand OpenRecentItemCommand { get; }

        public ICommand NodeDefaulActionCommand { get; }

        [UsedImplicitly]
        public ObservableCollection<DatabaseReference> Databases => Store.Current.Databases;

        [UsedImplicitly]
        public bool HasAnyDatabaseOpen => Store.Current.Databases.Any();

        [UsedImplicitly]
        public bool IsEmpty => Databases == null || !Databases.Any();

        public DatabaseReference SelectedDatabase { get; private set; }

        public CollectionReference SelectedCollection { get; private set; }

        protected override void OnViewLoaded(object view)
        {
            _view = view as IDatabasesExplorerView;
        }

        private void OnDatabasesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Commands.ShowNavigationPanel.ExecuteOnMainWindow(true);

            NotifyOfPropertyChange(nameof(IsEmpty));
            NotifyOfPropertyChange(nameof(HasAnyDatabaseOpen));

            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                var firstItem = e.NewItems.OfType<object>().FirstOrDefault();
                _view?.FocusItem(firstItem, true);
            }
        }

        [UsedImplicitly]
        public async Task OpenDatabase()
        {
            await _databaseInteractions.OpenDatabase();
        }

        [UsedImplicitly]
        public async Task OpenRecentItem(RecentDatabaseFileInfo info)
        {
            if (info == null)
            {
                return;
            }

            await _databaseInteractions.OpenDatabase(info.FullPath);
        }

        [UsedImplicitly]
        public async Task NewQuery(object item)
        {
            switch (item)
            {
                case DatabaseReference databaseReference:
                    await _applicationInteraction.OpenQuery(new RunQueryContext(databaseReference));
                    break;
                case CollectionReference collectionReference:
                    await _applicationInteraction.OpenQuery(new RunQueryContext(collectionReference.Database, QueryReference.Find(collectionReference)));
                    break;
            }
        }

        [UsedImplicitly]
        public void NewFindQuery(CollectionReference item, int? skip = null, int? limit = null)
        {
            var queryReference = QueryReference.Find(item, skip, limit);

            _applicationInteraction.OpenQuery(new RunQueryContext(item.Database, queryReference) { RunOnStart = true });
        }

        [UsedImplicitly]
        public void OnSelectedItemChanged(RoutedPropertyChangedEventArgs<object> e)
        {
            var value = e.NewValue as IReferenceNode;

            switch (value)
            {
                case null:
                    SelectedDatabase = null;
                    SelectedCollection = null;
                    break;
                case CollectionReference collection:
                    SelectedDatabase = collection.Database;
                    SelectedCollection = collection;
                    break;
                case DatabaseReference reference:
                {
                    SelectedDatabase = reference;
                    if (SelectedCollection != null && SelectedCollection.Database != SelectedDatabase)
                    {
                        SelectedCollection = null;
                    }

                    break;
                }

                default:
                    SelectedDatabase = null;
                    SelectedCollection = null;
                    break;
            }
        }

        public async Task NodeDefaultAction(object value)
        {
            switch (value)
            {
                case CollectionReference collectionReference:
                    await _applicationInteraction.ActivateDefaultCollectionView(collectionReference);
                    break;
                case DatabaseReference databaseReference:
                    await _applicationInteraction.OpenDatabaseProperties(databaseReference);
                    break;
            }
        }

        [UsedImplicitly]
        public async Task NodeKeyDownAction(KeyEventArgs keyArgs, object item)
        {   
            if (keyArgs.Key == Key.Enter)
            {
                keyArgs.Handled = true;
                await NodeDefaultAction(item);
            }
        }

        public void HandleError(Exception ex)
        {
            _applicationInteraction.ShowError(ex, "An error occurred while performing the action.");
        }

        #region Routed Commands

        [UsedImplicitly]
        public async Task SaveDatabaseCopyAs(DatabaseReference databaseReference)
        {
            var newDatabasePath = await _databaseInteractions.SaveDatabaseCopyAs(databaseReference);
            if (newDatabasePath.HasValue)
            {
                NotificationInteraction.Default()
                    .HasMessage($"Database copy saved in:\n{newDatabasePath.Value.ShrinkPath(128)}")
                    .Dismiss().WithButton("Open", async button =>
                    {
                        await _databaseInteractions.OpenDatabase(newDatabasePath.Value).ConfigureAwait(false);
                    })
                    .WithButton("Reveal in Explorer", button =>
                    {
                        _applicationInteraction.RevealInExplorer(newDatabasePath.Value);
                        })
                    .Dismiss().WithButton("Close", button => { })
                    .Queue();
            }
        }

        public bool CanSaveDatabaseCopyAs(DatabaseReference databaseReference)
        {
            return databaseReference != null;
        }

        [UsedImplicitly]
        public async Task CloseDatabase(DatabaseReference databaseReference)
        {
            if (SelectedCollection?.Database == databaseReference)
            {
                SelectedCollection = null;
            }

            if (SelectedDatabase == databaseReference)
            {
                SelectedDatabase = null;
            }

            await _databaseInteractions.CloseDatabase(databaseReference);
        }

        [UsedImplicitly]
        public bool CanCloseDatabase(DatabaseReference databaseReference)
        {
            return databaseReference != null;
        }

        [UsedImplicitly]
        public Task EditDbProperties(DatabaseReference databaseReference)
        {
            return _applicationInteraction.OpenDatabaseProperties(databaseReference);
        }

        [UsedImplicitly]
        public bool CanEditDbProperties(DatabaseReference databaseReference)
        {
            return databaseReference != null;
        }

        [UsedImplicitly]
        public async Task AddFile(DatabaseReference databaseReference)
        {
            await _databaseInteractions.AddFileToDatabase(this, databaseReference)
                .Tap(async reference =>
                {
                    await _applicationInteraction.ActivateDefaultCollectionView(reference.CollectionReference, reference.Items);
                });
        }

        [UsedImplicitly]
        public bool CanAddFile(DatabaseReference databaseReference)
        {
            return databaseReference != null;
        }

        [UsedImplicitly]
        public async Task AddCollection(DatabaseReference databaseReference)
        {
            await _databaseInteractions.AddCollection(this, databaseReference)
                .Tap(async reference =>
                {
                    await _applicationInteraction.ActivateDefaultCollectionView(reference);
                });
        }

        [UsedImplicitly]
        public bool CanAddCollection(DatabaseReference databaseReference)
        {
            return databaseReference != null;
        }

        [UsedImplicitly]
        public Task RefreshDatabase(DatabaseReference databaseReference)
        {
            databaseReference?.Refresh();

            return Task.CompletedTask;
        }

        [UsedImplicitly]
        public bool CanRefreshDatabase(DatabaseReference databaseReference)
        {
            return databaseReference != null;
        }

        [UsedImplicitly]
        public async Task RevealInExplorer(DatabaseReference databaseReference)
        {
            if (databaseReference == null)
            {
                return;
            }

            await _applicationInteraction.RevealInExplorer(databaseReference.Location);
        }

        [UsedImplicitly]
        public bool CanRevealInExplorer(DatabaseReference databaseReference)
        {
            return databaseReference != null;
        }

        [UsedImplicitly]
        public async Task RenameCollection(CollectionReference collectionReference)
        {
            await _databaseInteractions.RenameCollection(collectionReference);
        }

        [UsedImplicitly]
        public bool CanRenameCollection(CollectionReference collectionReference)
        {
            return collectionReference != null && !collectionReference.IsFilesOrChunks;
        }

        [UsedImplicitly]
        public async Task DropCollection(CollectionReference collectionReference)
        {
            await _databaseInteractions.DropCollection(collectionReference);

            SelectedCollection = null;
        }

        [UsedImplicitly]
        public bool CanDropCollection(CollectionReference collectionReference)
        {
            return collectionReference != null;
        }

        [UsedImplicitly]
        public async Task ExportCollection(CollectionReference collectionReference)
        {
            await _databaseInteractions.ExportAs(this, collectionReference);
        }

        [UsedImplicitly]
        public bool CanExportCollection(CollectionReference collectionReference)
        {
            return collectionReference != null;
        }

        [UsedImplicitly]
        public async Task ImportData()
        {
            var options = new ImportDataOptions();
            if (SelectedCollection != null)
            {
                options.DatabaseReference = SelectedCollection.Database;
                options.CollectionReference = SelectedCollection;
            } 
            else if (SelectedDatabase != null)
            {
                options.DatabaseReference = SelectedDatabase;
            }

            await _applicationInteraction.ShowImportWizard(options);
        }

        [UsedImplicitly]
        public bool CanImportData()
        {
            return HasAnyDatabaseOpen;
        }

        #endregion

    }
}