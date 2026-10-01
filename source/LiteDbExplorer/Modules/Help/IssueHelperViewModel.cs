using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Caliburn.Micro;
using Forge.Forms;
using Forge.Forms.Annotations;
using JetBrains.Annotations;
using LiteDbExplorer.Framework;
using LiteDbExplorer.Framework.Windows;
using LiteDbExplorer.Wpf.Framework.Windows;
using Octokit;
using DialogOptions = LiteDbExplorer.Framework.Windows.DialogOptions;

namespace LiteDbExplorer.Modules.Help
{
    [Export(typeof(IssueHelperViewModel))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class IssueHelperViewModel : Screen
    {
        public static DialogOptions DefaultDialogOptions = new DialogOptions
        {
            Height = WindowPosition.GoldenHeight(570),
            Width = WindowPosition.GoldenWidth(765),
            ResizeMode = ResizeMode.CanResizeWithGrip,
            SizeToContent = SizeToContent.Manual,
            ShowMinButton = false,
            ShowMaxRestoreButton = false,
            ShowIconOnTitleBar = false,
            ShowDialogsOverTitleBar = true,
            ShowInTaskbar = false
        }.SizeToFit();

        public IssueHelperViewModel()
        {
            DisplayName = "Issue Reporting";

            FormModel = new IssueFormModel
            {
                AppVersion = AppConstants.Application.CurrentVersion.ToString()
            };

            GoToPreviewCommand = new RelayCommand(GoToPreview);

            GoToFormCommand = new RelayCommand(GoToForm);

            ConfirmCommand = new RelayCommand(Confirm);
        }

        public IssueFormModel FormModel { get; private set; }

        public string IssueContentOutput { get; private set; }

        public ICommand GoToPreviewCommand { get; private set; }

        public ICommand GoToFormCommand { get; private set; }

        public ICommand ConfirmCommand { get; private set; }

        public int SelectedStepIndex { get; private set; }

        protected override async Task OnActivatedAsync(CancellationToken cancellationToken)
        {
            await base.OnActivatedAsync(cancellationToken);

            IsBusy = true;

            await GetAppVersionTags();

            IsBusy = false;
        }

        public bool IsBusy { get; set; }

        private void GoToForm(object obj)
        {
            SelectedStepIndex = 0;
        }

        private void GoToPreview(object obj)
        {
            var isValid = ModelState.Validate(FormModel);
            if (isValid)
            {
                IssueContentOutput = FormModel.BuildOutput();
                SelectedStepIndex = 1;
            }
        }

        private void Confirm(object obj)
        {
            SelectedStepIndex = 1;
            Clipboard.SetData(DataFormats.Text, IssueContentOutput);
            OpenNewIssuePage();
        }

        private void OpenNewIssuePage()
        {
            if (FormModel == null)
            {
                return;
            }

            var url = $"{Config.IssuesUrl.TrimEnd('/')}/new/?title={Uri.EscapeDataString(FormModel.Title)}";
            if (!string.IsNullOrEmpty(FormModel.IssueType) && FormModel.DefaultIssueTypes.Contains(FormModel.IssueType, StringComparer.OrdinalIgnoreCase))
            {
                var label = FormModel.DefaultIssueTypes.First(p =>
                    p.Equals(FormModel.IssueType, StringComparison.OrdinalIgnoreCase));

                url += $"&labels={Uri.EscapeDataString(label)}";
            }
            if (!string.IsNullOrEmpty(IssueContentOutput) && (url.Length + IssueContentOutput.Length) < 16384)
            {
                url += $"&body={Uri.EscapeDataString(IssueContentOutput)}";
            }
            Process.Start(url);
        }

        private async Task GetAppVersionTags()
        {
            try
            {
                var client = new GitHubClient(new ProductHeaderValue(AppConstants.Github.RepositoryOwner));

                var releases = await client.Repository.Release.GetAll(AppConstants.Github.RepositoryOwner,
                    AppConstants.Github.RepositoryName);

                var tags = releases.Select(p => p.TagName).Distinct();

                FormModel.VersionTags = tags;
            }
            catch (Exception)
            {
                // Ignore
                if (!string.IsNullOrEmpty(FormModel.AppVersion))
                {
                    FormModel.VersionTags = new []{ FormModel.AppVersion };
                }
            }
        }

    }

    [Title("New Issue Reporting")]
    [Form(Grid = new[] { 1d, 1d })]
    public class IssueFormModel : IActionHandler, INotifyPropertyChanged
    {
        [Field(Row = "1", ColumnSpan = 2)]
        [Value(Must.NotBeEmpty)]
        public string Title { get; set; }

        [Field(Row = "2")]
        [SelectFrom("{Binding DefaultIssueTypes}", SelectionType = SelectionType.ComboBoxEditable)]
        [Value(Must.NotBeEmpty)]
        public string IssueType { get; set; }

        [Field(Row = "2")]
        [SelectFrom("{Binding VersionTags}", SelectionType = SelectionType.ComboBoxEditable)]
        [Value(Must.NotBeEmpty)]
        public string AppVersion { get; set; }

        [Field(Row = "3", ColumnSpan = 2)]
        [MultiLine]
        [Value(Must.NotBeEmpty)]
        public string StepsToReproduce { get; set; }

        [Field(Row = "4", ColumnSpan = 2)]
        [MultiLine]
        [Value(Must.NotBeEmpty)]
        public string WhatIsExpected { get; set; }

        [Field(Row = "5", ColumnSpan = 2)]
        [MultiLine]
        [Value(Must.NotBeEmpty)]
        public string WhatIsActuallyHappening { get; set; }

        [Field(Row = "6", ColumnSpan = 2)]
        [MultiLine]
        public string AnyAdditionalComments { get; set; }

        [FieldIgnore]
        public IEnumerable<string> VersionTags { get; set; }

        [FieldIgnore]
        public IEnumerable<string> DefaultIssueTypes => new [] { "Bug Report", "Feature Request" };

        public void HandleAction(IActionContext actionContext)
        {
            switch (actionContext.Action)
            {
                case "validate":
                    ModelState.Validate(this);
                    break;
            }
        }

        public virtual string BuildOutput()
        {
            var builder = new StringBuilder();

            void Append(string header, string content)
            {
                if (!string.IsNullOrEmpty(content))
                {
                    builder.AppendLine($"**{header}:**");
                    builder.AppendLine(content);
                    builder.AppendLine();
                }
            }

            Append("This is a", IssueType);
            Append("Version", AppVersion);
            Append("Steps to reproduce", StepsToReproduce);
            Append("What is expected", WhatIsExpected);
            Append("What is actually happening", WhatIsActuallyHappening);
            Append("Any additional comments", AnyAdditionalComments);

            return builder.ToString();
        }

        public override string ToString()
        {
            return BuildOutput();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [UsedImplicitly]
        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}