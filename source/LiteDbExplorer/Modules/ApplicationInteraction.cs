using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Caliburn.Micro;
using CSharpFunctionalExtensions;
using Forge.Forms;
using LiteDbExplorer.Controls;
using LiteDbExplorer.Core;
using LiteDbExplorer.Framework.Windows;
using LiteDbExplorer.Modules.Database;
using LiteDbExplorer.Modules.DbCollection;
using LiteDbExplorer.Modules.DbDocument;
using LiteDbExplorer.Modules.DbQuery;
using LiteDbExplorer.Modules.Help;
using LiteDbExplorer.Modules.ImportData;
using LiteDbExplorer.Modules.Shared;
using LiteDbExplorer.Windows;
using LiteDbExplorer.Wpf.Framework;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Ookii.Dialogs.Wpf;
using DialogOptions = LiteDbExplorer.Framework.Windows.DialogOptions;
using TaskDialog = Microsoft.WindowsAPICodePack.Dialogs.TaskDialog;
using TaskDialogButton = Microsoft.WindowsAPICodePack.Dialogs.TaskDialogButton;

namespace LiteDbExplorer.Modules
{
    public enum ChangesActionResult
    {
        Save,
        Discard,
        Cancel
    }

    [Export(typeof(IApplicationInteraction))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class ApplicationInteraction : IApplicationInteraction
    {
        private readonly IWindowManager _windowManager;
        private readonly IShellNavigationService _navigationService;

        [ImportingConstructor]
        public ApplicationInteraction(IWindowManager windowManager, IShellNavigationService navigationService)
        {
            _windowManager = windowManager;
            _navigationService = navigationService;
        }

        public async Task<bool> OpenDatabaseProperties(DatabaseReference database)
        {
            var vm = IoC.Get<IDatabasePropertiesView>();
            vm.Init(database);
            
            var dialogOptions = new DialogOptions
            {
                Width = 480,
                MinWidth = 480,
                MinHeight = 740,
                MaxHeight = SystemParameters.VirtualScreenHeight - 160,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.CanResize,
                ShowMaxRestoreButton = false
            }
            .SizeToFit();

            return await _windowManager.ShowDialogAsync(vm, null, dialogOptions.Value) == true;
        }

        public async Task<bool> ShowImportWizard(ImportDataOptions options = null)
        {
            var vm = IoC.Get<ImportDataWizardViewModel>();
            vm.Init(options);
            
            var dialogOptions = new DialogOptions
            {
                Width = 800,
                MinWidth = 600,
                Height = 700,
                MinHeight = 500,
                SizeToContent = SizeToContent.Manual,
                ResizeMode = ResizeMode.CanResizeWithGrip
            }
            .SizeToFit();

            return await _windowManager.ShowDialogAsync(vm, null, dialogOptions.Value) == true;
        }

        public bool OpenEditDocument(DocumentReference document)
        {
            /*var vm = IoC.Get<DocumentEntryViewModel>();
            vm.Init(document);

            dynamic settings = new ExpandoObject();
            settings.Height = 600;
            settings.Width = 640;
            settings.SizeToContent = SizeToContent.Manual;

            return _windowManager.ShowDialog(vm, null, settings) == true;*/

            var windowController = new WindowController {Title = "Document Editor"};
            var control = new DocumentEntryControl(document, windowController);
            var window = new DialogWindow(control, windowController)
            {
                MinWidth = 400,
                MinHeight = 400,
                Height = Math.Min(Math.Max(636, SystemParameters.VirtualScreenHeight / 1.61), SystemParameters.VirtualScreenHeight)
            };
            if (document.Collection.IsFilesOrChunks)
            {
                window.Width = Math.Min(1024, SystemParameters.VirtualScreenWidth);
            }
            window.Owner = windowController.InferOwnerOf(window);

            return window.ShowDialog() == true;

            // TODO: Handle UpdateGridColumns(document.Value.LiteDocument) and UpdateDocumentPreview();
        }

        public async Task<Result> OpenQuery(RunQueryContext queryContext)
        {
            await _navigationService.Navigate<QueryViewModel>(queryContext);

            return Result.Success();
        }
        

        public Task<bool> RevealInExplorer(string filePath)
        {
            var isFile = Path.HasExtension(filePath);
            if ((Path.HasExtension(filePath) && !File.Exists(filePath)) || !isFile && !Directory.Exists(filePath))
            {
                return Task.FromResult(false);
            }

            //Clean up file path so it can be navigated OK
            filePath = Path.GetFullPath(filePath);

            System.Diagnostics.Process.Start("explorer.exe", isFile ? $"/select,\"{filePath}\"" : filePath);


            return Task.FromResult(true);
        }

        public Task<bool> OpenFileWithAssociatedApplication(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                return Task.FromResult(false);
            }

            //Clean up file path so it can be navigated OK
            filePath = Path.GetFullPath(filePath);
            System.Diagnostics.Process.Start(filePath);

            return Task.FromResult(true);
        }

        public async Task<Result> ActivateDefaultCollectionView(CollectionReference collection, IEnumerable<DocumentReference> selectedDocuments = null)
        {
            if (collection == null)
            {
                return Result.Success();
            }

            await _navigationService.Navigate<CollectionExplorerViewModel>(new CollectionReferencePayload(collection, selectedDocuments));

            return Result.Success();
        }

        public async Task<Result> ActivateDefaultDocumentView(DocumentReference document)
        {
            if (document == null)
            {
                return Result.Success();
            }

            await _navigationService.Navigate<DocumentPreviewViewModel>(new DocumentReferencePayload(document));

            return Result.Success();
        }
        
        public void PutClipboardText(string text)
        {
            Clipboard.SetData(DataFormats.Text, text);
        }

        public bool ShowConfirm(string message, string title = "Are you sure?")
        {
            return MessageBox.Show(
                       message,
                       title,
                       MessageBoxButton.YesNo,
                       MessageBoxImage.Question
                   ) == MessageBoxResult.Yes;
        }

        protected static Dictionary<UINotificationType, MessageBoxImage> NotificationTypeToMessageBoxImage = 
            new Dictionary<UINotificationType, MessageBoxImage>
            {
                { UINotificationType.None, MessageBoxImage.None },
                { UINotificationType.Info, MessageBoxImage.Information },
                { UINotificationType.Warning, MessageBoxImage.Warning },
                { UINotificationType.Error, MessageBoxImage.Error },
            };

        public void ShowAlert(string message, string title = null, UINotificationType type = UINotificationType.None)
        {
            if (!NotificationTypeToMessageBoxImage.TryGetValue(type, out var image))
            {
                image = MessageBoxImage.None;
            }

            MessageBox.Show(
                       message,
                       string.IsNullOrEmpty(title) ? AppConstants.Application.DisplayName : title,
                       MessageBoxButton.OK,
                       image
                   );
        }

        public void ShowError(string message, string title = "")
        {
            MessageBox.Show(
                message,
                string.IsNullOrEmpty(title) ? "Error" : title,
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }

        public void ShowError(Exception exception, string message, string title = "")
        {
            var exceptionViewer = new ExceptionViewer(message, exception);
            var baseDialogWindow = new BaseDialogWindow
            {
                Title = string.IsNullOrEmpty(title) ? "Error" : title,
                Content = exceptionViewer,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.CanResizeWithGrip,
                MinHeight = 400,
                MinWidth = 500,
                ShowMinButton = false,
                ShowMaxRestoreButton = false
            };
            baseDialogWindow.ShowDialog();
        }


        public Task ShowAbout()
        {
            return _windowManager.ShowDialogAsync(IoC.Get<AboutViewModel>(), null, AboutViewModel.DefaultDialogOptions.Value);
        }

        public Task ShowReleaseNotes(Version version = null)
        {
            var viewModel = IoC.Get<ReleaseNotesViewModel>();
            viewModel.FilterVersion(version);
            return _windowManager.ShowDialogAsync(viewModel, null, ReleaseNotesViewModel.DefaultDialogOptions.Value);
        }

        public Task ShowIssueHelper()
        {
            var viewModel = IoC.Get<IssueHelperViewModel>();
            return _windowManager.ShowDialogAsync(viewModel, null, IssueHelperViewModel.DefaultDialogOptions.Value);
        }


        public Task<Maybe<string>> ShowSaveFileDialog(string title = "", string filter = "All files|*.*",
            string fileName = "", string initialDirectory = "", bool overwritePrompt = true)
        {
            var completionSource = new TaskCompletionSource<Maybe<string>>();

            var dialog = new SaveFileDialog
            {
                OverwritePrompt = overwritePrompt
            };

            if (!string.IsNullOrEmpty(fileName))
            {
                dialog.FileName = fileName;
            }

            if (!string.IsNullOrEmpty(filter))
            {
                dialog.Filter = filter;
            }

            if (!string.IsNullOrEmpty(title))
            {
                dialog.Title = title;
            }

            if (!string.IsNullOrEmpty(initialDirectory))
            {
                dialog.InitialDirectory = initialDirectory;
            }

            completionSource.SetResult(dialog.ShowDialog() == true ? dialog.FileName : Maybe<string>.None);

            return completionSource.Task;
        }

        public Task<Maybe<string>> ShowOpenFileDialog(string title = "", string filter = "All files|*.*",
            string fileName = "", string initialDirectory = "")
        {
            var completionSource = new TaskCompletionSource<Maybe<string>>();

            var dialog = new OpenFileDialog
            {
                Multiselect = false
            };

            if (!string.IsNullOrEmpty(fileName))
            {
                dialog.FileName = fileName;
            }

            if (!string.IsNullOrEmpty(filter))
            {
                dialog.Filter = filter;
            }

            if (!string.IsNullOrEmpty(title))
            {
                dialog.Title = title;
            }

            if (!string.IsNullOrEmpty(initialDirectory))
            {
                dialog.InitialDirectory = initialDirectory;
            }

            completionSource.SetResult(dialog.ShowDialog() == true ? dialog.FileName : Maybe<string>.None);

            return completionSource.Task;
        }

        public Task<Maybe<string>> ShowFolderPickerDialog(string title = "", string initialDirectory = "")
        {
            var completionSource = new TaskCompletionSource<Maybe<string>>();

            var dialog = new CommonOpenFileDialog
            {
                Multiselect = false,
                IsFolderPicker = true
            };

            if (!string.IsNullOrEmpty(title))
            {
                dialog.Title = title;
            }

            if (!string.IsNullOrEmpty(initialDirectory))
            {
                dialog.InitialDirectory = initialDirectory;
            }

            completionSource.SetResult(
                dialog.ShowDialog() == CommonFileDialogResult.Ok
                ? dialog.FileName
                : Maybe<string>.None);

            return completionSource.Task;
        }

        public Task<Maybe<string>> ShowInputDialog(string message, string caption = "", string predefined = "", Func<string, Result> validationFunc = null)
        {
            var completionSource = new TaskCompletionSource<Maybe<string>>();

            completionSource.SetResult(
                InputBoxWindow.ShowDialog(message, caption, predefined, validationFunc, out var inputText) == true
                    ? inputText
                    : Maybe<string>.None);

            return completionSource.Task;
        }

        public Task<Maybe<PasswordInput>> ShowPasswordInputDialog(string message, string caption = "", string predefined = "", bool rememberMe = false)
        {
            var dialogResult = InputBoxWindow.ShowDialog(message, caption, predefined, null, out var inputText);
            if (dialogResult != true)
            {
                return Task.FromResult(Maybe<PasswordInput>.None);
            }

            return Task.FromResult(Maybe<PasswordInput>.From(new PasswordInput(message, caption, inputText, rememberMe)));
        }


        public ChangesActionResult ShowChangesActionDialog(string message = null)
        {
            if (string.IsNullOrEmpty(message))
            {
                message = "There are unsaved changes.";
            }
            
            if(Ookii.Dialogs.Wpf.TaskDialog.OSSupportsTaskDialogs)
            {
                using(var dialog = new Ookii.Dialogs.Wpf.TaskDialog())
                {
                    dialog.WindowTitle = AppConstants.Application.DisplayName;
                    dialog.MainInstruction = message;
                    dialog.MainIcon = TaskDialogIcon.Information;
                    dialog.ButtonStyle = TaskDialogButtonStyle.CommandLinks;
                    
                    var saveButton = new Ookii.Dialogs.Wpf.TaskDialogButton("Save Changes");
                    var discardButton = new Ookii.Dialogs.Wpf.TaskDialogButton("Discard Changes");
                    var cancelButton = new Ookii.Dialogs.Wpf.TaskDialogButton(ButtonType.Cancel);

                    dialog.Buttons.Add(saveButton);
                    dialog.Buttons.Add(discardButton);
                    dialog.Buttons.Add(cancelButton);
                    var button = dialog.ShowDialog();

                    if (button == saveButton)
                    {
                        return ChangesActionResult.Save;
                    }

                    if (button == discardButton)
                    {
                        return ChangesActionResult.Discard;
                    }

                    return ChangesActionResult.Cancel;
                }
            }

            var messageBoxResult = MessageBox.Show(message, null, MessageBoxButton.YesNoCancel, MessageBoxImage.Information);
            if (messageBoxResult == MessageBoxResult.Yes)
            {
                return ChangesActionResult.Save;
            }

            if (messageBoxResult == MessageBoxResult.No)
            {
                return ChangesActionResult.Discard;
            }

            return ChangesActionResult.Cancel;
        }

    }
}