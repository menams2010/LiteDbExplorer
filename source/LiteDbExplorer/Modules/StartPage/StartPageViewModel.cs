using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using DynamicData;
using DynamicData.Binding;
using JetBrains.Annotations;
using LiteDbExplorer.Presentation;
using LiteDbExplorer.Wpf.Framework.Shell;

namespace LiteDbExplorer.Modules.StartPage
{
    [Export(typeof(StartPageViewModel))]
    [Export(typeof(IStartupDocument))]
    [PartCreationPolicy (CreationPolicy.Shared)]
    public class StartPageViewModel : Document, IStartupDocument
    {
        private readonly IDatabaseInteractions _databaseInteractions;
        private readonly IApplicationInteraction _applicationInteraction;
        private bool _showStartPageOnOpen;

        private readonly IDisposable _cleanUp;
        private readonly ReadOnlyObservableCollection<RecentDatabaseFileInfo> _recentFilesFiltered;

        [ImportingConstructor]
        public StartPageViewModel(
            IDatabaseInteractions databaseInteractions, 
            IApplicationInteraction applicationInteraction,
            IRecentDatabaseFilesProvider recentDatabaseFilesProvider)
        {
            _databaseInteractions = databaseInteractions;
            _applicationInteraction = applicationInteraction;

            PathDefinitions = recentDatabaseFilesProvider;

            ShowStartPageOnOpen = Properties.Settings.Default.ShowStartPageOnOpen;
            
            var recentFilesTermFilter = this.WhenValueChanged(vm => vm.SearchTerm)
                .Throttle(TimeSpan.FromMilliseconds(150))
                .Select(CreatePredicate);

            _cleanUp = PathDefinitions.RecentFiles
                .ToObservableChangeSet()
                .Filter(recentFilesTermFilter)
                .Sort(
                    SortExpressionComparer<RecentDatabaseFileInfo>
                        .Descending(p => p.FixedAt.HasValue)
                        .ThenByDescending(p => p.FixedAt ?? p.LastOpenedAt)
                )
                .ObserveOn(new DispatcherSynchronizationContext())
                .Bind(out _recentFilesFiltered)
                .Do(p =>
                {
                    NotifyOfPropertyChange(nameof(RecentFilesListIsEmpty));
                    NotifyOfPropertyChange(nameof(RecentFilesListEmptyMessage));
                })
                .Subscribe();
        }
        
        public override string DisplayName => "Start";

        public override string InstanceId => "StartPage";

        public override object IconContent => IconProvider.GetResourceDrawingImageIcon(@"AppIconImage", new ImageIconOptions{Height = 16});

        public IRecentDatabaseFilesProvider PathDefinitions { get; }

        public bool ShowStartPageOnOpen
        {
            get => _showStartPageOnOpen;
            set
            {
                if (!Equals(_showStartPageOnOpen, value))
                {
                    _showStartPageOnOpen = value;
                    SaveSettings();
                }
            }
        }

        [UsedImplicitly]
        public bool RecentFilesListIsEmpty => !_recentFilesFiltered.Any();

        [UsedImplicitly]
        public string RecentFilesListEmptyMessage
        {
            get
            {
                if (!RecentFilesListIsEmpty)
                {
                    return null;
                }

                return !string.IsNullOrWhiteSpace(SearchTerm)
                    ? $"No results found for '{SearchTerm}'"
                    : "No recent items in the list";
            }
        }

        public string SearchTerm { get; set; }

        [UsedImplicitly]
        public ReadOnlyObservableCollection<RecentDatabaseFileInfo> RecentFilesFiltered => _recentFilesFiltered;

        private Func<RecentDatabaseFileInfo, bool> CreatePredicate(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return info => true;
            }

            var lowerTerm = term.ToLower();
            return info => info.FileName.ToLower().Contains(lowerTerm) || info.DirectoryPath.ToLower().Contains(lowerTerm);
        }

        public void SaveSettings()
        {
            Properties.Settings.Default.ShowStartPageOnOpen = ShowStartPageOnOpen;
            Properties.Settings.Default.Save();
        }

        [UsedImplicitly]
        public void ClearSearch()
        {
            SearchTerm = null;
        }

        [UsedImplicitly]
        public async Task OpenDatabase()
        {
            await _databaseInteractions.OpenDatabase();
        }

        [UsedImplicitly]
        public async Task OpenRecentItem(RecentDatabaseFileInfo recentDatabaseFileInfo)
        {
            if (recentDatabaseFileInfo == null)
            {
                return;
            }

            if (recentDatabaseFileInfo.FileNotFound.HasValue && recentDatabaseFileInfo.FileNotFound == true)
            {
                var message = $"File {recentDatabaseFileInfo.FullPath} not found.\n\nRemove from list?";
                if (_applicationInteraction.ShowConfirm(message, "File not found!"))
                {
                    RemoveFromList(recentDatabaseFileInfo);
                }
                return;
            }

            await _databaseInteractions.OpenDatabase(recentDatabaseFileInfo.FullPath);
        }
        
        [UsedImplicitly]
        public void OpenIssuePage()
        {
            Process.Start(Config.IssuesUrl);
        }

        [UsedImplicitly]
        public void OpenHomepage()
        {
            Process.Start(Config.HomepageUrl);
        }

        [UsedImplicitly]
        public void OpenDocs()
        {
            Process.Start("https://github.com/mbdavid/LiteDB/wiki");
        }

        [UsedImplicitly]
        public void RevealInExplorer(RecentDatabaseFileInfo recentDatabaseFileInfo)
        {
            if (recentDatabaseFileInfo != null)
            {
                _applicationInteraction.RevealInExplorer(recentDatabaseFileInfo.FullPath);
            }
        }

        [UsedImplicitly]
        public void CopyPath(RecentDatabaseFileInfo recentDatabaseFileInfo)
        {
            if (recentDatabaseFileInfo != null)
            {
                _applicationInteraction.PutClipboardText(recentDatabaseFileInfo.FullPath);
            }
        }

        [UsedImplicitly]
        public void RemoveFromList(RecentDatabaseFileInfo recentDatabaseFileInfo)
        {
            if (recentDatabaseFileInfo != null)
            {
                PathDefinitions.RemoveRecentFile(recentDatabaseFileInfo.FullPath);
            }
        }

        [UsedImplicitly]
        public void PinItem(RecentDatabaseFileInfo recentDatabaseFileInfo)
        {
            if (recentDatabaseFileInfo != null)
            {
                PathDefinitions.SetRecentFileFixed(recentDatabaseFileInfo.FullPath, true);
            }
        }

        [UsedImplicitly]
        public bool CanPinItem(RecentDatabaseFileInfo recentDatabaseFileInfo)
        {
            return recentDatabaseFileInfo != null && !recentDatabaseFileInfo.IsFixed;
        }

        [UsedImplicitly]
        public void UnPinItem(RecentDatabaseFileInfo recentDatabaseFileInfo)
        {
            if (recentDatabaseFileInfo != null)
            {
                PathDefinitions.SetRecentFileFixed(recentDatabaseFileInfo.FullPath, false);
            }
        }

        [UsedImplicitly]
        public bool CanUnPinItem(RecentDatabaseFileInfo recentDatabaseFileInfo)
        {
            return recentDatabaseFileInfo != null && recentDatabaseFileInfo.IsFixed;
        }
    }

    

}