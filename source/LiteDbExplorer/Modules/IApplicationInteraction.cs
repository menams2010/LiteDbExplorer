using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using LiteDbExplorer.Core;
using LiteDbExplorer.Modules.DbQuery;
using LiteDbExplorer.Modules.Shared;

namespace LiteDbExplorer.Modules
{
    public interface IApplicationInteraction
    {
        Task<bool> OpenDatabaseProperties(DatabaseReference database);
        bool OpenEditDocument(DocumentReference document);
        Task<Result> OpenQuery(RunQueryContext queryContext);
        Task<bool> RevealInExplorer(string filePath);
        Task<Result> ActivateDefaultDocumentView(DocumentReference document);
        Task<Result> ActivateDefaultCollectionView(CollectionReference collection, IEnumerable<DocumentReference> selectedDocuments = null);
        bool ShowConfirm(string message, string title = "Are you sure?");
        void ShowError(string message, string title = "");
        void ShowError(Exception exception, string message, string title = "");
        void PutClipboardText(string text);
        Task ShowAbout();
        Task ShowReleaseNotes(Version version = null);
        Task ShowIssueHelper();
        Task<Maybe<string>> ShowSaveFileDialog(string title = "", string filter = "All files|*.*", string fileName = "",
            string initialDirectory = "", bool overwritePrompt = true);
        Task<Maybe<string>> ShowOpenFileDialog(string title = "", string filter = "All files|*.*", string fileName = "",
            string initialDirectory = "");
        Task<Maybe<string>> ShowFolderPickerDialog(string title = "", string initialDirectory = "");
        Task<Maybe<string>> ShowInputDialog(string message, string caption = "", string predefined = "", Func<string, Result> validationFunc = null);
        Task<bool> OpenFileWithAssociatedApplication(string filePath);
        void ShowAlert(string message, string title = null, UINotificationType type = UINotificationType.None);
        Task<bool> ShowImportWizard(ImportDataOptions options = null);
        Task<Maybe<PasswordInput>> ShowPasswordInputDialog(string message, string caption = "", string predefined = "", bool rememberMe = false);
        ChangesActionResult ShowChangesActionDialog(string message = null);
    }
}