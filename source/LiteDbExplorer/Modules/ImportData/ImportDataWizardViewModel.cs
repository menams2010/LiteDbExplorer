using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using JetBrains.Annotations;
using LiteDbExplorer.Modules.Shared;
using LiteDbExplorer.Wpf.Framework;

namespace LiteDbExplorer.Modules.ImportData
{
    [Export(typeof(ImportDataWizardViewModel))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class ImportDataWizardViewModel : Conductor<IStepsScreen>.Collection.OneActive, INavigationTarget<ImportDataOptions>
    {
        private IDisposable _activeItemObservable;
        private bool _suppressPreviousPush;

        public ImportDataWizardViewModel()
        {
            DisplayName = "Import Data";
        }

        public Stack<IStepsScreen> PreviousItems { get; } = new Stack<IStepsScreen>();

        public bool CanNext => ActiveItem?.HasNext ?? false;

        public bool CanPrevious => PreviousItems.Count > 1;

        public bool IsBusy { get; private set; }

        public void Init(ImportDataOptions modelParams)
        {
        }

        protected override async void OnViewReady(object view)
        {
            base.OnViewReady(view);

            var importDataHandlerSelector = IoC.Get<ImportDataHandlerSelector>();

            await ActivateItemAsync(importDataHandlerSelector);
        }

        public override async Task ActivateItemAsync(IStepsScreen item, CancellationToken cancellationToken = default(CancellationToken))
        {
            _activeItemObservable?.Dispose();

            if (!_suppressPreviousPush)
            {
                PreviousItems.Push(ActiveItem);
            }
            
            await base.ActivateItemAsync(item, cancellationToken);
            
            _activeItemObservable = item == null ? null : Observable
                .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                    handler => item.PropertyChanged += handler,
                    handler => item.PropertyChanged -= handler)
                .Where(args => string.IsNullOrEmpty(args.EventArgs.PropertyName) ||
                               args.EventArgs.PropertyName == nameof(IStepsScreen.HasNext))
                .Subscribe(args => NotifyOfPropertyChange(nameof(CanNext)));

            InvalidateProperties();
        }

        public override async Task DeactivateItemAsync(IStepsScreen item, bool close, CancellationToken cancellationToken = default(CancellationToken))
        {
            await base.DeactivateItemAsync(item, close, cancellationToken);

            PreviousItems.Push(item);

            InvalidateProperties();
        }

        [UsedImplicitly]
        public async Task Next()
        {
            if (ActiveItem == null || !ActiveItem.Validate())
            {
                return;
            }

            IsBusy = true;

            if (await ActiveItem?.Next() is IStepsScreen next)
            {
                await ActivateItemAsync(next);
            }

            IsBusy = false;
        }

        [UsedImplicitly]
        public async Task Previous()
        {
            var previous = PreviousItems.Pop();
            if (previous != null)
            {
                _suppressPreviousPush = true;
                try
                {
                    await ActivateItemAsync(previous);
                }
                finally
                {
                    _suppressPreviousPush = false;
                }
            }

            InvalidateProperties();
        }

        private void InvalidateProperties()
        {
            NotifyOfPropertyChange(nameof(CanNext));
            NotifyOfPropertyChange(nameof(CanPrevious));
        }
    }

}