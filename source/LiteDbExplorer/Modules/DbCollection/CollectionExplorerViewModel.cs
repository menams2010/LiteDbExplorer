using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Caliburn.Micro;
using CSharpFunctionalExtensions;
using JetBrains.Annotations;
using LiteDbExplorer.Core;
using LiteDbExplorer.Framework;
using LiteDbExplorer.Modules.DbDocument;
using LiteDbExplorer.Wpf.Framework;
using LiteDbExplorer.Wpf.Framework.Shell;
using MaterialDesignThemes.Wpf;
using Serilog;

namespace LiteDbExplorer.Modules.DbCollection
{
    [Export(typeof(CollectionExplorerViewModel))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class CollectionExplorerViewModel : DocumentConductor<CollectionReferencePayload, IDocumentPreview>,
        INavigationTarget<CollectionReferencePayload>, IErrorHandler
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IApplicationInteraction _applicationInteraction;
        private readonly IDatabaseInteractions _databaseInteractions;
        private DocumentReference _selectedDocument;
        private ICollectionReferenceListView _view;
        private bool _showDocumentPreview = true;
        private CollectionReference _collectionReference;

        [ImportingConstructor]
        public CollectionExplorerViewModel(
            IEventAggregator eventAggregator,
            IApplicationInteraction applicationInteraction,
            IDatabaseInteractions databaseInteractions)
        {
            _eventAggregator = eventAggregator;
            _applicationInteraction = applicationInteraction;
            _databaseInteractions = databaseInteractions;

            SplitOrientation = Properties.Settings.Default.CollectionExplorer_SplitOrientation;
            ShowDocumentPreview = Properties.Settings.Default.CollectionExplorer_ShowPreview;
            ContentMaxLength = Properties.Settings.Default.CollectionExplorer_ContentMaxLength;
            DoubleClickAction = Properties.Settings.Default.CollectionExplorer_DoubleClickAction;

            FindTextModel = new FindTextModel();

            ItemDoubleClickCommand = new RelayCommand<DocumentReference>(async doc => await OnItemDoubleClick(doc));

            AddDocumentCommand = new AsyncCommand(AddDocument, CanAddDocument, this);
            EditDocumentCommand = new AsyncCommand<DocumentReference>(EditDocument, CanEditDocument, this);
            RemoveDocumentCommand = new AsyncCommand(RemoveDocument, CanRemoveDocument, this);
            ExportDocumentCommand = new AsyncCommand(ExportDocument, CanExportDocument, this);
            CopyDocumentCommand = new AsyncCommand(CopyDocument, CanCopyDocument, this);
            PasteDocumentCommand = new AsyncCommand(PasteDocument, CanPasteDocument, this);

            RefreshCollectionCommand = new RelayCommand(_ => RefreshCollection(), o => CanRefreshCollection());
            EditDbPropertiesCommand = new RelayCommand(async _ => await EditDbProperties(), o => CanEditDbProperties());
            FindCommand = new RelayCommand(_ => OpenFind(), o => CanOpenFind());
            FindNextCommand = new RelayCommand(_ => Find(), o => CanFind());
            FindPreviousCommand = new RelayCommand(_ => FindPrevious(), o => CanFind());

            FileDroppedCommand = new AsyncCommand<IDataObject>(OnFileDropped);
        }

        public CollectionItemDoubleClickAction DoubleClickAction { get; }

        public int ContentMaxLength { get; }

        public Orientation? SplitOrientation { get; }

        public RelayCommand<DocumentReference> ItemDoubleClickCommand { get; }

        public ICommand AddDocumentCommand { get; }

        public ICommand EditDocumentCommand { get; }

        public ICommand RemoveDocumentCommand { get; }

        public ICommand ExportDocumentCommand { get; }

        public ICommand CopyDocumentCommand { get; }

        public ICommand PasteDocumentCommand { get; }

        public ICommand RefreshCollectionCommand { get; }

        public ICommand EditDbPropertiesCommand { get; }

        public ICommand FindCommand { get; }

        public ICommand FindNextCommand { get; }

        public ICommand FindPreviousCommand { get; }

        public ICommand FileDroppedCommand { get; }

        public FindTextModel FindTextModel { get; }

        [UsedImplicitly]
        public CollectionReference CollectionReference
        {
            get => _collectionReference;
            private set
            {
                if (_collectionReference != null)
                {
                    _collectionReference.PropertyChanged -= OnCollectionReferencePropertyChanged;
                    _collectionReference.ReferenceChanged -= OnCollectionReferenceChanged;
                    _collectionReference.DocumentsCollectionChanged -= OnDocumentsCollectionChanged;
                }

                _collectionReference = value;
                if (_collectionReference != null)
                {
                    _collectionReference.PropertyChanged += OnCollectionReferencePropertyChanged;
                    _collectionReference.ReferenceChanged += OnCollectionReferenceChanged;
                    _collectionReference.DocumentsCollectionChanged += OnDocumentsCollectionChanged;
                }
            }
        }

        [UsedImplicitly]
        public DocumentReference SelectedDocument
        {
            get => _selectedDocument;
            set
            {
                _selectedDocument = value;
                if (_showDocumentPreview)
                {
                    ActivateDocumentPreview();
                }
                else
                {
                    DeactivateDocumentPreview();
                }
            }
        }

        [UsedImplicitly]
        public IList<DocumentReference> SelectedDocuments { get; set; }

        [UsedImplicitly]
        public bool IsFindOpen { get; private set; }

        public bool ShowDocumentPreview
        {
            get => _showDocumentPreview;
            set
            {
                if (_showDocumentPreview == false && value)
                {
                    ActivateDocumentPreview();
                }

                _showDocumentPreview = value;
                if (_showDocumentPreview == false)
                {
                    DeactivateDocumentPreview();
                }
            }
        }

        [UsedImplicitly]
        public bool HideDocumentPreview => SelectedDocument == null || !ShowDocumentPreview;

        [UsedImplicitly]
        public string DocumentsCountInfo
        {
            get
            {
                if (CollectionReference?.Items == null)
                {
                    return "Collection is Null";
                }

                return CollectionReference.Items.Count == 1 ? "1 item" : $"{CollectionReference.Items.Count} items";
            }
        }

        [UsedImplicitly]
        public string SelectedDocumentsCountInfo
        {
            get
            {
                if (SelectedDocuments == null)
                {
                    return string.Empty;
                }

                return SelectedDocuments.Count == 1 ? "1 selected item" : $"{SelectedDocuments.Count} selected items";
            }
        }

        public Func<object> GetIconContent { get; set; }

        public override object IconContent => GetIconContent?.Invoke();

        public override void Init(CollectionReferencePayload value)
        {
            if (value == null)
            {
                Execute.OnUIThread(async () => await TryCloseAsync(false));
                return;
            }

            Log.Debug("Init. {ViewModelName}, ReferenceId {ReferenceId}", nameof(CollectionExplorerViewModel),
                value.InstanceId);

            InstanceId = value.InstanceId;

            var collectionReference = value.CollectionReference;

            DisplayName = collectionReference.Name;

            if (collectionReference.Database != null)
            {
                GroupId = collectionReference.Database.InstanceId;
                GroupDisplayName = collectionReference.Database.Name;
            }

            GetIconContent = () =>  collectionReference is FileCollectionReference
                ? new PackIcon {Kind = PackIconKind.FileMultiple}
                : new PackIcon {Kind = PackIconKind.TableLarge, Height = 16};

            CollectionReference = collectionReference;

            if (value.SelectedDocuments != null)
            {
                SelectedDocuments = value.SelectedDocuments.ToList();
            }
            else
            {
                SelectedDocument = CollectionReference.Items.FirstOrDefault();
                SelectedDocuments = new List<DocumentReference> { SelectedDocument };
            }

            
        }

        public void HandleError(Exception ex)
        {
            _applicationInteraction.ShowError(ex, "An error occurred while performing the action.");
        }

        protected override void OnViewLoaded(object view)
        {
            _view = view as ICollectionReferenceListView;

            if (_view != null)
            {
                _view.CollectionLoadedAction = () =>
                {
                    _view?.FocusListView();

                    if (SelectedDocument != null)
                    {
                        _view?.ScrollIntoSelectedItem();
                    }
                };
            }
        }

        protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            await base.OnDeactivateAsync(close, cancellationToken);
            if (close)
            {
                Log.Debug("Deactivate {ViewModelName}, ReferenceId {ReferenceId}", nameof(CollectionExplorerViewModel),
                    InstanceId);

                await DeactivateItemAsync(ActiveItem, true, cancellationToken);

                SelectedDocuments = null;
                SelectedDocument = null;
                CollectionReference = null;
            }
        }

