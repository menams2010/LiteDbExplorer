using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using Humanizer;
using LiteDbExplorer.Framework.Windows;

namespace LiteDbExplorer.Modules.Help
{
    [Export(typeof(AboutViewModel))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class AboutViewModel : Screen
    {
        public static DialogOptions DefaultDialogOptions = new DialogOptions
        {
            Height = 570,
            Width = 765,
            ResizeMode = System.Windows.ResizeMode.NoResize,
            SizeToContent = System.Windows.SizeToContent.Manual,
            ShowMinButton = false,
            ShowMaxRestoreButton = false,
            ShowIconOnTitleBar = false,
            ShowDialogsOverTitleBar = true,
            ShowInTaskbar = false
        };

        public AboutViewModel()
        {
            DisplayName = "About";
        }

        protected override async Task OnActivatedAsync(CancellationToken cancellationToken)
        {
            await base.OnActivatedAsync(cancellationToken);

            CurrentVersion = AppConstants.Application.CurrentVersion;

            DirectoryLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;

            SetUpdateMessage();
        }

        public Version CurrentVersion { get; private set; }

        public string DirectoryLocation { get; private set; }

        public string VersionUpdateMessage { get; private set; }

        private void SetUpdateMessage()
        {
            VersionUpdateMessage = string.Empty;
            
            var lastVersion = Properties.Settings.Default.UpdateManager_LastVersion;
            var lastCheck = Properties.Settings.Default.UpdateManager_LastCheck;
            if (lastVersion != null)
            {
                if (CurrentVersion.CompareTo(lastVersion) < 0)
                {
                    VersionUpdateMessage += $"Update {lastVersion} is available.";
                }
                else
                {
                    VersionUpdateMessage += "Updated.";
                }
            }

            if (lastCheck.HasValue)
            {
                VersionUpdateMessage += $" Last update check on {lastCheck.Value.Humanize()}.";
            }
        }
    }
}