using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Caliburn.Micro;
using LiteDbExplorer.Modules.Diagnostics;
using LiteDbExplorer.Modules.Shared;
using LiteDbExplorer.Wpf.Framework.Shell;
using LiteDbExplorer.Wpf.Modules.Output;

namespace LiteDbExplorer.Modules.Main
{
    [Export(typeof(IShell))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class ShellViewModel : Screen, IShell
    {
        private readonly IEventAggregator _eventAggregator;

        private IOwnerViewModelMessageHandler _view;

        [ImportingConstructor]
        public ShellViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            _eventAggregator.SubscribeOnUIThread(this);

            DisplayName = AppConstants.Application.DisplayName;

            WindowMenu = IoC.Get<IShellMenu>();

            WindowRightMenu = IoC.Get<IShellRightMenu>();

            StatusBarContent = IoC.Get<IShellStatusBar>();

            LeftContent = IoC.Get<IDocumentExplorer>();

            MainContent = IoC.Get<IDocumentSet>();

            ToolPanelsContent = IoC.Get<IToolPanelSet>();

            MainContent.DocumentDeactivated += MainContentOnDocumentDeactivated;

            Properties.Settings.Default.PropertyChanged += OnSettingsPropertyChanged;
        }

        public object WindowMenu { get; }

        public object WindowRightMenu { get; }

        public object LeftContent { get; }

        public IShellStatusBar StatusBarContent { get; }

        public IDocumentSet MainContent { get; }

        public IToolPanelSet ToolPanelsContent { get; }

        protected override void OnViewReady(object view)
        {
            _view = view as IOwnerViewModelMessageHandler;

            try
            {
                if (Application.Current.Properties["ArbitraryArgName"] != null)
                {
                    var arg = Application.Current.Properties["ArbitraryArgName"].ToString();
                    switch (arg)
                    {
                        case AppConstants.CmdlineCommands.New:
                        {
                            IoC.Get<IDatabaseInteractions>().CreateAndOpenDatabase().Wait();
                            break;
                        }
                        case AppConstants.CmdlineCommands.Open:
                        {
                            IoC.Get<IDatabaseInteractions>().OpenDatabase().Wait();
                            break;
                        }
                    }

                    Application.Current.Properties["ArbitraryArgName"] = null;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        protected override async void OnViewLoaded(object view)
        {
            await ToolPanelsContent.ActivateItemAsync(IoC.Get<IOutput>());

            if (Properties.Settings.Default.ShowNavigationPanelOnOpen)
            {
                ShellLayoutController.Current.LeftContentIsVisible = true;
            }

            if (Properties.Settings.Default.ShowStartPageOnOpen)
            {
                await MainContent.OpenDocument<IStartupDocument>();
            }

            if (Properties.Settings.Default.Diagnostics_ShowManagedMemory)
            {
                StatusBarContent.ActivateContent(new MemoryUsageStatusButton().Start(), StatusBarContentLocation.Right);
            }

            CommandManager.InvalidateRequerySuggested();

            if (!AppConstants.IsDebug)
            {
                _ = Task.Delay(TimeSpan.FromSeconds(30))
                    .ContinueWith(task => { OnUIThread(async () => await AppUpdateManager.Current.CheckForUpdates(false)); })
                    .ConfigureAwait(false);
            }
        }

        private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Properties.Settings.Default.Diagnostics_ShowManagedMemory))
            {
                if (Properties.Settings.Default.Diagnostics_ShowManagedMemory)
                {
                    var usageStatusButton = StatusBarContent.ActivateContent(new MemoryUsageStatusButton(), StatusBarContentLocation.Right) as MemoryUsageStatusButton;
                    usageStatusButton?.Start();
                }
                else
                {
                    StatusBarContent.DeactivateContent(MemoryUsageStatusButton.ContentIdTag);
                }
            }
        }

        private async void MainContentOnDocumentDeactivated(object sender, DocumentDeactivateEventArgs e)
        {
            if (!e.Close)
            {
                return;
            }

            if (!MainContent.Documents.Any() && Properties.Settings.Default.ShowStartOnCloseAll)
            {
                if (e.Item is IStartupDocument)
                {
                    return;
                }

                await MainContent.OpenDocument<IStartupDocument>();
            }
        }
    }
}