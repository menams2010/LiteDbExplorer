using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Caliburn.Micro;
using Enterwell.Clients.Wpf.Notifications;
using Humanizer;
using JetBrains.Annotations;
using LiteDbExplorer.Controls;
using LiteDbExplorer.Framework;
using Onova;
using Onova.Services;
using Serilog;
using Action = System.Action;

namespace LiteDbExplorer.Modules
{
    public class AppUpdateManager : Freezable, INotifyPropertyChanged
    {
        private readonly UpdateManager _updateManager;
        private bool _hasUpdate;
        private bool _isBusy;

        private static readonly Lazy<AppUpdateManager> _instance =
            new Lazy<AppUpdateManager>(() => new AppUpdateManager());

        public static AppUpdateManager Current => _instance.Value;

        private static readonly ILogger Logger = Log.ForContext<AppUpdateManager>();

        public AppUpdateManager()
        {
            _updateManager = new UpdateManager(
                new GithubPackageResolver(AppConstants.Github.RepositoryOwner, AppConstants.Github.RepositoryName, "*.zip"), 
                new LocalZipPackageExtractor{ IgnoreZipRootPath = true });

            UpdateActionText = "Update";

            CheckForUpdatesCommand = new AsyncCommand(() => CheckForUpdates(true), () => !IsBusy);

            DoUpdateCommand = new AsyncCommand(DoUpdate, () => HasUpdate && !IsBusy);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                _isBusy = value;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool HasUpdate
        {
            get => _hasUpdate;
            private set
            {
                _hasUpdate = value;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string UpdateActionText { get; private set; }

        public string UpdateMessage { get; private set; }

        public Version LastVersion { get; private set; }

        public bool IsUpdatePrepared { get; private set; }

        public double UpdateProgress { get; private set; }

        public AsyncCommand CheckForUpdatesCommand { get; private set; }

        public AsyncCommand DoUpdateCommand { get; private set; }
        
        public async Task CheckForUpdates(bool userInitiated)
        {
            if (!userInitiated && !Properties.Settings.Default.UpdateManager_CheckUpdateOnStartup)
            {
                return;
            }

            // Limit request per hour
            var lastCheck = Properties.Settings.Default.UpdateManager_LastCheck;
            if (!userInitiated && lastCheck.HasValue && lastCheck.Value.AddHours(1) < DateTime.UtcNow)
            {
                return;
            }

            Logger.Information("Start check for updates. Last check {LastCheck}.", lastCheck);

            IsBusy = true;
            HasUpdate = false;
            UpdateMessage = string.Empty;
            LastVersion = null;

            try
            {
                // Check for updates
                var result = await _updateManager.CheckForUpdatesAsync();

                Properties.Settings.Default.UpdateManager_LastCheck = DateTime.UtcNow;
                if (result.LastVersion != null)
                {
                    Properties.Settings.Default.UpdateManager_LastVersion = result.LastVersion;
                }

                Properties.Settings.Default.Save();

                Logger.Information("Check for updates: {canUpdate}, {lastVersion}.", result.CanUpdate, result.LastVersion);

                if (result.CanUpdate)
                {
                    HasUpdate = true;

                    UpdateMessage = $"A new version {result.LastVersion} is available for update.";

                    LastVersion = result.LastVersion;

                    IsUpdatePrepared = _updateManager.IsUpdatePrepared(LastVersion);

                    ShowUpdateNotification();
                }
                else if (userInitiated)
                {
                    NotificationInteraction.Alert("There are currently no updates available.", UINotificationType.Info);
                }
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Error on check for updates.");
                if (userInitiated)
                {
                    NotificationInteraction.Alert("Unable to check for updates.", UINotificationType.Error);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task DoUpdate()
        {
            if (LastVersion == null)
            {
                return;
            }

            IsUpdatePrepared = _updateManager.IsUpdatePrepared(LastVersion);
            if (IsUpdatePrepared)
            {
                // Launch an executable that will apply the update
                // (can optionally restart application on completion)
                _updateManager.LaunchUpdater(LastVersion, true);

                // External updater will wait until the application exits
                Environment.Exit(0);
                return;
            }

            IsBusy = true;
            UpdateProgress = 0;

            try
            {
                var cancellationTokenSource = new CancellationTokenSource();

                UpdateMessage = "Downloading...";
                UpdateActionText = "Downloading...";

                ShowDownloadNotification();

                // Prepare an update so it can be applied later
                // (supports optional progress reporting and cancellation)
                await _updateManager.PrepareUpdateAsync(LastVersion, new Progress<double>(SetUpdateProgress), cancellationTokenSource.Token);

                IsUpdatePrepared = _updateManager.IsUpdatePrepared(LastVersion);

                UpdateActionText = "Restart";
                UpdateMessage = "Ready, restart to update...";
                UpdateProgress = 100;

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                UpdateActionText = "Update";
                UpdateMessage = "Error on application update.";
                MessageBox.Show(
                    e.Message,
                    "Error on application update.",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }

            ShowUpdateNotification();

            IsBusy = false;
        }

        private INotificationMessage _updateNotificationMessage;

        private void SetUpdateProgress(double value)
        {
            Dispatcher.BeginInvoke((Action) (() =>
            {
                            
                var progress = value > 90 ? "Preparing" : "Downloading";
                UpdateMessage = $"{progress}... {value:P0}";
                UpdateProgress = value * 100;

                if (_downloadNotification != null)
                {
                    _downloadNotification.Message = UpdateMessage;
                    if (_downloadNotification.OverlayContent is ProgressBar progressBar)
                    {
                        progressBar.IsIndeterminate = false;
                        progressBar.Value = UpdateProgress;
                    }
                }

            }), DispatcherPriority.Normal);
        }

        private void ShowUpdateNotification()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_downloadNotification != null)
                {
                    NotificationInteraction.Manager.Dismiss(_downloadNotification);
                }

                if (_updateNotificationMessage != null)
                {
                    NotificationInteraction.Manager.Dismiss(_updateNotificationMessage);
                }

                _updateNotificationMessage = NotificationInteraction.Default()
                    .HasBadge("Update")
                    .HasMessage(UpdateMessage)
                    .Dismiss().WithButton(IsUpdatePrepared ? "Restart to install" : "Download",
                        async button => { await DoUpdate(); })
                    .WithButton("Release notes",
                        async button => { await IoC.Get<IApplicationInteraction>().ShowReleaseNotes(LastVersion); })
                    .Dismiss().WithButton("Later", button => { })
                    .Queue();
                
            }, DispatcherPriority.Normal);

        }

        private INotificationMessage _downloadNotification;

        private void ShowDownloadNotification()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_updateNotificationMessage != null)
                {
                    NotificationInteraction.Manager.Dismiss(_updateNotificationMessage);
                }

                var progressBar = new ProgressBar
                {
                    VerticalAlignment = VerticalAlignment.Bottom,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Height = 4,
                    Minimum = 0,
                    Maximum = 100,
                    BorderThickness = new Thickness(0),
                    Foreground = StyleKit.AccentColorBrush,
                    Background = Brushes.Transparent,
                    IsIndeterminate = true,
                    IsHitTestVisible = false
                };

                _downloadNotification = NotificationInteraction.Default()
                    .HasBadge("Update")
                    .HasMessage(UpdateMessage)
                    .WithOverlay(progressBar)
                    .Queue();

            }, DispatcherPriority.Normal);
        }
        
        public event PropertyChangedEventHandler PropertyChanged;

        [UsedImplicitly]
        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected override Freezable CreateInstanceCore()
        {
            return _instance.Value;
        }
    }
}