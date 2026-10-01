using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Caliburn.Micro;
using JetBrains.Annotations;
using LiteDbExplorer.Framework;
using LiteDbExplorer.Modules.Shared;
using LiteDbExplorer.Presentation;
using LiteDbExplorer.Wpf.Framework;
using LiteDbExplorer.Wpf.Framework.Shell;

namespace LiteDbExplorer.Modules.Main
{
    [Export(typeof(IDocumentSet))]
    [Export(typeof(IViewModelHost))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DocumentSetViewModel : Conductor<IDocument>.Collection.OneActive, IDocumentSet, IViewModelHost, IHandle<NavigationRequestMessage>
    {
        private readonly IEventAggregator _eventAggregator;

#pragma warning disable 649
        private bool _closing;
#pragma warning restore 649
        
        [ImportingConstructor]
        public DocumentSetViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            _eventAggregator.SubscribeOnUIThread(this);

            DisplayName = $"LiteDB Explorer {AppConstants.Application.CurrentVersion}";

            CloseDocumentCommand = new RelayCommand<FrameworkElement>(async element => await CloseDocument(element));
        }
        
        public Guid Id { get; } = Guid.NewGuid();

        public string ContentId => Id.ToString();
        
        public ICommand CloseDocumentCommand { get; }
        
        public IObservableCollection<IDocument> Documents => Items;

        private ILayoutItem _activeLayoutItem;
        
        public ILayoutItem ActiveLayoutItem
        {
            get => _activeLayoutItem;
            set
            {
                if (ReferenceEquals(_activeLayoutItem, value))
                {
                    return;
                }

                _activeLayoutItem = value;

                if (value is IDocument document)
                {
                    Execute.OnUIThread(async () => await ActivateItemAsync(document));
                }

                NotifyOfPropertyChange(() => ActiveLayoutItem);
            }
        }

        public async Task OpenDocument(IDocument model)
        {
            await ActivateItemAsync(model);
            model.IsSelected = true;
        }

        public async Task OpenDocument<TDocument>() where TDocument : IDocument
        {
            var doc = IoC.Get<TDocument>();

            await OpenDocument(doc);
        }

        public IScreen FindViewModel(Type viewModelType, IReferenceId referenceId)
        {
            if (!string.IsNullOrEmpty(referenceId.InstanceId))
            {
                return Items.FirstOrDefault(p => viewModelType.IsInstanceOfType(p) && !string.IsNullOrEmpty(p.InstanceId) && p.InstanceId.Equals(referenceId.InstanceId));
            }

            return null;
        }

        public async Task HandleAsync(NavigationRequestMessage message, CancellationToken cancellationToken)
        {
            if (message.ViewModel is IDocument document)
            {
                await ActivateItemAsync(document, cancellationToken);
                document.IsSelected = true;
            }
        }

        public async Task<TDocument> OpenDocument<TDocument, TNode>(TDocument document, TNode initPayload) where TDocument : IDocument<TNode> where TNode : IReferenceId
        {
            if (!string.IsNullOrEmpty(initPayload.InstanceId))
            {
                var instance = Items.OfType<TDocument>().FirstOrDefault(p => !string.IsNullOrEmpty(p.InstanceId) && p.InstanceId.Equals(initPayload.InstanceId));
                if (instance != null)
                {
                    await ActivateItemAsync(instance);
                    return instance;
                }
            }
            
            document.Init(initPayload);

            await OpenDocument(document);

            return document;
        }

        public async Task<TDocument> OpenDocument<TDocument, TNode>(TNode initPayload) where TDocument : IDocument<TNode> where TNode : IReferenceId
        {
            var model = IoC.Get<TDocument>();
            await OpenDocument(model, initPayload);
            return model;
        }

        public async Task CloseDocument(FrameworkElement element)
        {
            if (element.DataContext is IDocument document)
            {
                await CloseDocument(document);
            }
        }

        public Task CloseDocument(IDocument document)
        {
            return DeactivateItemAsync(document, true);
        }

        [UsedImplicitly]
        public async Task CloseAllDocuments()
        {
            if (Items == null)
            {
                return;
            }

            foreach (var document in Items.ToList())
            {
                await CloseDocument(document);
            }
        }

        [UsedImplicitly]
        public bool CanCloseAllDocuments => Items != null && Items.Any();

        [UsedImplicitly]
        public bool IsEmpty => Items == null || !Items.Any();

        [UsedImplicitly]
        public async Task OpenStartupDocument()
        {
            await OpenDocument<IStartupDocument>();
        }

        public override async Task ActivateItemAsync(IDocument item, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (_closing)
            {
                return;
            }

            RaiseActiveDocumentChanging();

            var currentActiveItem = ActiveItem;
            
            await base.ActivateItemAsync(item, cancellationToken);
            
            if (item != null)
            {
                await item.ActivateAsync(cancellationToken);
                item.IsSelected = true;
                item.PropertyChanged -= DocumentOnPropertyChanged;
                item.UpdateGroupDisplayRequest -= DocumentOnUpdateGroupDisplayRequest;
                item.PropertyChanged += DocumentOnPropertyChanged;
                item.UpdateGroupDisplayRequest += DocumentOnUpdateGroupDisplayRequest;
            }
            
            InvalidateDisplayGroup();

            if (!ReferenceEquals(item, currentActiveItem))
            {
                RaiseActiveDocumentChanged();
            }
        }

        public override async Task DeactivateItemAsync(IDocument item, bool close, CancellationToken cancellationToken = default(CancellationToken))
        {
            RaiseActiveDocumentChanging();
            
            if (close == false && item != null)
            {
                await item.DeactivateAsync(false, cancellationToken);
            }

            if (item != null)
            {
                item.PropertyChanged -= DocumentOnPropertyChanged;
                item.UpdateGroupDisplayRequest -= DocumentOnUpdateGroupDisplayRequest;
            }

            await base.DeactivateItemAsync(item, close, cancellationToken);

            InvalidateDisplayGroup();

            OnDocumentDeactivated(item, close);

            RaiseActiveDocumentChanged();
        }
        
        protected override void OnActivationProcessed(IDocument item, bool success)
        {
            if (!ReferenceEquals(ActiveLayoutItem, item))
            {
                ActiveLayoutItem = item;
            }

            base.OnActivationProcessed(item, success);
        }
        
        public void InvalidateDisplayGroup()
        {
            foreach (var document in Items)
            {
                if (document.GroupDisplayVisibility == GroupDisplayVisibility.AlwaysVisible)
                {
                    document.GroupDisplayNameIsVisible = true;
                }
                else
                {
                    document.GroupDisplayNameIsVisible = Items
                        .Where(p => p.GroupDisplayName != document.GroupDisplayName)
                        .Any(p => p.DisplayName.Equals(document.DisplayName));        
                }

                document.GroupDisplayBackground =
                    GroupDisplayColor.GetDisplayColor(document.GroupId, Colors.Transparent);
            }
        }

        private static readonly string[] _displayGroupProperties = {nameof(IDocument.DisplayName), nameof(IDocument.GroupDisplayName), nameof(IDocument.GroupId)};
        private void DocumentOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.PropertyName) && _displayGroupProperties.Contains(e.PropertyName))
            {
                InvalidateDisplayGroup();
            }
        }

        private void DocumentOnUpdateGroupDisplayRequest(object sender, EventArgs e)
        {
            InvalidateDisplayGroup();
        }

        private void RaiseActiveDocumentChanging()
        {
            var handler = ActiveDocumentChanging;
            handler?.Invoke(this, EventArgs.Empty);
        }

        private void RaiseActiveDocumentChanged()
        {
            var handler = ActiveDocumentChanged;
            handler?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnDocumentDeactivated(IDocument item, bool deactivate)
        {
            DocumentDeactivated?.Invoke(this, new DocumentDeactivateEventArgs(item, deactivate));
        }

        public event EventHandler ActiveDocumentChanging;
        public event EventHandler ActiveDocumentChanged;
        public event EventHandler<DocumentDeactivateEventArgs> DocumentDeactivated;
    }

    

}