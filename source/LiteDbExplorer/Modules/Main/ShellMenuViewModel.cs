using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading.Tasks;
using Caliburn.Micro;
using JetBrains.Annotations;
using LiteDbExplorer.Modules.Shared;
using LiteDbExplorer.Wpf.Framework.Shell;
using LiteDbExplorer.Wpf.Modules.Settings;

namespace LiteDbExplorer.Modules.Main
{

    [Export(typeof(IShellMenu))]
    [PartCreationPolicy (CreationPolicy.Shared)]
    public class ShellMenuViewModel : PropertyChangedBase, IShellMenu
    {
        private readonly IDatabaseInteractions _databaseInteractions;
        private readonly IWindowManager _windowManager;
        private readonly IApplicationInteraction _applicationInteraction;
        private readonly IEventAggregator _eventAggregator;

        [ImportingConstructor]
        public ShellMenuViewModel(
            IDatabaseInteractions databaseInteractions,
            IWindowManager windowManager,
            IApplicationInteraction applicationInteraction,
            IEventAggregator eventAggregator,
            IRecentDatabaseFilesProvider recentDatabaseFilesProvider)
        {
            _databaseInteractions = databaseInteractions;
            _windowManager = windowManager;
            _applicationInteraction = applicationInteraction;
            _eventAggregator = eventAggregator;

            PathDefinitions = recentDatabaseFilesProvider;
        }

        public IRecentDatabaseFilesProvider PathDefinitions { get; }
        
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
        public Task OpenIssuePage()
        {
            return _applicationInteraction.ShowIssueHelper();
        }

        [UsedImplicitly]
        public void OpenHomepage()
        {
            Process.Start(Config.HomepageUrl);
        }

        [UsedImplicitly]
        public async Task OpenStartupDocument()
        {
            await IoC.Get<IDocumentSet>().OpenDocument<IStartupDocument>();
        }

        [UsedImplicitly]
        public Task OpenSettings()
        {
            return _windowManager.ShowDialogAsync(IoC.Get<SettingsViewModel>());
        }

        [UsedImplicitly]
        public Task OpenAbout()
        {
            return _applicationInteraction.ShowAbout();
        }

        [UsedImplicitly]
        public Task ShowReleaseNotes()
        {
            return _applicationInteraction.ShowReleaseNotes();
        }

        [UsedImplicitly]
        public async Task ViewErrorLogs()
        {
            var lastErrorLogPath = Paths.GetLastErrorLogPath();
            if (lastErrorLogPath.HasNoValue)
            {
                _applicationInteraction.ShowAlert("No error log file available.");
                return;
            }

            await _applicationInteraction.OpenFileWithAssociatedApplication(lastErrorLogPath.Value);
        }

        [UsedImplicitly]
        public async Task ViewAppData()
        {
            var appDataPath = Paths.AppDataPath;
            
            await _applicationInteraction.RevealInExplorer(appDataPath);
        }

    }
}