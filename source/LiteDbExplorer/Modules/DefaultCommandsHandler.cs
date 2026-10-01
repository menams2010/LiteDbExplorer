using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using LiteDbExplorer.Core;
using LiteDbExplorer.Framework.Services;
using LiteDbExplorer.Modules.Main;
using Serilog;

namespace LiteDbExplorer.Modules
{
    public class DefaultCommandsHandler : ApplicationCommandHandler
    {
        private static readonly ILogger Logger = Log.ForContext<DefaultCommandsHandler>();

        private readonly IDatabaseInteractions _databaseInteractions;
        private readonly IApplicationInteraction _applicationInteraction;

        [ImportingConstructor]
        public DefaultCommandsHandler(
            IDatabaseInteractions databaseInteractions,
            IApplicationInteraction applicationInteraction)
        {
            _databaseInteractions = databaseInteractions;
            _applicationInteraction = applicationInteraction;

            Add(Commands.Exit, (sender, args) =>
            {
                Store.Current.CloseDatabases();

                if (Application.Current.MainWindow != null)
                {
                    Application.Current.MainWindow.Close();
                }
            });

            Add(ApplicationCommands.New, async (sender, args) =>
            {
                await _databaseInteractions.CreateAndOpenDatabase();
            });

            Add(ApplicationCommands.Open, async (sender, args) =>
            {
                await _databaseInteractions.OpenDatabase();
            });

            Add(Commands.FileDropped, async (sender, args) =>
            {
                if (!(args.Parameter is IDataObject dataObject))
                {
                    return;
                }

                try
                {
                    if (dataObject.GetDataPresent(DataFormats.FileDrop) && dataObject.GetData(DataFormats.FileDrop, false) is string[] paths)
                    {
                        await _databaseInteractions.OpenDatabases(paths);
                    }
                }
                catch (Exception exc)
                {
                    _applicationInteraction.ShowError("Failed to open database: " + exc.Message, "Database Error");
                }
            });

            Add(Commands.Import, async (sender, args) =>
            {
                await _applicationInteraction.ShowImportWizard();
            }, (sender, args) =>
            {
                var hasDatabaseOpen = Store.Current.Databases.Any();
                args.CanExecute = hasDatabaseOpen;
            });

            Add(Commands.ShowNavigationPanel, (sender, args) =>
            {
                var isVisible = !ShellLayoutController.Current.LeftContentIsVisible;
                if (args.Parameter is bool isVisibleParam)
                {
                    isVisible = isVisibleParam;
                }
                ShellLayoutController.Current.LeftContentIsVisible = isVisible;
            });

            Add(Commands.ShowToolsPanel, (sender, args) =>
            {
                var isVisible = !ShellLayoutController.Current.ToolsPanelIsVisible;
                if (args.Parameter is bool isVisibleParam)
                {
                    isVisible = isVisibleParam;
                }
                ShellLayoutController.Current.ToolsPanelIsVisible = isVisible;
            });
        }

    }
}