        protected async void ActivateDocumentPreview()
        {
            if (ActiveItem == null)
            {
                ActiveItem = IoC.Get<IDocumentPreview>();
            }

            ActiveItem?.SetActiveDocument(_selectedDocument);
            await ActivateItemAsync(ActiveItem);
        }

        protected async void DeactivateDocumentPreview()
        {
            await DeactivateItemAsync(ActiveItem, false);
        }

        #region Handles

        private void OnCollectionReferencePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals(nameof(CollectionReference.Name)))
            {
                DisplayName = CollectionReference.Name;
            }
        }

        private async void OnCollectionReferenceChanged(object sender, ReferenceChangedEventArgs<CollectionReference> e)
        {
            switch (e.Action)
            {
                case ReferenceNodeChangeAction.Remove:
                case ReferenceNodeChangeAction.Dispose:
                    await TryCloseAsync();
                    break;
                case ReferenceNodeChangeAction.Update:
                case ReferenceNodeChangeAction.Add:
                    _view?.UpdateView(e.Reference);
                    break;
            }
        }

        private void OnDocumentsCollectionChanged(object sender,
            CollectionReferenceChangedEventArgs<DocumentReference> e)
        {
            if (e.Action == ReferenceNodeChangeAction.Add)
            {
                SelectedDocument = e.Items.FirstOrDefault() ?? SelectedDocument;
                _view?.UpdateView(SelectedDocument);
            }

            if (e.Action == ReferenceNodeChangeAction.Update)
            {
                _view?.UpdateView(SelectedDocument);
            }
        }

        protected async Task OnItemDoubleClick(DocumentReference documentReference)
        {
            if (documentReference == null)
            {
                return;
            }

            switch (DoubleClickAction)
            {
                case CollectionItemDoubleClickAction.EditDocument:
                    await _databaseInteractions.OpenEditDocument(documentReference);
                    break;
                case CollectionItemDoubleClickAction.OpenPreview:
                    await _applicationInteraction.ActivateDefaultDocumentView(documentReference);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        #region Routed Commands

        [UsedImplicitly]
        public async Task AddDocument()
        {
            var result = await _databaseInteractions.CreateDocument(this, CollectionReference);
            await result.Tap(async reference =>
            {
                await _applicationInteraction.ActivateDefaultCollectionView(reference.CollectionReference,
                    reference.Items);
                await _eventAggregator.PublishOnUIThreadAsync(reference);

                if (reference.PostAction is "edit" && reference.DocumentReference != null)
                {
                    await EditDocument(reference.DocumentReference);
                }
            });
        }

        [UsedImplicitly]
        public bool CanAddDocument()
        {
            return CollectionReference != null;
        }

        [UsedImplicitly]
        public async Task EditDocument(DocumentReference documentReference)
        {
            await _databaseInteractions.OpenEditDocument(documentReference);

            _view.FocusListView();
        }

        [UsedImplicitly]
        public bool CanEditDocument(DocumentReference documentReference)
        {
            return documentReference != null;
        }

        [UsedImplicitly]
        public async Task RemoveDocument()
        {
            await _databaseInteractions.RemoveDocuments(SelectedDocuments);
        }

        [UsedImplicitly]
        public bool CanRemoveDocument()
        {
            return SelectedDocuments.HasAnyDocumentsReference();
        }

        [UsedImplicitly]
        public async Task ExportDocument()
        {
            await _databaseInteractions.ExportAs(this, CollectionReference, SelectedDocuments);
        }

        [UsedImplicitly]
        public bool CanExportDocument()
        {
            return CollectionReference != null && CollectionReference.Items.Any();
        }

        [UsedImplicitly]
        public async Task CopyDocument()
        {
            await _databaseInteractions.CopyDocuments(SelectedDocuments);
        }

        [UsedImplicitly]
        public bool CanCopyDocument()
        {
            return SelectedDocuments.HasAnyDocumentsReference();
        }

        [UsedImplicitly]
        public async Task PasteDocument()
        {
            var textData = Clipboard.GetText();

            await _databaseInteractions
                .ImportDataFromText(CollectionReference, textData)
                .Tap(update => _eventAggregator.PublishOnUIThreadAsync(update));
        }

        [UsedImplicitly]
        public bool CanPasteDocument()
        {
            return !CollectionReference.IsFilesCollection();
        }

        [UsedImplicitly]
        public void RefreshCollection()
        {
            CollectionReference?.Refresh();
        }

        [UsedImplicitly]
        public bool CanRefreshCollection()
        {
            return CollectionReference != null;
        }

        [UsedImplicitly]
        public async Task EditDbProperties()
        {
            await _applicationInteraction.OpenDatabaseProperties(CollectionReference.Database);
        }

        [UsedImplicitly]
        public bool CanEditDbProperties()
        {
            return CollectionReference?.Database != null;
        }

        [UsedImplicitly]
        public void OpenFind()
        {
            IsFindOpen = true;
        }

        [UsedImplicitly]
        public void Find()
        {
            IsFindOpen = true;

            _view?.Find(FindTextModel.Text, FindTextModel.MatchCase);
        }

        [UsedImplicitly]
        public void FindPrevious()
        {
            IsFindOpen = true;

            _view?.FindPrevious(FindTextModel.Text, FindTextModel.MatchCase);
        }

        [UsedImplicitly]
        public void CloseFind()
        {
            IsFindOpen = false;
            _view?.FindClear();
        }

        [UsedImplicitly]
        public bool CanOpenFind()
        {
            return CollectionReference != null;
        }

        [UsedImplicitly]
        public bool CanFind()
        {
            return CollectionReference != null && IsFindOpen;
        }

        private async Task OnFileDropped(IDataObject dataObject)
        {
            if (dataObject.GetDataPresent(DataFormats.FileDrop) &&
                dataObject.GetData(DataFormats.FileDrop, false) is string[] paths)
            {
                if (CollectionReference.IsFilesOrChunks)
                {
                    var result = await _databaseInteractions.AddFileToDatabase(this, CollectionReference.Database, filePath: paths.FirstOrDefault());
                    if (result.IsSuccess)
                    {
                        Init(new CollectionReferencePayload(result.Value.CollectionReference));
                        _view?.SelectItem(result.Value.DocumentReference);
                    }
                    
                    return;
                }

                await _databaseInteractions.OpenDatabases(paths);
            }
        }

        #endregion
    }

    public class FindTextModel : INotifyPropertyChanged
    {
        public string Text { get; set; }

        public bool MatchCase { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        [UsedImplicitly]
        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}